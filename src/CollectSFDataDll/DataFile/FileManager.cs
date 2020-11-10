// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CollectSFData.DataFile
{
    public class FileManager : Constants
    {
        private Instance _instance = Instance.Singleton();
        private ConfigurationOptions Config => _instance.Config;
        private readonly CustomTaskManager _fileTasks = new CustomTaskManager(true);

        public static string NormalizePath(string path, string directorySeparator = "/")
        {
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

            if (fileObject.DownloadAction != null && fileObject.Stream.Get().Length < 1 && !fileObject.Exists)
            {
                string error = $"memoryStream does not exist and file does not exist {fileObject.FileUri}";
                Log.Error(error);
                throw new ArgumentException(error);
            }

            if (!fileObject.FileType.Equals(FileTypesEnum.any))
            {
                if (fileObject.Stream.Get().Length < 1 & fileObject.Exists)
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

        public string RelogBlg(FileObject fileObject)
        {
            string outputFile = fileObject.FileUri + PerfCsvExtension;

            if (!(Config.FileType.Equals(FileTypesEnum.counter)))
            {
                return outputFile;
            }

            fileObject.Stream.SaveToFile();
            string csvParams = fileObject.FileUri + " -f csv -o " + outputFile;
            DeleteFile(outputFile);

            Log.Info($"Writing {outputFile}");
            Log.Info($"relog.exe {csvParams}");
            ProcessStartInfo startInfo = new ProcessStartInfo("relog.exe", csvParams)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                LoadUserProfile = false,
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
                }
            }

            convertFileProc.WaitForExit();
            _instance.TotalFilesConverted++;
            fileObject.Stream.ReadFromFile(outputFile);
            DeleteFile(outputFile);

            if (Config.UseMemoryStream | !Config.IsCacheLocationPreConfigured())
            {
                DeleteFile(fileObject.FileUri);
            }

            return outputFile;
        }

        private void DeleteFile(string fileUri)
        {
            if (File.Exists(fileUri))
            {
                Log.Info($"deleting file: {fileUri}");
                File.Delete(fileUri);
            }
        }

        private IList<CsvCounterRecord> ExtractPerfCsvData(FileObject fileObject)
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
                                        CounterName = headers[headerIndex],
                                        CounterValue = Decimal.Parse(stringValue, NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint),
                                        Object = counterInfo.Groups["object"].Value.Replace("\"","").Trim(),
                                        Counter = counterInfo.Groups["counter"].Value.Replace("\"","").Trim(),
                                        Instance = counterInfo.Groups["instance"].Value.Replace("\"","").Trim().Trim('(').Trim(')'),
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
            fileObject.FileUri = RelogBlg(fileObject);
            IList<CsvCounterRecord> records = ExtractPerfCsvData(fileObject);

            return PopulateCollection(fileObject, records);
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
            return PopulateCollection(fileObject, records);
        }

        private FileObjectCollection FormatSetupFile(FileObject fileObject)
        {
            return FormatTraceFile<CsvSetupRecord>(fileObject);
        }

        private FileObjectCollection FormatTableFile(FileObject fileObject)
        {
            Log.Debug($"enter:{fileObject.FileUri}");
            IList<CsvTableRecord> records = fileObject.Stream.Read<CsvTableRecord>();

            return PopulateCollection(fileObject, records);
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
                return PopulateCollection(fileObject, records.Cast<IRecord>().ToList());
            }
            catch (Exception e)
            {
                Log.Exception($"file:{fileObject.FileUri} exception:{e}");
                return new FileObjectCollection() { fileObject };
            }
        }

        private FileObjectCollection PopulateCollection<T>(FileObject fileObject, IList<T> records) where T : IRecord
        {
            FileObjectCollection collection = new FileObjectCollection() { fileObject };
            _instance.TotalFilesFormatted++;
            _instance.TotalRecords += records.Count;

            if (Config.IsKustoConfigured())
            {
                // kusto native format is Csv
                // kusto json ingest is 2 to 3 times slower and does *not* use standard json format. uses json document per line no comma
                // using csv and compression for best performance
                collection = SerializeCsv(fileObject, records);

                if (Config.KustoCompressed)
                {
                    collection.ForEach(x => x.Stream.Compress());
                }
            }
            else if (Config.IsLogAnalyticsConfigured())
            {
                // la is kusto based but only accepts non compressed json format ingest
                collection = SerializeJson(fileObject, records);
            }

            collection.ForEach(x => SaveToCache(x));
            records.Clear();
            return collection;
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

        private FileObjectCollection SerializeCsv<T>(FileObject fileObject, IList<T> records)
        {
            Log.Debug("enter");
            FileObjectCollection collection = new FileObjectCollection() { fileObject };
            int counter = 0;

            string sourceFile = fileObject.FileUri.ToLower().Replace(CsvExtension, "");
            fileObject.FileUri = $"{sourceFile}{CsvExtension}";
            List<byte> csvSerializedBytes = new List<byte>();

            foreach (T record in records)
            {
                byte[] recordBytes = Encoding.UTF8.GetBytes(record.ToString());

                if (csvSerializedBytes.Count + recordBytes.Length > MaxCsvTransmitBytes)
                {
                    fileObject.Stream.Set(csvSerializedBytes.ToArray());
                    fileObject.Length = fileObject.Stream.Get().Length;
                    csvSerializedBytes.Clear();

                    fileObject = new FileObject($"{sourceFile}.{counter}{CsvExtension}", fileObject.BaseUri);

                    Log.Debug($"csv serialized size: {csvSerializedBytes.Count} file: {fileObject.FileUri}");
                    collection.Add(fileObject);
                }

                csvSerializedBytes.AddRange(recordBytes);
                counter++;
            }

            fileObject.Stream.Set(csvSerializedBytes.ToArray());
            fileObject.Length = fileObject.Stream.Get().Length;

            Log.Debug($"csv serialized size: {csvSerializedBytes.Count} file: {fileObject.FileUri}");
            return collection;
        }

        private FileObjectCollection SerializeJson<T>(FileObject fileObject, IList<T> records)
        {
            Log.Debug("enter");
            FileObjectCollection collection = new FileObjectCollection() { fileObject };
            int counter = 0;

            string sourceFile = fileObject.FileUri.ToLower().Replace(JsonExtension, "");
            fileObject.FileUri = $"{sourceFile}{JsonExtension}";
            List<byte> jsonSerializedBytes = new List<byte>();

            byte[] leftBracket = Encoding.UTF8.GetBytes("[");
            byte[] rightBracket = Encoding.UTF8.GetBytes("]");
            byte[] comma = Encoding.UTF8.GetBytes(",");

            jsonSerializedBytes.AddRange(leftBracket);

            foreach (T record in records)
            {
                byte[] recordBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(record, new JsonSerializerSettings() { }));

                if (jsonSerializedBytes.Count + recordBytes.Length + rightBracket.Length > MaxJsonTransmitBytes)
                {
                    jsonSerializedBytes.AddRange(rightBracket);
                    fileObject.Stream.Set(jsonSerializedBytes.ToArray());
                    fileObject.Length = fileObject.Stream.Get().Length;
                    jsonSerializedBytes.Clear();

                    fileObject = new FileObject($"{sourceFile}.{counter}{JsonExtension}", fileObject.BaseUri);
                    jsonSerializedBytes.AddRange(leftBracket);

                    Log.Debug($"json serialized size: {jsonSerializedBytes.Count} file: {fileObject.FileUri}");
                    collection.Add(fileObject);
                }

                jsonSerializedBytes.AddRange(recordBytes);
                jsonSerializedBytes.AddRange(comma);
                counter++;
            }

            jsonSerializedBytes.RemoveRange(jsonSerializedBytes.Count - comma.Length, comma.Length);
            jsonSerializedBytes.AddRange(rightBracket);
            fileObject.Stream.Set(jsonSerializedBytes.ToArray());
            fileObject.Length = fileObject.Stream.Get().Length;

            Log.Debug($"json serialized size: {jsonSerializedBytes.Count} file: {fileObject.FileUri}");
            return collection;
        }
    }
}