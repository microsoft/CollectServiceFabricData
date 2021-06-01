// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Tx.Windows;
using Tools.EtlReader;
using System.Fabric.Strings;

namespace CollectSFData.DataFile
{
    public class FileManager
    {
        private readonly CustomTaskManager _fileTasks = new CustomTaskManager();
        private ConfigurationOptions _config;
        private Instance _instance;
        private object _lockObj = new object();

        public FileManager(Instance instance)
        {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
            _config = _instance.Config;
        }

        public static string NormalizePath(string path, string directorySeparator = "/")
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (path.Contains("\\"))
            {
                path = path.Replace("\\\\", directorySeparator);
                path = path.Replace("\\", directorySeparator);
            }

            if (!directorySeparator.Equals("/"))
            {
                try
                {
                    Uri uri = new Uri(path);

                    if (!uri.IsFile)
                    {
                        return path;
                    }
                }
                catch { }

                path = path.Replace("/", directorySeparator);
            }

            return path;
        }

        public void DeleteFile(string fileUri)
        {
            if (File.Exists(fileUri))
            {
                Log.Info($"deleting file: {fileUri}");
                File.Delete(fileUri);
            }
        }

        public IList<CsvCounterRecord> ExtractPerfRelogCsvData(FileObject fileObject)
        {
            List<CsvCounterRecord> csvRecords = new List<CsvCounterRecord>();
            string counterPattern = @"\\\\.+?\\(?<object>.+?)(?<instance>\(.*?\)){0,1}\\(?<counter>.+)";

            try
            {
                IList<string> allLines = fileObject.Stream.Read();
                MatchCollection matchList = Regex.Matches(allLines[0], "\"(.+?)\"");
                string[] headers = matchList.Cast<Match>().Select(match => match.Value).ToArray();

                for (int csvRecordIndex = 1; csvRecordIndex < allLines.Count; csvRecordIndex++)
                {
                    string[] counterValues = allLines[csvRecordIndex].Split(',');

                    for (int headerIndex = 1; headerIndex < headers.Length; headerIndex++)
                    {
                        if (counterValues.Length > headerIndex)
                        {
                            string stringValue = counterValues[headerIndex].Trim('"').Trim(' ');
                            Match counterInfo = Regex.Match(headers[headerIndex], counterPattern);

                            if (!string.IsNullOrEmpty(stringValue))
                            {
                                try
                                {
                                    csvRecords.Add(new CsvCounterRecord()
                                    {
                                        Timestamp = Convert.ToDateTime(counterValues[0].Trim('"').Trim(' ')),
                                        CounterName = headers[headerIndex].Replace("\"", "").Trim(),
                                        CounterValue = Decimal.Parse(stringValue, NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint),
                                        Object = counterInfo.Groups["object"].Value.Replace("\"", "").Trim(),
                                        Counter = counterInfo.Groups["counter"].Value.Replace("\"", "").Trim(),
                                        Instance = Regex.Replace(counterInfo.Groups["instance"].Value, @"^\(|\)$", "").Trim(),
                                        NodeName = fileObject.NodeName,
                                        FileType = fileObject.FileDataType.ToString(),
                                        RelativeUri = fileObject.RelativeUri
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Log.Exception($"stringValue:{stringValue} exception:{ex}");
                                }
                            }
                        }
                    }
                }

                return csvRecords;
            }
            catch (Exception e)
            {
                Log.Exception($"{e}", csvRecords);
                return csvRecords;
            }
        }

        public FileObjectCollection FormatCounterFile(FileObject fileObject)
        {
            Log.Debug($"enter:{fileObject.FileUri}");
            string outputFile = fileObject.FileUri + Constants.PerfCsvExtension;
            bool result;

            fileObject.Stream.SaveToFile();
            DeleteFile(outputFile);
            Log.Info($"Writing {outputFile}");

            if (_config.UseTx)
            {
                result = TxBlg(fileObject, outputFile);
            }
            else
            {
                result = RelogBlg(fileObject, outputFile);
            }

            if (result)
            {
                _instance.TotalFilesConverted++;

                if (!_config.UseTx)
                {
                    fileObject.Stream.ReadFromFile(outputFile);
                    fileObject.Stream.Write<CsvCounterRecord>(ExtractPerfRelogCsvData(fileObject));
                }
            }
            else
            {
                _instance.TotalErrors++;
            }

            DeleteFile(outputFile);

            if (_config.UseMemoryStream | !_config.IsCacheLocationPreConfigured())
            {
                DeleteFile(fileObject.FileUri);
            }

            return PopulateCollection<CsvCounterRecord>(fileObject);
        }

        public FileObjectCollection FormatDtrFile(FileObject fileObject)
        {
            return FormatTraceFile<DtrTraceRecord>(fileObject);
        }

        public FileObjectCollection FormatEtlFile(FileObject fileObject)
        {
            Log.Debug($"enter:{fileObject.FileUri}");
            bool result;
            fileObject.Stream.SaveToFile();
            result = ReadEtl(fileObject);

            //todo review
            if (_config.DeleteCache && (_config.UseMemoryStream | !_config.IsCacheLocationPreConfigured()))
            {
                DeleteFile(fileObject.FileUri);
            }

            if (result)
            {
                _instance.TotalFilesConverted++;

                // if (!Config.UseTx)
                // {
                //     fileObject.Stream.ReadFromFile(outputFile);
                //     fileObject.Stream.Write<CsvCounterRecord>(ExtractPerfRelogCsvData(fileObject));
                // }

                return PopulateCollection<DtrTraceRecord>(fileObject);
            }

            _instance.TotalErrors++;
            return new FileObjectCollection();
        }

        public FileObjectCollection FormatExceptionFile(FileObject fileObject)
        {
            Log.Debug($"enter:{fileObject.FileUri}");
            IList<CsvExceptionRecord> records = new List<CsvExceptionRecord>
            {
                new CsvExceptionRecord($"{fileObject.FileUri}", fileObject, _config.ResourceUri)
            };

            Log.Last($"{fileObject.LastModified} {fileObject.FileUri}{_config.SasEndpointInfo.SasToken}", ConsoleColor.Cyan);
            fileObject.Stream.Write(records);
            return PopulateCollection<CsvExceptionRecord>(fileObject);
        }

        public FileObjectCollection FormatSetupFile(FileObject fileObject)
        {
            return FormatTraceFile<CsvSetupRecord>(fileObject);
        }

        public FileObjectCollection FormatTableFile(FileObject fileObject)
        {
            Log.Debug($"enter:{fileObject.FileUri}");
            return PopulateCollection<CsvTableRecord>(fileObject);
        }

        public FileObjectCollection FormatTraceFile<T>(FileObject fileObject) where T : ITraceRecord, new()
        {
            Log.Debug($"enter:{fileObject.FileUri}");
            IList<IRecord> records = new List<IRecord>();
            // handles dtr, setup, and deployer file timestamp formats
            string newEventPattern = @"^[0-9]{2,4}(-|/)[0-9]{1,2}(-|/)[0-9]{1,2}(-| )[0-9]{1,2}:[0-9]{1,2}:[0-9]{1,2}";
            Regex regex = new Regex(newEventPattern, RegexOptions.Compiled);
            string record = string.Empty;

            try
            {
                foreach (string tempLine in fileObject.Stream.ReadLine())
                {
                    if (regex.IsMatch(tempLine))
                    {
                        // new record, write old record
                        if (record.Length > 0)
                        {
                            records.Add(new T().Populate(fileObject, record, _config.ResourceUri));
                        }

                        record = string.Empty;
                    }

                    record += tempLine;
                }

                // last record
                if (record.Length > 0)
                {
                    records.Add(new T().Populate(fileObject, record, _config.ResourceUri));
                }

                Log.Debug($"finished format:{fileObject.FileUri}");

                fileObject.Stream.ResetPosition();
                fileObject.Stream.Write(records);
                return PopulateCollection<T>(fileObject);
            }
            catch (Exception e)
            {
                Log.Exception($"file:{fileObject.FileUri} exception:{e}");
                return new FileObjectCollection() { fileObject };
            }
        }

        public FileObjectCollection PopulateCollection<T>(FileObject fileObject) where T : IRecord
        {
            FileObjectCollection collection = new FileObjectCollection() { fileObject };
            _instance.TotalFilesFormatted++;
            _instance.TotalRecords += fileObject.RecordCount;

            if (_config.IsKustoConfigured())
            {
                // kusto native format is Csv
                // kusto json ingest is 2 to 3 times slower and does *not* use standard json format. uses json document per line no comma
                // using csv and compression for best performance
                collection = SerializeCsv<T>(fileObject);

                if (_config.KustoCompressed)
                {
                    collection.ForEach(x => x.Stream.Compress());
                }
            }
            else if (_config.IsLogAnalyticsConfigured())
            {
                // la is kusto based but only accepts non compressed json format ingest
                collection = SerializeJson<T>(fileObject);
            }

            collection.ForEach(x => SaveToCache(x));
            return collection;
        }

        public FileObjectCollection ProcessFile(FileObject fileObject)
        {
            Log.Debug($"enter:{fileObject.FileUri}");

            if (fileObject.DownloadAction != null)
            {
                Log.Info($"downloading:{fileObject.FileUri}", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
                fileObject.Status = FileStatus.downloading;
                _fileTasks.TaskAction(fileObject.DownloadAction).Wait();
                Log.Info($"downloaded:{fileObject.FileUri}", ConsoleColor.DarkCyan, ConsoleColor.DarkBlue);
            }

            if (fileObject.DownloadAction != null && fileObject.Length < 1 && !fileObject.Exists)
            {
                string error = $"memoryStream does not exist and file does not exist {fileObject.FileUri}";
                Log.Error(error);
                throw new ArgumentException(error);
            }

            fileObject.Status = FileStatus.formatting;

            if (!fileObject.FileType.Equals(FileTypesEnum.any))
            {
                if (fileObject.Length < 1 & fileObject.Exists)
                {
                    // for cached directory uploads
                    fileObject.Stream.ReadFromFile();
                }

                if (fileObject.FileExtensionType.Equals(FileExtensionTypesEnum.zip))
                {
                    fileObject.Stream.Decompress();
                    SaveToCache(fileObject);
                }
            }

            switch (fileObject.FileDataType)
            {
                case FileDataTypesEnum.unknown:
                    {
                        if (fileObject.FileType.Equals(FileTypesEnum.any))
                        {
                            SaveToCache(fileObject);
                            return new FileObjectCollection();
                        }

                        Log.Warning("unknown file", fileObject);
                        break;
                    }
                case FileDataTypesEnum.counter:
                    {
                        if (fileObject.FileExtensionType.Equals(FileExtensionTypesEnum.blg))
                        {
                            return FormatCounterFile(fileObject);
                        }

                        break;
                    }
                case FileDataTypesEnum.data:
                case FileDataTypesEnum.fabriccrashdumps:
                    {
                        if (fileObject.FileExtensionType.Equals(FileExtensionTypesEnum.dmp))
                        {
                            return FormatExceptionFile(fileObject);
                        }

                        SaveToCache(fileObject);
                        return new FileObjectCollection();
                    }
                case FileDataTypesEnum.fabric:
                case FileDataTypesEnum.lease:
                    {
                        if (fileObject.FileExtensionType.Equals(FileExtensionTypesEnum.dtr))
                        {
                            return FormatDtrFile(fileObject);
                        }
                        else if (fileObject.FileExtensionType.Equals(FileExtensionTypesEnum.etl))
                        {
                            return FormatEtlFile(fileObject);
                        }

                        break;
                    }
                case FileDataTypesEnum.bootstrap:
                case FileDataTypesEnum.fabricsetup:
                case FileDataTypesEnum.fabricdeployer:
                    {
                        if (fileObject.FileExtensionType.Equals(FileExtensionTypesEnum.trace))
                        {
                            return FormatSetupFile(fileObject);
                        }

                        break;
                    }
                case FileDataTypesEnum.table:
                    {
                        if (fileObject.FileExtensionType.Equals(FileExtensionTypesEnum.csv))
                        {
                            return FormatTableFile(fileObject);
                        }

                        break;
                    }
                default:
                    {
                        Log.Warning("unsupported file data type", fileObject.FileDataType);
                        break;
                    }
            }

            Log.Warning($"returning: empty fileObjectCollection for file: {fileObject.FileUri}");
            return new FileObjectCollection();
        }

        public bool ReadEtl(FileObject fileObject)
        {
            bool success = false;
            int recordsCount = 0;
            DateTime startTime = DateTime.Now;

            var action = new Action<DtrTraceRecord>((trace) =>
            {
                trace.FileType = fileObject.FileDataType.ToString();
                trace.NodeName = fileObject.NodeName;
                trace.RelativeUri = fileObject.RelativeUri;
                fileObject.Stream.Write<DtrTraceRecord>(new List<DtrTraceRecord>(1) { trace }, true);
                recordsCount++;
            });

            EtlTraceFileParser<DtrTraceRecord> parser = new EtlTraceFileParser<DtrTraceRecord>(_config);
            parser.ParseTraces(action, fileObject.FileUri, _config.StartTimeUtc.UtcDateTime, _config.EndTimeUtc.UtcDateTime);
            
            _instance.SetMinMaxDate(parser.TraceSessionMetaData.EndTime.Ticks, parser.TraceSessionMetaData.StartTime.Ticks);
            success = recordsCount != 0;
            fileObject.Status = success ? FileStatus.succeeded : FileStatus.failed;

            int totalMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
            Log.Info($"complete:total ms:{totalMs} total records:{recordsCount} records per second:{recordsCount / (totalMs * .001)}", ConsoleColor.Green);
            return success;
        }

        public TraceObserver<T> ReadTraceRecords<T>(IObservable<T> source)
        {
            DateTime startTime = DateTime.Now;
            TraceObserver<T> observer = new TraceObserver<T>();
            source.Subscribe(observer);
            int records = -1;
            bool waitResult = false;

            while (!waitResult && observer.Records.Count > records)
            {
                records = observer.Records.Count;
                Log.Debug($"waiting for completion:current record count{records}");
                waitResult = observer.Completed.Wait(Constants.ThreadSleepMs10000, _fileTasks.CancellationToken);
            }

            if (!waitResult)
            {
                _instance.TotalErrors++;
                Log.Error($"timed out waiting for observer progress.", source);
            }

            int totalMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
            int recordsCount = observer.Records.Count;
            double recordsPerSecond = recordsCount / (totalMs * .001);
            Log.Info($"complete:{waitResult} total ms:{totalMs} total records:{recordsCount} records per second:{recordsPerSecond}");
            return observer;
        }

        public bool RelogBlg(FileObject fileObject, string outputFile)
        {
            bool result = true;
            string csvParams = fileObject.FileUri + " -f csv -o " + outputFile;
            Log.Info($"relog.exe {csvParams}");
            ProcessStartInfo startInfo = new ProcessStartInfo("relog.exe", csvParams)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            Process convertFileProc = Process.Start(startInfo);

            if (!convertFileProc.HasExited)
            {
                if (convertFileProc.StandardOutput.Peek() > -1)
                {
                    Log.Info($"{convertFileProc.StandardOutput.ReadToEnd()}");
                }

                if (convertFileProc.StandardError.Peek() > -1)
                {
                    Log.Error($"{convertFileProc.StandardError.ReadToEnd()}");
                    result = false;
                }
            }

            convertFileProc.WaitForExit();
            return result;
        }

        public void SaveToCache(FileObject fileObject, bool force = false)
        {
            try
            {
                if (force || (!_config.UseMemoryStream && fileObject.FileUriType == FileUriTypesEnum.fileUri && !fileObject.Exists))
                {
                    fileObject.Stream.SaveToFile();
                }
            }
            catch (Exception e)
            {
                Log.Exception($"{e}", fileObject);
            }
        }

        public FileObjectCollection SerializeCsv<T>(FileObject fileObject) where T : IRecord
        {
            Log.Debug("enter");
            FileObjectCollection collection = new FileObjectCollection() { fileObject };
            int counter = 0;

            string sourceFile = fileObject.FileUri.ToLower().TrimEnd(Constants.CsvExtension.ToCharArray());
            fileObject.FileUri = $"{sourceFile}{Constants.CsvExtension}";
            List<byte> csvSerializedBytes = new List<byte>();
            string relativeUri = fileObject.RelativeUri.TrimEnd(Constants.CsvExtension.ToCharArray()) + Constants.CsvExtension;

            foreach (T record in fileObject.Stream.Read<T>())
            {
                record.RelativeUri = relativeUri;
                byte[] recordBytes = Encoding.UTF8.GetBytes(record.ToString());

                if (csvSerializedBytes.Count + recordBytes.Length > Constants.MaxCsvTransmitBytes)
                {
                    record.RelativeUri = relativeUri.TrimEnd(Constants.CsvExtension.ToCharArray()) + $".{counter}{Constants.CsvExtension}";
                    recordBytes = Encoding.UTF8.GetBytes(record.ToString());

                    fileObject.Stream.Set(csvSerializedBytes.ToArray());
                    csvSerializedBytes.Clear();

                    fileObject = new FileObject(record.RelativeUri, fileObject.BaseUri) { Status = FileStatus.formatting };
                    _instance.FileObjects.Add(fileObject);

                    Log.Debug($"csv serialized size: {csvSerializedBytes.Count} file: {fileObject.FileUri}");
                    collection.Add(fileObject);
                }

                csvSerializedBytes.AddRange(recordBytes);
                counter++;
            }

            fileObject.Stream.Set(csvSerializedBytes.ToArray());
            Log.Debug($"csv serialized size: {csvSerializedBytes.Count} file: {fileObject.FileUri}");
            return collection;
        }

        public FileObjectCollection SerializeJson<T>(FileObject fileObject) where T : IRecord
        {
            Log.Debug("enter");
            string sourceFile = fileObject.FileUri.ToLower().TrimEnd(Constants.JsonExtension.ToCharArray());
            fileObject.FileUri = $"{sourceFile}{Constants.JsonExtension}";
            FileObjectCollection collection = new FileObjectCollection();
            string relativeUri = fileObject.RelativeUri.TrimEnd(Constants.JsonExtension.ToCharArray()) + Constants.JsonExtension;

            if (fileObject.Length > Constants.MaxJsonTransmitBytes)
            {
                FileObject newFileObject = new FileObject($"{sourceFile}", fileObject.BaseUri) { Status = FileStatus.formatting };
                _instance.FileObjects.Add(newFileObject);
                int counter = 0;

                foreach (T record in fileObject.Stream.Read<T>())
                {
                    record.RelativeUri = relativeUri;
                    counter++;

                    if (newFileObject.Length < Constants.WarningJsonTransmitBytes)
                    {
                        newFileObject.Stream.Write<T>(new List<T>(1) { record }, true);
                    }
                    else
                    {
                        collection.Add(newFileObject);
                        record.RelativeUri = relativeUri.TrimEnd(Constants.JsonExtension.ToCharArray()) + $".{counter}{Constants.JsonExtension}";
                        newFileObject = new FileObject(record.RelativeUri, fileObject.BaseUri) { Status = FileStatus.formatting };
                        _instance.FileObjects.Add(newFileObject);
                    }
                }

                _instance.FileObjects.Remove(fileObject);
                newFileObject.FileUri = $"{sourceFile}.{counter}{Constants.JsonExtension}";
                collection.Add(newFileObject);
            }
            else
            {
                collection.Add(fileObject);
            }

            Log.Debug($"json serialized size: {fileObject.Length} file: {fileObject.FileUri}");
            return collection;
        }

        public bool TxBlg(FileObject fileObject, string outputFile)
        {
            // this forces blg output timestamps to use local capture timezone which is utc for azure
            // Tx module is not able to determine with PDH api blg source timezone
            TimeUtil.DateTimeKind = DateTimeKind.Unspecified;

            DateTime startTime = DateTime.Now;
            IObservable<PerformanceSample> observable = default(IObservable<PerformanceSample>);
            TraceObserver<PerformanceSample> counterSession = default(TraceObserver<PerformanceSample>);
            List<PerformanceSample> records = new List<PerformanceSample>();
            List<CsvCounterRecord> csvRecords = new List<CsvCounterRecord>();

            // testing pdh found invalid data when using concurrently
            lock (_lockObj)
            {
                Log.Debug($"observable creating: {fileObject.FileUri}");
                observable = PerfCounterObservable.FromFile(fileObject.FileUri);

                Log.Debug($"observable created: {fileObject.FileUri}");
                counterSession = ReadTraceRecords(observable);

                Log.Debug($"finished total ms: {DateTime.Now.Subtract(startTime).TotalMilliseconds} reading: {fileObject.FileUri}");
                records = counterSession.Records;
            }

            foreach (PerformanceSample record in records)
            {
                if (!string.IsNullOrEmpty(record.Value.ToString()))
                {
                    string counterValue = record.Value.ToString() == "NaN" ? "0" : record.Value.ToString();

                    try
                    {
                        csvRecords.Add(new CsvCounterRecord()
                        {
                            Timestamp = record.Timestamp,
                            CounterName = record.CounterPath.Replace("\"", "").Trim(),
                            CounterValue = Decimal.Parse(counterValue, NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint),
                            Object = record.CounterSet?.Replace("\"", "").Trim(),
                            Counter = record.CounterName.Replace("\"", "").Trim(),
                            Instance = record.Instance?.Replace("\"", "").Trim(),
                            NodeName = fileObject.NodeName,
                            FileType = fileObject.FileDataType.ToString(),
                            RelativeUri = fileObject.RelativeUri
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Exception($"stringValue:{counterValue} exception:{ex}", record);
                    }
                }
                else
                {
                    Log.Warning($"empty counter value:", record);
                }
            }

            fileObject.Stream.Write(csvRecords);
            Log.Info($"records: {records.Count()} {csvRecords.Count}");
            return true;
        }

        public bool TxEtl(FileObject fileObject, string outputFile)
        {
            // this forces blg output timestamps to use local capture timezone which is utc for azure
            // Tx module is not able to determine with PDH api blg source timezone
            // todo: verify if needed for etl...
            //TimeUtil.DateTimeKind = DateTimeKind.Unspecified;

            DateTime startTime = DateTime.Now;
            IObservable<EtwNativeEvent> observable = default(IObservable<EtwNativeEvent>);
            TraceObserver<EtwNativeEvent> traceSession = default(TraceObserver<EtwNativeEvent>);
            //List<EtwNativeEvent> records = new List<EtwNativeEvent>();
            List<DtrTraceRecord> csvRecords = new List<DtrTraceRecord>();

            // todo: verify if needed for etl...testing pdh found invalid data when using concurrently
            // lock (_lockObj)
            // {
            Log.Debug($"observable creating: {fileObject.FileUri}");
            observable = EtwObservable.FromFiles(fileObject.FileUri);

            Log.Debug($"observable created: {fileObject.FileUri}");
            traceSession = ReadTraceRecords(observable);
            Log.Debug($"finished total ms: {DateTime.Now.Subtract(startTime).TotalMilliseconds} reading: {fileObject.FileUri}");
            //    records = traceSession.Records;
            // }

            //foreach (EtwNativeEvent record in records)
            foreach (EtwNativeEvent record in traceSession.Records)
            {
                //Log.Info("record", record);
                //if (!string.IsNullOrEmpty(record.Value.ToString()))
                //{
                //    string counterValue = record.Value.ToString() == "NaN" ? "0" : record.Value.ToString();

                //    try
                //    {
                //        csvRecords.Add(new CsvCounterRecord()
                //        {
                //            Timestamp = record.Timestamp,
                //            CounterName = record.CounterPath.Replace("\"", "").Trim(),
                //            CounterValue = Decimal.Parse(counterValue, NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint),
                //            Object = record.CounterSet?.Replace("\"", "").Trim(),
                //            Counter = record.CounterName.Replace("\"", "").Trim(),
                //            Instance = record.Instance?.Replace("\"", "").Trim(),
                //            NodeName = fileObject.NodeName,
                //            FileType = fileObject.FileDataType.ToString(),
                //            RelativeUri = fileObject.RelativeUri
                //        });
                //    }
                //    catch (Exception ex)
                //    {
                //        Log.Exception($"stringValue:{counterValue} exception:{ex}", record);
                //    }
                //}
                //else
                //{
                //    Log.Warning($"empty counter value:", record);
                //}
            }

            fileObject.Stream.Write(csvRecords);
            Log.Info($"records: {traceSession.Records.Count()} {csvRecords.Count}");
            traceSession.Dispose();
            return true;
        }
    }
}