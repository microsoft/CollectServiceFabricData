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
using Tx.Windows;

namespace CollectSFData.DataFile
{
    public class FileManager : Constants
    {
        private readonly CustomTaskManager _fileTasks = new CustomTaskManager(true);
        private Instance _instance = Instance.Singleton();
        private object _lockObj = new object();

        private ConfigurationOptions Config => _instance.Config;

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

        public FileObjectCollection ProcessFile(FileObject fileObject)
        {
            Log.Debug($"enter:{fileObject.FileUri}");

            if (fileObject.DownloadAction != null)
            {
                Log.Info($"downloading:{fileObject.FileUri}", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
                _fileTasks.TaskAction(fileObject.DownloadAction).Wait();
                Log.Info($"downloaded:{fileObject.FileUri}", ConsoleColor.DarkCyan, ConsoleColor.DarkBlue);
            }

            if (fileObject.DownloadAction != null && fileObject.Length < 1 && !fileObject.Exists)
            {
                string error = $"memoryStream does not exist and file does not exist {fileObject.FileUri}";
                Log.Error(error);
                throw new ArgumentException(error);
            }

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

        private void DeleteFile(string fileUri)
        {
            if (File.Exists(fileUri))
            {
                Log.Info($"deleting file: {fileUri}");
                File.Delete(fileUri);
            }
        }

        private IList<CsvCounterRecord> ExtractPerfRelogCsvData(FileObject fileObject)
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

        private FileObjectCollection FormatCounterFile(FileObject fileObject)
        {
            Log.Debug($"enter:{fileObject.FileUri}");
            string outputFile = fileObject.FileUri + PerfCsvExtension;
            bool result;

            fileObject.Stream.SaveToFile();
            DeleteFile(outputFile);
            Log.Info($"Writing {outputFile}");

            if (Config.UseTx)
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

                if (!Config.UseTx)
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

            if (Config.UseMemoryStream | !Config.IsCacheLocationPreConfigured())
            {
                DeleteFile(fileObject.FileUri);
            }

            return PopulateCollection<CsvCounterRecord>(fileObject);
        }

        private FileObjectCollection FormatDtrFile(FileObject fileObject)
        {
            return FormatTraceFile<DtrTraceRecord>(fileObject);
        }

        private FileObjectCollection FormatExceptionFile(FileObject fileObject)
        {
            Log.Debug($"enter:{fileObject.FileUri}");
            IList<CsvExceptionRecord> records = new List<CsvExceptionRecord>
            {
                new CsvExceptionRecord($"{fileObject.FileUri}", fileObject, Config.ResourceUri)
            };

            Log.Last($"{fileObject.LastModified} {fileObject.FileUri}{Config.SasEndpointInfo.SasToken}", ConsoleColor.Cyan);
            fileObject.Stream.Write(records);
            return PopulateCollection<CsvExceptionRecord>(fileObject);
        }

        private FileObjectCollection FormatSetupFile(FileObject fileObject)
        {
            return FormatTraceFile<CsvSetupRecord>(fileObject);
        }

        private FileObjectCollection FormatTableFile(FileObject fileObject)
        {
            Log.Debug($"enter:{fileObject.FileUri}");
            return PopulateCollection<CsvTableRecord>(fileObject);
        }

        private FileObjectCollection FormatTraceFile<T>(FileObject fileObject) where T : ITraceRecord, new()
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
                            records.Add(new T().Populate(fileObject, record, Config.ResourceUri));
                        }

                        record = string.Empty;
                    }

                    record += tempLine;
                }

