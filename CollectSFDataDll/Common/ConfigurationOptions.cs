// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Azure;
using CollectSFData.DataFile;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace CollectSFData.Common
{
    public class ConfigurationOptions : Constants
    {
        private static readonly string _workDir = "csfd";
        private readonly CommandLineArguments _cmdLineArgs = new CommandLineArguments();
        private bool _defaultConfigLoaded;
        private string _endTime;
        private bool _logDebugEnabled;
        private string _startTime;
        private string _tempPath;
        private int _threads;

        public ConfigurationOptions()
        {
            _cmdLineArgs.CmdLineApp.OnExecute(() => MergeCmdLine());
            _cmdLineArgs.InitFromCmdLine();

            DateTimeOffset defaultOffset = DateTimeOffset.Now;
            StartTimeUtc = defaultOffset.UtcDateTime.AddHours(DefaultStartTimeHours);
            _startTime = defaultOffset.AddHours(DefaultStartTimeHours).ToString(DefaultDatePattern);
            EndTimeUtc = defaultOffset.UtcDateTime;
            _endTime = defaultOffset.ToString(DefaultDatePattern);
            DefaultConfig();
        }

        public string AzureClientId { get; set; }

        public string AzureClientSecret { get; set; }

        public string AzureResourceGroup { get; set; }

        public string AzureResourceGroupLocation { get; set; }

        public string AzureSubscriptionId { get; set; }

        public string AzureTenantId { get; set; }

        public string CacheLocation { get; set; }

        public string ConfigurationFile { get; set; }

        public string ContainerFilter { get; set; }

        public bool DeleteCache { get; set; }

        public string EndTimeStamp
        {
            get => _endTime;
            set
            {
                if (value == null)
                {
                    return;
                }

                DateTimeOffset dto;

                if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dto))
                {
                    EndTimeUtc = dto.UtcDateTime;
                    _endTime = value;
                    return;
                }

                throw new FormatException($"EndTimeStamp (--to) invalid format, got {value} but expecting pattern:{DefaultDatePattern}");
            }
        }

        public DateTimeOffset EndTimeUtc { get; private set; }

        public bool Examples { get; private set; }

        public FileTypesEnum FileType { get; private set; }

        public string GatherType
        {
            get => FileType.ToString();
            set
            {
                FileTypesEnum fileType = ConvertFileType(value);

                if (fileType == FileTypesEnum.unknown)
                {
                    Log.Warning($"GatherType unknown: {value}");
                }
                else
                {
                    FileType = fileType;
                }
            }
        }

        public string KustoCluster { get; set; }

        public bool KustoCompressed { get; set; } = true;

        public string KustoPurge { get; set; }

        public bool KustoRecreateTable { get; set; }

        public string KustoTable { get; set; }

        public bool KustoUseBlobAsSource { get; set; }

        public bool KustoUseIngestMessage { get; set; } = true; // todo: remove

        public bool List { get; set; }

        public bool LogAnalyticsCreate { get; set; }

        public string LogAnalyticsId { get; set; }

        public string LogAnalyticsKey { get; set; }

        public string LogAnalyticsName { get; set; }

        public string LogAnalyticsPurge { get; set; }

        public bool LogAnalyticsRecreate { get; set; }

        public string LogAnalyticsWorkspaceName { get; set; }

        public string LogAnalyticsWorkspaceSku { get; set; } = "PerGB2018";

        public bool LogDebug
        {
            get => Log.LogDebugEnabled = _logDebugEnabled;
            set => Log.LogDebugEnabled = _logDebugEnabled = value;
        }

        public string LogFile { get; set; }

        public string NodeFilter { get; set; }

        public int NoProgressTimeoutMin { get; set; } = 10;

        public string ResourceUri { get; set; }

        public SasEndpoints SasEndpointInfo { get; private set; } = new SasEndpoints();

        public string SasKey { get; set; } = string.Empty;

        public string SaveConfiguration { get; set; }

        public string Schema { get; set; }

        public string StartTimeStamp
        {
            get => _startTime;
            set
            {
                if (value == null)
                {
                    return;
                }

                DateTimeOffset dto;

                if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dto))
                {
                    StartTimeUtc = dto.UtcDateTime;
                    _startTime = value;
                    return;
                }

                throw new FormatException($"StartTimeStamp (--from) invalid format, got {value} but was expecting pattern:{DefaultDatePattern}");
            }
        }

        public DateTimeOffset StartTimeUtc { get; private set; }

        public int Threads
        {
            get => _threads < 1 ? Environment.ProcessorCount : _threads;
            set => _threads = value < 1 ? Environment.ProcessorCount : value;
        }

        public bool Unique { get; set; } = true;

        public string UriFilter { get; set; }

        public bool UseMemoryStream { get; set; } = true;

        public bool VersionOption { get; set; }

        public static FileTypesEnum ConvertFileType(string fileTypeString)
        {
            if (string.IsNullOrEmpty(fileTypeString) || !Enum.TryParse(fileTypeString.ToLower(), out FileTypesEnum fileType))
            {
                return FileTypesEnum.unknown;
            }

            return fileType;
        }

        public void CheckReleaseVersion()
        {
            string response = $"\r\n\tlocal running version: {Version}";
            Http http = new Http();
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("User-Agent", $"{AppDomain.CurrentDomain.FriendlyName}");

            try
            {
                if (new Ping().Send(new Uri(CodeLatestRelease).Host).Status == IPStatus.Success && http.SendRequest(uri: CodeLatestRelease, headers: headers))
                {
                    JToken downloadUrl = http.ResponseStreamJson.SelectToken("assets[0].browser_download_url");
                    JToken downloadVersion = http.ResponseStreamJson.SelectToken("tag_name");
                    JToken body = http.ResponseStreamJson.SelectToken("body");
                    response += $"\r\n\tlatest download release version: {downloadVersion.ToString()}";
                    response += $"\r\n\trelease notes: \r\n\t\t{body.ToString().Replace("\r\n", "\r\n\t\t")}";
                    response += $"\r\n\tlatest download release url: {downloadUrl.ToString()}";
                }

                Log.Last(response);
            }
            catch
            {
                Log.Last(response);
            }
        }

        public bool DefaultConfig()
        {
            if (File.Exists(DefaultOptionsFile))
            {
                MergeConfig(DefaultOptionsFile);
                _defaultConfigLoaded = true;
                return true;
            }

            return false;
        }

        public void DisplayStatus()
        {
            Log.Min($"      Gathering: {FileType.ToString()}", ConsoleColor.White);
            Log.Min($"         Source: {(SasEndpointInfo?.StorageAccountName ?? CacheLocation)}", ConsoleColor.White);
            Log.Min($"     Start Time: {StartTimeStamp}", ConsoleColor.White);
            Log.Min($"            UTC: {StartTimeUtc.ToString(DefaultDatePattern)}", ConsoleColor.White);
            Log.Min($"          Local: {StartTimeUtc.ToLocalTime().ToString(DefaultDatePattern)}", ConsoleColor.White);
            Log.Min($"       End Time: {EndTimeStamp}", ConsoleColor.White);
            Log.Min($"            UTC: {EndTimeUtc.ToString(DefaultDatePattern)}", ConsoleColor.White);
            Log.Min($"          Local: {EndTimeUtc.ToLocalTime().ToString(DefaultDatePattern)}", ConsoleColor.White);
            Log.Min($"        SAS key: {SasKey}", ConsoleColor.White);
            Log.Min($"        Threads: {Threads}", ConsoleColor.White);
            Log.Min($"  CacheLocation: {CacheLocation}", ConsoleColor.White);
            Log.Min($"     NodeFilter: {NodeFilter}", ConsoleColor.White);
            Log.Min($"      UriFilter: {UriFilter}", ConsoleColor.White);
            Log.Min($"ContainerFilter: {ContainerFilter}", ConsoleColor.White);

            if (IsKustoConfigured())
            {
                Log.Min($"KustoTable: {KustoTable}", ConsoleColor.White);
            }

            if (IsLogAnalyticsConfigured())
            {
                Log.Min($"LogAnalyticsName: {LogAnalyticsName}_CL", ConsoleColor.White);

                if (LogAnalyticsRecreate | LogAnalyticsCreate)
                {
                    Log.Warning($"new log analytics information: save to config file!");
                    Log.Min($"LogAnalyticsId: {LogAnalyticsId}", ConsoleColor.Yellow);
                    Log.Min($"LogAnalyticsKey: {LogAnalyticsKey}", ConsoleColor.Yellow);
                    Log.Min($"LogAnalyticsWorkspaceName: {LogAnalyticsWorkspaceName}", ConsoleColor.Yellow);
                    Log.Min($"AzureSubscriptionId: {AzureSubscriptionId}", ConsoleColor.Yellow);
                    Log.Min($"AzureResourceGroup: {AzureResourceGroup}", ConsoleColor.Yellow);
                    Log.Min($"AzureResourceGroupLocation: {AzureResourceGroupLocation}", ConsoleColor.Yellow);
                }
            }
        }

        public bool IsCacheLocationPreConfigured()
        {
            // saving config file with no options will set cache location to %temp% by default
            // collectsfdata.exe -save file.json
            return !(string.IsNullOrEmpty(CacheLocation) | (CacheLocation == _tempPath));
        }

        public bool IsClientIdConfigured()
        {
            return AzureClientId?.Length > 0 & AzureClientSecret?.Length > 0 & AzureTenantId?.Length > 0;
        }

        public bool IsKustoConfigured()
        {
            return (!FileType.Equals(FileTypesEnum.any) & !string.IsNullOrEmpty(KustoCluster) & !string.IsNullOrEmpty(KustoTable));
        }

        public bool IsKustoPurgeRequested()
        {
            return !string.IsNullOrEmpty(KustoPurge);
        }

        public bool IsLogAnalyticsConfigured()
        {
            return (!string.IsNullOrEmpty(LogAnalyticsId) | LogAnalyticsCreate)
                & (!string.IsNullOrEmpty(LogAnalyticsKey) | LogAnalyticsCreate)
                & !string.IsNullOrEmpty(LogAnalyticsName);
        }

        public bool IsLogAnalyticsPurgeRequested()
        {
            return !string.IsNullOrEmpty(LogAnalyticsPurge);
        }

        public bool PopulateConfig(string[] args)
        {
            try
            {
                _tempPath = FileManager.NormalizePath(Path.GetTempPath() + _workDir);

                if (args.Length == 0 & !_defaultConfigLoaded)
                {
                    Log.Last(_cmdLineArgs.CmdLineApp.GetHelpText());
                    return false;
                }

                if (args.Length == 1)
                {
                    // check for help and FTA
                    if (!args[0].StartsWith("/?") && !args[0].StartsWith("-") && args[0].EndsWith(".json") && File.Exists(args[0]))
                    {
                        ConfigurationFile = args[0];
                        MergeConfig(ConfigurationFile);
                        Log.Info($"setting options to {DefaultOptionsFile}", ConsoleColor.Yellow);
                    }
                    else if (args[0].StartsWith("/?") | args[0].StartsWith("-?") | args[0].StartsWith("--?"))
                    {
                        Log.Last(_cmdLineArgs.CmdLineApp.GetHelpText());
                        return false;
                    }
                }

                // check for name and value pair
                for (int i = 0; i < args.Length - 1; i++)
                {
                    string name = args[i];
                    string value = args[++i];

                    if (!Regex.IsMatch($"{name} {value}", "-\\w+ [^-]"))
                    {
                        Log.Error($"invalid argument pair: parameter name: {name} parameter value: {value}");
                        Log.Error("all parameters are required to have a value.");
                        return false;
                    }
                }

                if (!ParseCmdLine(args))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(ConfigurationFile))
                {
                    foreach (string file in ConfigurationFile.Split(','))
                    {
                        MergeConfig(file);
                    }

                    MergeCmdLine();
                }

                EndTimeUtc = EndTimeUtc.AddHours(WarningTimeSpanMinHours);
                Log.Highlight($"adding {WarningTimeSpanMinHours * 60} minutes to EndTimeUtc to compensate for sf file upload timer. New EndTimeUtc: ({EndTimeUtc.ToString("o")})");

                if (VersionOption)
                {
                    CheckReleaseVersion();
                    return false;
                }
                else if (Validate())
                {
                    Log.Info($"options:", ShallowCopy());
                    DisplayStatus();
                    return true;
                }

                Log.Warning($"review console output above for errors and warnings. refer to {CodeRepository} for additional information.");
                return false;
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
                Log.Last(_cmdLineArgs.CmdLineApp.GetHelpText());
                return false;
            }
        }

        public void SaveConfigFile()
        {
            if (string.IsNullOrEmpty(SaveConfiguration))
            {
                return;
            }

            // remove options that should not be saved in configuration file
            JObject options = JObject.FromObject(this);
            options.AddFirst(new JProperty("$schema", SchemaFile));
            options.Remove("Schema");
            options.Remove("ConfigurationFile");
            options.Remove("EndTimeUtc");
            options.Remove("Examples");
            options.Remove("FileType");
            options.Remove("SasEndpointInfo");
            options.Remove("SaveConfiguration");
            options.Remove("StartTimeUtc");
            options.Remove("VersionOption");

            if (IsKustoConfigured())
            {
                options.Property("KustoTable").Value = Regex.Replace(KustoTable, $"^{GatherType}_", "");
            }

            if (IsLogAnalyticsConfigured())
            {
                options.Property("LogAnalyticsName").Value = Regex.Replace(LogAnalyticsName, $"^{GatherType}_", "");
            }

            if (!IsCacheLocationPreConfigured())
            {
                options.Property("CacheLocation").Value = null;
            }

            Log.Info($"options results:", options);

            File.WriteAllText(SaveConfiguration, options.ToString());
            Log.Info($"configuration file saved to: {SaveConfiguration}", ConsoleColor.Green);
        }

        private void CheckCache()
        {
            if (!IsCacheLocationPreConfigured())
            {
                CacheLocation = _tempPath;
            }

            CacheLocation = FileManager.NormalizePath(CacheLocation);

            if (!Directory.Exists(CacheLocation))
            {
                Directory.CreateDirectory(CacheLocation);
            }
            else if (Directory.Exists(CacheLocation)
                & Directory.GetFileSystemEntries(CacheLocation).Length > 0
                & DeleteCache
                & SasEndpointInfo.IsPopulated())
            {
                // add working dir to outputlocation so it can be deleted
                string workDirPath = $"{CacheLocation}{Path.DirectorySeparatorChar}{Path.GetFileName(Path.GetTempFileName())}";
                Log.Warning($"outputlocation not empty and DeleteCache is enabled, creating work subdir {workDirPath}");

                if (!Directory.Exists(workDirPath))
                {
                    Directory.CreateDirectory(workDirPath);
                }

                Log.Info($"setting output location to: {workDirPath}");
                CacheLocation = workDirPath;
            }

            if (!UseMemoryStream && !CacheLocation.StartsWith("\\\\"))
            {
                DriveInfo drive = DriveInfo.GetDrives().FirstOrDefault(x => String.Equals(x.Name, Path.GetPathRoot(CacheLocation), StringComparison.OrdinalIgnoreCase));
                if (drive != null && drive.AvailableFreeSpace < ((long)1024 * 1024 * 1024 * 100))
                {
                    Log.Warning($"available free space in {CacheLocation} is less than 100 GB");
                }
            }

            if (DeleteCache & !SasEndpointInfo.IsPopulated())
            {
                Log.Warning($"setting 'DeleteCache' is set to true but no sas information provided.\r\nfiles will be deleted at exit!\r\nctrl-c now if this incorrect.");
                Thread.Sleep(ThreadSleepMsWarning);
            }
        }

        private void CheckLogFile()
        {
            if (!string.IsNullOrEmpty(LogFile))
            {
                Log.LogFile = FileManager.NormalizePath(LogFile);
                Log.Info($"setting output log file to: {LogFile}");
            }
        }

        private PropertyInfo[] InstanceProperties()
        {
            return GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        }

        private int MergeCmdLine()
        {
            try
            {
                PropertyInfo[] instanceProperties = InstanceProperties();
                PropertyInfo[] argumentProperties = _cmdLineArgs.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(x => x.PropertyType == typeof(CommandOption)).ToArray();

                foreach (PropertyInfo argumentProperty in argumentProperties)
                {
                    if (!instanceProperties.Any(x => x.Name.Equals(argumentProperty.Name)))
                    {
                        Log.Debug($"options / arguments properties mismatch", argumentProperty);
                        throw new Exception($"options / arguments properties mismatch:{argumentProperty.Name}");
                    }

                    if (!((CommandOption)argumentProperty.GetValue(_cmdLineArgs)).HasValue())
                    {
                        Log.Debug($"empty / null argument value. skipping", argumentProperty);
                        continue;
                    }

                    PropertyInfo instanceProperty = instanceProperties.First(x => x.Name.Equals(argumentProperty.Name));
                    object instanceValue = ((CommandOption)argumentProperty.GetValue(_cmdLineArgs)).Value();
                    Log.Debug($"argumentProperty:", argumentProperty);
                    Log.Debug($"instanceProperty:", instanceProperty);

                    if (instanceProperty.PropertyType == typeof(string))
                    {
                        SetPropertyValue(instanceProperty, instanceValue.ToString());
                    }
                    else if (instanceProperty.PropertyType == typeof(int))
                    {
                        SetPropertyValue(instanceProperty, Convert.ToInt32(instanceValue));
                    }
                    else if (instanceProperty.PropertyType == typeof(bool))
                    {
                        bool value = false;

                        if (Regex.IsMatch(instanceValue.ToString(), TrueStringPattern, RegexOptions.IgnoreCase))
                        {
                            value = true;
                        }
                        else if (!Regex.IsMatch(instanceValue.ToString(), FalseStringPattern, RegexOptions.IgnoreCase))
                        {
                            string error = $"{instanceProperty.Name} bool argument values on command line should either be {TrueStringPattern} or {FalseStringPattern}";
                            throw new ArgumentException(error);
                        }

                        SetPropertyValue(instanceProperty, value);
                    }
                    else
                    {
                        Log.Error($"undefined property type:", argumentProperty);
                        throw new Exception($"undefined property type:{argumentProperty.Name}");
                    }
                }

                if (Examples)
                {
                    _cmdLineArgs.DisplayExamples();
                    return 0;
                }

                return 1;
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
                return 0;
            }
        }

        public void MergeConfig(string optionsFile)
        {
            JObject fileOptions = ReadConfigFile(optionsFile);
            MergeConfig(fileOptions);
        }

        public void MergeConfig(ConfigurationOptions configurationOptions)
        {
            JObject options = JObject.FromObject(configurationOptions);
            MergeConfig(options);
        }

        public void MergeConfig(JObject fileOptions)
        {
            object instanceValue = null;

            if (fileOptions == null || !fileOptions.HasValues)
            {
                Log.Error($"empty options:", fileOptions);
                throw new ArgumentException();
            }

            PropertyInfo[] instanceProperties = InstanceProperties();

            foreach (KeyValuePair<string, JToken> fileOption in fileOptions)
            {
                Log.Debug($"JObject config option:", fileOption);

                if (!instanceProperties.Any(x => Regex.IsMatch(x.Name, fileOption.Key.Replace("$", ""), RegexOptions.IgnoreCase)))
                {
                    Log.Error($"unknown config file option:", fileOption);
                }
            }

            foreach (PropertyInfo instanceProperty in instanceProperties)
            {
                if (!fileOptions.ToObject<Dictionary<string, JToken>>().Any(x => Regex.IsMatch(x.Key, instanceProperty.Name, RegexOptions.IgnoreCase)))
                {
                    Log.Debug($"instance option not found in file:{instanceProperty.Name}");
                    continue;
                }

                JToken token = fileOptions.ToObject<Dictionary<string, JToken>>()
                    .First(x => Regex.IsMatch(x.Key, instanceProperty.Name, RegexOptions.IgnoreCase)).Value;
                Log.Debug($"token:", token);

                switch (token.Type)
                {
                    case JTokenType.Null:
                        instanceValue = null;
                        break;

                    case JTokenType.Boolean:
                        instanceValue = token.Value<bool>();
                        break;

                    case JTokenType.Integer:
                        instanceValue = token.Value<int>();
                        break;

                    case JTokenType.String:
                        instanceValue = token.Value<string>();
                        break;

                    case JTokenType.Uri:
                        instanceValue = token.Value<string>();
                        break;

                    case JTokenType.Date:
                        // issue with date and datetimeoffset
                        if (instanceProperty.PropertyType.Equals(typeof(string)))
                        {
                            instanceValue = token.Value<string>();
                        }
                        else
                        {
                            instanceValue = token.ToObject<DateTimeOffset>();
                        }

                        break;

                    case JTokenType.Guid:
                        instanceValue = token.Value<string>();
                        break;

                    default:
                        Log.Debug($"jtoken type unknown:", token);
                        continue;
                }

                SetPropertyValue(instanceProperty, instanceValue);
            }
        }

        private bool ParseCmdLine(string[] args)
        {
            try
            {
                // can only be called once
                if (_cmdLineArgs.CmdLineApp.Execute(args) == 0)
                {
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
                Log.Last(_cmdLineArgs.CmdLineApp.GetHelpText());
                return false;
            }
        }

        private JObject ReadConfigFile(string configFile)
        {
            JObject options = new JObject();

            try
            {
                Log.Info($"reading {configFile}", ConsoleColor.Yellow);
                options = (JObject)JsonConvert.DeserializeObject(File.ReadAllText(configFile));
                Log.Info($"options results:", options);
                return options;
            }
            catch (Exception e)
            {
                Log.Exception($"returning default options: exception:{e}");
                return options;
            }
        }

        private void SetPropertyValue(PropertyInfo propertyInstance, object instanceValue)
        {
            object thisValue = propertyInstance.GetValue(this);
            Log.Debug($"checking:{propertyInstance.Name}:{thisValue} -> {instanceValue}", propertyInstance);

            if (thisValue != null && thisValue.Equals(instanceValue))
            {
                Log.Debug("value same. skipping.");
                return;
            }

            if (propertyInstance.CanWrite)
            {
                propertyInstance.SetValue(this, instanceValue);
                Log.Debug($"modifying:{propertyInstance.Name} {thisValue} -> {instanceValue}", ConsoleColor.White);
            }
            else
            {
                Log.Debug($"property not writable:{propertyInstance.Name}");
            }
        }

        private ConfigurationOptions ShallowCopy()
        {
            return (ConfigurationOptions)MemberwiseClone();
        }

        public bool Validate()
        {
            try
            {
                bool retval = true;
                CheckLogFile();

                retval &= ValidateSasKey();
                retval &= ValidateFileType();
                retval &= ValidateTime();

                CheckCache();
                retval &= ValidateSource();
                retval &= ValidateDestination();
                retval &= ValidateAad();
                return retval;
            }
            catch (Exception e)
            {
                Log.Exception($"validate:exception:{e}");
                return false;
            }
        }

        public bool ValidateAad()
        {
            bool retval = true;
            bool needsAad = IsKustoConfigured() | IsKustoPurgeRequested() | IsLogAnalyticsConfigured();
            needsAad |= LogAnalyticsCreate | LogAnalyticsRecreate | IsLogAnalyticsPurgeRequested();

            if (needsAad | IsClientIdConfigured())
            {
                AzureResourceManager arm = new AzureResourceManager();
                retval = arm.Authenticate();

                // LA workspace commands require subscription id. if not specified and tenant has more than one, fail
                if ((LogAnalyticsCreate | LogAnalyticsRecreate) && string.IsNullOrEmpty(AzureSubscriptionId))
                {
                    if (arm.PopulateSubscriptions() && arm.Subscriptions.Length != 1)
                    {
                        Log.Error($"this configuration requires AzureSubscriptionId to be configured", arm.Subscriptions);
                        retval = false;
                    }
                    else
                    {
                        AzureSubscriptionId = arm.Subscriptions[0].id;
                        Log.Info($"AzureSubscriptionId set to {AzureSubscriptionId}");
                    }
                }
            }

            return retval;
        }

        public bool ValidateDestination()
        {
            bool retval = true;

            if (IsKustoConfigured() | IsKustoPurgeRequested())
            {
                KustoTable = FileType + "_" + KustoTable;
                Log.Info($"adding prefix to KustoTable: {KustoTable}");

                if (IsKustoPurgeRequested())
                {
                    retval = IsKustoConfigured();
                }

                if (!Regex.IsMatch(KustoCluster, KustoUrlPattern))
                {
                    string errMessage = $"invalid kusto url. should match pattern {KustoUrlPattern}\r\nexample: https://ingest-{{kustocluster}}.{{optional location}}.kusto.windows.net/{{kustodatabase}}";
                    Log.Error(errMessage);
                    retval = false;
                }
            }

            if (LogAnalyticsCreate & !IsLogAnalyticsConfigured())
            {
                if (string.IsNullOrEmpty(AzureResourceGroup) | string.IsNullOrEmpty(AzureResourceGroupLocation) | string.IsNullOrEmpty(LogAnalyticsWorkspaceName))
                {
                    Log.Error("LogAnalyticsWorkspaceName, AzureSubscriptionId, AzureResourceGroup, and AzureResourceGroupLocation are required for LogAnalyticsCreate");
                    retval = false;
                }
            }

            if (IsLogAnalyticsConfigured() | IsLogAnalyticsPurgeRequested())
            {
                LogAnalyticsName = FileType + "_" + LogAnalyticsName;
                Log.Info($"adding prefix to logAnalyticsName: {LogAnalyticsName}");

                if (IsLogAnalyticsConfigured() & Unique & string.IsNullOrEmpty(AzureSubscriptionId))
                {
                    Log.Error($"log analytics and 'Unique' require 'AzureSubscriptionId'. supply AzureSubscriptionId or set Unique to false.");
                    retval = false;
                }

                if (LogAnalyticsCreate & LogAnalyticsRecreate)
                {
                    Log.Error($"log analytics create and log analytics recreate *cannot* both be enabled. remove configuration for one");
                    retval = false;
                }

                if (IsLogAnalyticsPurgeRequested())
                {
                    if (string.IsNullOrEmpty(LogAnalyticsId) | string.IsNullOrEmpty(LogAnalyticsKey) | string.IsNullOrEmpty(LogAnalyticsName))
                    {
                        Log.Error("LogAnalyticsId, LogAnalyticsKey, and LogAnalyticsName are required for LogAnalyticsPurge");
                        retval = false;
                    }
                }

                if (LogAnalyticsRecreate)
                {
                    if (string.IsNullOrEmpty(LogAnalyticsId) | string.IsNullOrEmpty(LogAnalyticsKey) | string.IsNullOrEmpty(LogAnalyticsName))
                    {
                        Log.Error("LogAnalyticsId, LogAnalyticsKey, and LogAnalyticsName are required for LogAnalyticsRecreate");
                        retval = false;
                    }
                }
            }

            if (IsKustoConfigured() & IsLogAnalyticsConfigured())
            {
                Log.Error($"kusto and log analytics *cannot* both be enabled. remove configuration for one");
                retval = false;
            }

            if (!IsKustoConfigured() & !IsLogAnalyticsConfigured() & !IsCacheLocationPreConfigured())
            {
                Log.Error($"kusto or log analytics or cacheLocation must be configured for file destination.");
                retval = false;
            }

            if (!IsKustoConfigured() & !IsLogAnalyticsConfigured() & UseMemoryStream)
            {
                Log.Error($"kusto or log analytics must be configured for UseMemoryStream.");
                retval = false;
            }

            return retval;
        }

        public bool ValidateFileType()
        {
            bool retval = true;

            if (FileType == FileTypesEnum.unknown)
            {
                Log.Warning($"invalid -type|--gatherType argument, value can be:", Enum.GetNames(typeof(FileTypesEnum)).Skip(1));
                retval = false;
            }

            if (FileType == FileTypesEnum.any && (UseMemoryStream | DeleteCache))
            {
                Log.Warning($"setting UseMemoryStream and DeleteCache to false for FileType 'any'");
                UseMemoryStream = false;
                DeleteCache = false;
            }

            if (FileType != FileTypesEnum.trace && KustoUseBlobAsSource)
            {
                Log.Warning($"setting KustoUseBlobAsSource to false for FileType: {FileType}");
                KustoUseBlobAsSource = false;
            }

            return retval;
        }

        public bool ValidateSasKey()
        {
            if (!string.IsNullOrEmpty(SasKey))
            {
                SasEndpointInfo = new SasEndpoints(SasKey);
                return SasEndpointInfo.IsValid();
            }

            return true;
        }

        public bool ValidateSource()
        {
            bool retval = true;

            if (!SasEndpointInfo.IsPopulated() & !IsCacheLocationPreConfigured())
            {
                Log.Error($"sasKey or cacheLocation should be populated as file source.");
                retval = false;
            }

            return retval;
        }

        public bool ValidateTime()
        {
            bool retval = true;

            if (string.IsNullOrEmpty(_startTime) != string.IsNullOrEmpty(_endTime))
            {
                Log.Error("supply start and end time");
                retval = false;
            }

            if (!string.IsNullOrEmpty(_startTime) & !string.IsNullOrEmpty(_endTime))
            {
                if (EndTimeUtc <= StartTimeUtc)
                {
                    Log.Error("supply start time less than end time");
                    retval = false;
                }
                else if ((EndTimeUtc - StartTimeUtc).TotalHours > WarningTimeSpanHours)
                {
                    Log.Warning($"current time range hours ({(EndTimeUtc - StartTimeUtc).TotalHours}) over maximum recommended time range hours ({WarningTimeSpanHours})");
                }
                else if ((EndTimeUtc - StartTimeUtc).TotalHours < WarningTimeSpanMinHours)
                {
                    Log.Warning($"current time range hours ({(EndTimeUtc - StartTimeUtc).TotalHours}) below minimum recommended time range hours ({WarningTimeSpanMinHours})");
                }
            }

            return retval;
        }
    }
}