                // last record
                if (record.Length > 0)
                {
                    records.Add(new T().Populate(fileObject, record, Config.ResourceUri));
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

        private FileObjectCollection PopulateCollection<T>(FileObject fileObject) where T : IRecord
        {
            FileObjectCollection collection = new FileObjectCollection() { fileObject };
            _instance.TotalFilesFormatted++;
            _instance.TotalRecords += fileObject.RecordCount;

            if (Config.IsKustoConfigured())
            {
                // kusto native format is Csv
                // kusto json ingest is 2 to 3 times slower and does *not* use standard json format. uses json document per line no comma
                // using csv and compression for best performance
                collection = SerializeCsv<T>(fileObject);

                if (Config.KustoCompressed)
                {
                    collection.ForEach(x => x.Stream.Compress());
                }
            }
            else if (Config.IsLogAnalyticsConfigured())
            {
                // la is kusto based but only accepts non compressed json format ingest
                collection = SerializeJson<T>(fileObject);
            }

            collection.ForEach(x => SaveToCache(x));
            return collection;
        }

        private PerfCounterObserver<T> ReadCounterRecords<T>(IObservable<T> source)
        {
            var observer = new PerfCounterObserver<T>();
            source.Subscribe(observer);
            Log.Info($"complete: {observer.Complete}");
            return observer;
        }

        private bool RelogBlg(FileObject fileObject, string outputFile)
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

        private void SaveToCache(FileObject fileObject, bool force = false)
        {
            try
            {
                if (force || (!Config.UseMemoryStream & !fileObject.Exists))
                {
                    fileObject.Stream.SaveToFile();
                }
            }
            catch (Exception e)
            {
                Log.Exception($"{e}", fileObject);
            }
        }

        private FileObjectCollection SerializeCsv<T>(FileObject fileObject) where T : IRecord
        {
            Log.Debug("enter");
            FileObjectCollection collection = new FileObjectCollection() { fileObject };
            int counter = 0;

            string sourceFile = fileObject.FileUri.ToLower().TrimEnd(CsvExtension.ToCharArray());
            fileObject.FileUri = $"{sourceFile}{CsvExtension}";
            List<byte> csvSerializedBytes = new List<byte>();
            string relativeUri = fileObject.RelativeUri.TrimEnd(CsvExtension.ToCharArray()) + CsvExtension;

            foreach (T record in fileObject.Stream.Read<T>())
            {
                record.RelativeUri = relativeUri;
                byte[] recordBytes = Encoding.UTF8.GetBytes(record.ToString());

                if (csvSerializedBytes.Count + recordBytes.Length > MaxCsvTransmitBytes)
                {
                    record.RelativeUri = relativeUri.TrimEnd(CsvExtension.ToCharArray()) + $".{counter}{CsvExtension}";
                    recordBytes = Encoding.UTF8.GetBytes(record.ToString());

                    fileObject.Stream.Set(csvSerializedBytes.ToArray());
                    csvSerializedBytes.Clear();

                    fileObject = new FileObject(record.RelativeUri, fileObject.BaseUri);

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

        private FileObjectCollection SerializeJson<T>(FileObject fileObject) where T : IRecord
        {
            Log.Debug("enter");
            string sourceFile = fileObject.FileUri.ToLower().TrimEnd(JsonExtension.ToCharArray());
            fileObject.FileUri = $"{sourceFile}{JsonExtension}";
            FileObjectCollection collection = new FileObjectCollection();
            string relativeUri = fileObject.RelativeUri.TrimEnd(JsonExtension.ToCharArray()) + JsonExtension;

            if (fileObject.Length > MaxJsonTransmitBytes)
            {
                FileObject newFileObject = new FileObject($"{sourceFile}", fileObject.BaseUri);
                int counter = 0;

                foreach (T record in fileObject.Stream.Read<T>())
                {
                    record.RelativeUri = relativeUri;
                    counter++;

                    if (newFileObject.Length < WarningJsonTransmitBytes)
                    {
                        newFileObject.Stream.Write<T>(new List<T> { record }, true);
                    }
                    else
                    {
                        collection.Add(newFileObject);
                        record.RelativeUri = relativeUri.TrimEnd(JsonExtension.ToCharArray()) + $".{counter}{JsonExtension}";
                        newFileObject = new FileObject(record.RelativeUri, fileObject.BaseUri);
                    }
                }

                newFileObject.FileUri = $"{sourceFile}.{counter}{JsonExtension}";
                collection.Add(newFileObject);
            }
            else
            {
                collection.Add(fileObject);
            }

            Log.Debug($"json serialized size: {fileObject.Length} file: {fileObject.FileUri}");
            return collection;
        }

        private bool TxBlg(FileObject fileObject, string outputFile)
        {
            // this forces blg output timestamps to use local capture timezone which is utc for azure
            // Tx module is not able to determine with PDH api blg source timezone
            TimeUtil.DateTimeKind = DateTimeKind.Unspecified;

            DateTime startTime = DateTime.Now;
            IObservable<PerformanceSample> observable = default(IObservable<PerformanceSample>);
            PerfCounterObserver<PerformanceSample> counterSession = default(PerfCounterObserver<PerformanceSample>);
            List<PerformanceSample> records = new List<PerformanceSample>();
            List<CsvCounterRecord> csvRecords = new List<CsvCounterRecord>();

            // testing pdh found invalid data when using concurrently
            lock (_lockObj)
            {
                Log.Debug($"observable creating: {fileObject.FileUri}");
                observable = PerfCounterObservable.FromFile(fileObject.FileUri);

                Log.Debug($"observable created: {fileObject.FileUri}");
                counterSession = ReadCounterRecords(observable);

                Log.Debug($"finished total ms: {DateTime.Now.Subtract(startTime).TotalMilliseconds} reading: {fileObject.FileUri}");
                records = counterSession.Records;
            }

            foreach (var record in records)
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
    }
}