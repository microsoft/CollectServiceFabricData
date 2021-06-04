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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;

namespace CollectSFData.Common
{
    public class ConfigurationOptions : ConfigurationProperties
    {
        private static readonly CommandLineArguments _cmdLineArgs = new CommandLineArguments();
        private static bool _cmdLineExecuted;
        private static bool? _cacheLocationPreconfigured = null;
        private static string[] _commandlineArguments = new string[0];
        private static ConfigurationOptions _defaultConfig;
        private readonly string _tempName = "csfd";
        private string _tempPath;

        public X509Certificate2 ClientCertificate { get; set; }

        public new string EndTimeStamp
        {
            get => base.EndTimeStamp;
            set
            {
                if (!string.IsNullOrEmpty(value) && base.EndTimeStamp != value)
                {
                    EndTimeUtc = ConvertToUtcTime(value);
                    base.EndTimeStamp = ConvertToUtcTimeString(value);
                }
            }
        }

        public string ExePath { get; } = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\{Constants.DefaultOptionsFile}";

        public FileTypesEnum FileType { get; private set; }

        public new string GatherType
        {
            get => base.GatherType;
            set
            {
                FileTypesEnum fileType = ConvertFileType(value);

                if (fileType == FileTypesEnum.unknown)
                {
                    Log.Warning($"GatherType unknown: {value}");
                }

                FileType = fileType;
                base.GatherType = FileType.ToString();
            }
        }

        public bool IsValid { get; set; }

        public bool NeedsValidation { get; set; } = true;

        public new string StartTimeStamp
        {
            get => base.StartTimeStamp;
            set
            {
                if (!string.IsNullOrEmpty(value) && base.StartTimeStamp != value)
                {
                    StartTimeUtc = ConvertToUtcTime(value);
                    base.StartTimeStamp = ConvertToUtcTimeString(value);
                }
            }
        }

        public new int Threads
        {
            get => base.Threads < 1 ? Environment.ProcessorCount : base.Threads;
            set => base.Threads = value < 1 ? Environment.ProcessorCount : value;
        }

        public string Version { get; } = $"{Process.GetCurrentProcess().MainModule?.FileVersionInfo.FileVersion}";
        private bool _defaultConfigLoaded => HasValue(_defaultConfig);

        static ConfigurationOptions()
        {
            _cmdLineArgs.InitFromCmdLine();
        }

        public ConfigurationOptions() : this(null)
        {
        }

        public ConfigurationOptions(string[] commandlineArguments = null, bool validate = false, bool loadDefaultConfig = true)
        {
            if (commandlineArguments != null)
            {
                _commandlineArguments = commandlineArguments;
            }

            _tempPath = FileManager.NormalizePath(Path.GetTempPath() + _tempName);

            DateTimeOffset defaultOffset = DateTimeOffset.Now;
            StartTimeUtc = defaultOffset.UtcDateTime.AddHours(Constants.DefaultStartTimeHours);
            StartTimeStamp = defaultOffset.AddHours(Constants.DefaultStartTimeHours).ToString(Constants.DefaultDatePattern);
            EndTimeUtc = defaultOffset.UtcDateTime;
            EndTimeStamp = defaultOffset.ToString(Constants.DefaultDatePattern);

            if (loadDefaultConfig)
            {
                LoadDefaultConfig();
            }

            if (validate)
            {
                Validate();
            }
        }

        public void CheckReleaseVersion()
        {
            string response = $"\r\n\tlocal running version: {Version}";
            Http http = Http.ClientFactory();
            http.DisplayError = false;

            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("User-Agent", $"{Constants.ApplicationName}");

            try
            {
                if (http.SendRequest(uri: Constants.CodeLatestRelease, headers: headers, httpMethod: HttpMethod.Head)
                     && http.SendRequest(uri: Constants.CodeLatestRelease, headers: headers))
                {
                    JToken downloadUrl = http.ResponseStreamJson.SelectToken("assets[0].browser_download_url");
                    JToken downloadVersion = http.ResponseStreamJson.SelectToken("tag_name");
                    JToken body = http.ResponseStreamJson.SelectToken("body");

                    if (new Version(Version) <= new Version(downloadVersion.ToString().TrimStart('v')))
                    {
                        response += $"\r\n\tlatest download release version: {downloadVersion.ToString()}";
                        response += $"\r\n\trelease notes: \r\n\t\t{body.ToString().Replace("\r\n", "\r\n\t\t")}";
                        response += $"\r\n\tlatest download release url: {downloadUrl.ToString()}";
                    }
                    else
                    {
                        response += $"\r\n\tlocal running version is latest.";
                    }
                }

                Log.Last(response);
            }
            catch
            {
                Log.Last(response);
            }
        }

        public ConfigurationOptions Clone()
        {
            return (ConfigurationOptions)MemberwiseClone();
        }

        public DateTime ConvertToUtcTime(string timeString)
        {
            DateTimeOffset dateTimeOffset;

            if (DateTimeOffset.TryParse(timeString, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTimeOffset))
            {
                Log.Debug($"TimeStamp valid format:input:'{timeString}'");
                return dateTimeOffset.UtcDateTime;
            }

            Log.Error($"TimeStamp invalid format:input:'{timeString}' but expecting pattern:'{Constants.DefaultDatePattern}' example:'{DateTime.Now.ToString(Constants.DefaultDatePattern)}'");
            return DateTime.MinValue;
        }

        public string ConvertToUtcTimeString(string timeString)
        {
            DateTime dateTime = DateTime.MinValue;

            if (!HasValue(timeString))
            {
                Log.Warning("empty time string");
            }
            else
            {
                dateTime = ConvertToUtcTime(timeString);
                if (dateTime != DateTime.MinValue)
                {
                    timeString = dateTime.ToString(Constants.DefaultDatePattern);
                }
            }

            Log.Debug($"returning:time string:'{timeString}'");
            return timeString;
        }

        public bool CreateDirectory(string directory)
        {
            try
            {
                if (string.IsNullOrEmpty(directory))
                {
                    return false;
                }

                if (!Directory.Exists(directory))
                {
                    Log.Info($"creating directory:{directory}");
                    Directory.CreateDirectory(directory);
                }
                else
                {
                    Log.Debug($"directory exists:{directory}");
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Exception($"exception:{e}");
                return false;
            }
        }

        public void DisplayStatus()
        {
            Log.Min($"      Gathering: {FileType.ToString()}", ConsoleColor.White);
            Log.Min($"         Source: {(SasEndpointInfo?.StorageAccountName ?? CacheLocation)}", ConsoleColor.White);
            Log.Min($"     Start Time: {StartTimeStamp}", ConsoleColor.White);
            Log.Min($"            UTC: {StartTimeUtc.ToString(Constants.DefaultDatePattern)}", ConsoleColor.White);
            Log.Min($"          Local: {StartTimeUtc.ToLocalTime().ToString(Constants.DefaultDatePattern)}", ConsoleColor.White);
            Log.Min($"       End Time: {EndTimeStamp}", ConsoleColor.White);
            Log.Min($"            UTC: {EndTimeUtc.ToString(Constants.DefaultDatePattern)}", ConsoleColor.White);
            Log.Min($"          Local: {EndTimeUtc.ToLocalTime().ToString(Constants.DefaultDatePattern)}", ConsoleColor.White);
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

        public void DownloadEtwManifests()
        {
            Log.Info($"Checking EtwManifestsCache:{Constants.EtwManifestsUrlIndex}");
            Http http = Http.ClientFactory();
            http.DisplayError = false;

            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("User-Agent", $"{Constants.ApplicationName}");

            if (!CreateDirectory(EtwManifestsCache))
            {
                return;
            }

            try
            {
                if (http.SendRequest(uri: Constants.EtwManifestsUrlIndex, headers: headers, httpMethod: HttpMethod.Head)
                     && http.SendRequest(uri: Constants.EtwManifestsUrlIndex, headers: headers))
                {
                    JArray manifests = http.ResponseStreamJson.SelectToken("manifests") as JArray;
                    Log.Info("manifests", manifests);

                    foreach (JToken manifest in manifests)
                    {
                        Log.Info($"downloading {manifest}");
                        http.SendRequest(uri: $"{Constants.EtwManifestsUrl}/{manifest}", headers: headers, expectJsonResult: false);

                        string manifestPath = $"{EtwManifestsCache}/{manifest}";
                        Log.Info($"saving {manifestPath}");
                        File.WriteAllText(manifestPath, http.ResponseStreamString);
                    }
                }
                else
                {
                    Log.Warning($"unable to connect to manifests url {Constants.EtwManifestsUrlIndex}");
                }
            }
            catch (Exception e)
            {
                Log.Exception($"exception:{e}");
            }
        }

        public ConfigurationOptions GetDefaultConfig()
        {
            LoadDefaultConfig();
            Log.Debug($"exit:", _defaultConfig);
            return _defaultConfig;
        }

        public bool HasValue(object property = null)
        {
            if (property != null)
            {
                if (property is string && string.IsNullOrEmpty(property.ToString()))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public bool IsCacheLocationPreConfigured()
        {
            if (_cacheLocationPreconfigured == null)
            {
                _cacheLocationPreconfigured = HasValue(CacheLocation);
                Log.Info($"{_cacheLocationPreconfigured}");
            }

            return (bool)_cacheLocationPreconfigured;
        }

        public bool IsClientIdConfigured()
        {
            bool configured = ((HasValue(AzureClientId) & HasValue(ClientCertificate)) // app registration configured
                || (HasValue(AzureClientId) & !HasValue(AzureKeyVault) & HasValue(AzureClientCertificate) & !HasValue(AzureClientSecret)) // app registration
                || (HasValue(AzureClientId) & !HasValue(AzureKeyVault) & !HasValue(AzureClientCertificate) & HasValue(AzureClientSecret)) // app registration with clientsecret
                || (HasValue(AzureClientId) & HasValue(AzureKeyVault) & !HasValue(AzureClientCertificate) & HasValue(AzureClientSecret)) // app registration with kv user managed
                || (!HasValue(AzureClientId) & HasValue(AzureKeyVault) & !HasValue(AzureClientCertificate) & HasValue(AzureClientSecret)) // system managed identity with kv
                || (HasValue(AzureClientId) & !HasValue(AzureKeyVault) & !HasValue(AzureClientCertificate) & !HasValue(AzureClientSecret)) // user managed identity
            );

            Log.Debug($"exit:configured:{configured} azureClientId:{AzureClientId} clientCertificate:{ClientCertificate} azureKeyVault:{AzureKeyVault} azureClientSecret:{AzureClientSecret}");
            return configured;
        }

        public bool IsGuidIfPopulated(string guid)
        {
            if (!HasValue(guid))
            {
                return true;
            }

            Guid testGuid = new Guid();
            return Guid.TryParse(guid, out testGuid);
        }

        public bool IsKustoConfigured()
        {
            return (!FileType.Equals(FileTypesEnum.any) & HasValue(KustoCluster) & HasValue(KustoTable));
        }

        public bool IsKustoPurgeRequested()
        {
            return HasValue(KustoPurge);
        }

        public bool IsLogAnalyticsConfigured()
        {
            return (HasValue(LogAnalyticsId) | LogAnalyticsCreate)
                & (HasValue(LogAnalyticsKey) | LogAnalyticsCreate)
                & HasValue(LogAnalyticsName);
        }

        public bool IsLogAnalyticsPurgeRequested()
        {
            return HasValue(LogAnalyticsPurge);
        }

        public void MergeConfig(string optionsFile)
        {
            JObject fileOptions = ReadConfigFile(optionsFile);
            MergeConfig(fileOptions);
        }

        public void MergeConfig(ConfigurationProperties configurationProperties)
        {
            JObject options = JObject.FromObject(configurationProperties);
            MergeConfig(options);
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
                Log.Debug($"JObject config option:{fileOption.Key}");

                if (!instanceProperties.Any(x => Regex.IsMatch(x.Name, fileOption.Key.Replace("$", ""), RegexOptions.IgnoreCase)))
                {
                    Log.Error($"unknown config file option:{fileOption.Key}");
                }
            }

            foreach (PropertyInfo instanceProperty in instanceProperties)
            {
                if (!fileOptions.ToObject<Dictionary<string, JToken>>().Any(x => Regex.IsMatch(x.Key, $"^{instanceProperty.Name}$", RegexOptions.IgnoreCase)))
                {
                    Log.Debug($"instance option not found in file:{instanceProperty.Name}");
                    continue;
                }

                JToken token = fileOptions.ToObject<Dictionary<string, JToken>>()
                    .First(x => Regex.IsMatch(x.Key, $"^{instanceProperty.Name}$", RegexOptions.IgnoreCase)).Value;
                Log.Debug($"token:{token.Type}");

                switch (token.Type)
                {
                    case JTokenType.Array:
                        instanceValue = token.Values<string>().ToArray();
                        break;

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
                        Log.Debug($"jtoken type unknown:{token}");
                        continue;
                }

                SetPropertyValue(instanceProperty, instanceValue);
            }

            SetDefaultConfig(Clone());
        }

        public ConfigurationProperties PropertyClone()
        {
            return (ConfigurationProperties)base.MemberwiseClone();
        }

        public string SaveConfigFile()
        {
            if (!HasValue(SaveConfiguration))
            {
                return null;
            }

            // remove options that should not be saved in configuration file
            JObject options = JObject.FromObject(this);
            options.AddFirst(new JProperty("$schema", Constants.SchemaFile));
            options.Remove("Schema");
            options.Remove("ClientCertificate");
            options.Remove("ConfigurationFile");
            options.Remove("EndTimeUtc");
            options.Remove("Examples");
            options.Remove("ExePath");
            options.Remove("FileType");
            options.Remove("IsValid");
            options.Remove("NeedsValidation");
            options.Remove("SasEndpointInfo");
            options.Remove("SaveConfiguration");
            options.Remove("StartTimeUtc");
            options.Remove("VersionOption");
            options.Remove("Version");

            if (IsKustoConfigured())
            {
                options.Property("KustoTable").Value = CleanTableName(KustoTable);
            }

            if (IsLogAnalyticsConfigured())
            {
                options.Property("LogAnalyticsName").Value = CleanTableName(LogAnalyticsName);
            }

            if (!IsCacheLocationPreConfigured())
            {
                options.Property("CacheLocation").Value = null;
            }

            Log.Info($"options results:", options);

            File.WriteAllText(SaveConfiguration, options.ToString());
            Log.Info($"configuration file saved to: {SaveConfiguration}", ConsoleColor.Green);
            return options.ToString();
        }

        public void SetDefaultConfig(ConfigurationOptions configurationOptions)
        {
            Log.Debug($"enter:", configurationOptions);
            _defaultConfig = configurationOptions;
        }

        public bool Validate()
        {
            try
            {
                bool retval = true;
                NeedsValidation = false;

                if (!ProcessArguments())
                {
                    retval = false;
                }
                else
                {
                    retval &= ValidateSasKey();
                    CheckCache();
                    CheckEtwManifestsCache();
                    CheckLogFile();

                    retval &= ValidateFileType();
                    retval &= ValidateTime();
                    retval &= ValidateSource();
                    retval &= ValidateDestination();
                    retval &= ValidateAad();

                    if (retval)
                    {
                        Log.Info($"options:", Clone());
                        DisplayStatus();
                    }
                    else
                    {
                        Log.Warning($"review console output above for errors and warnings. refer to {Constants.CodeRepository} for additional information.");
                    }
                }

                SaveConfigFile();
                return IsValid = retval;
            }
            catch (Exception e)
            {
                Log.Exception($"validate:exception:{e}");
                return IsValid = false;
            }
        }

        public bool ValidateAad()
        {
            CertificateUtilities certificateUtilities = new CertificateUtilities();
            AzureResourceManager arm = new AzureResourceManager(this);
            bool retval = true;
            bool clientIdConfigured = IsClientIdConfigured();
            bool usingAad = clientIdConfigured | IsKustoConfigured() | IsKustoPurgeRequested();
            usingAad |= LogAnalyticsCreate | LogAnalyticsRecreate | IsLogAnalyticsPurgeRequested() | IsLogAnalyticsConfigured();

            if (!IsGuidIfPopulated(AzureClientId))
            {
                Log.Error($"invalid client id value:{AzureClientId} expected:guid");
                retval &= false;
            }

            if (!IsGuidIfPopulated(AzureSubscriptionId))
            {
                Log.Error($"invalid subscription id value:{AzureSubscriptionId} expected:guid");
                retval &= false;
            }

            if (!IsGuidIfPopulated(AzureTenantId))
            {
                Log.Error($"invalid tenant id value:{AzureTenantId} expected:guid");
                retval &= false;
            }

            if (HasValue(AzureKeyVault) && FileTypes.MapFileUriType(AzureKeyVault) != FileUriTypesEnum.azureKeyVaultUri)
            {
                Log.Error($"invalid key vault value:{AzureKeyVault} expected:{FileUriTypesEnum.azureKeyVaultUri}");
                retval &= false;
            }

            if (usingAad)
            {
                if (clientIdConfigured && HasValue(AzureClientCertificate) && !HasValue(ClientCertificate))
                {
                    ClientCertificate = certificateUtilities.GetClientCertificate(AzureClientCertificate);
                    if (!HasValue(ClientCertificate))
                    {
                        Log.Error("Failed to find certificate");
                        retval &= false;
                    }
                }

                if (HasValue(ClientCertificate))
                {
                    if (!certificateUtilities.CheckCertificate(ClientCertificate))
                    {
                        Log.Error("Failed certificate check");
                        retval &= false;
                    }
                }
                // else
                // {
                //     Log.Error("Failed certificate to find certificate");
                //     retval &= false;
                // }

                retval &= arm.Authenticate();

                if (clientIdConfigured & !arm.ClientIdentity.IsTypeManagedIdentity & !arm.ClientIdentity.IsAppRegistration)
                {
                    Log.Warning($"unable to detect managed identity. verify azure client configuration settings and set both AzureClientId and AzureClientSecret.");
                }

                // LA workspace commands require subscription id. if not specified and tenant has more than one, fail
                if ((LogAnalyticsCreate | LogAnalyticsRecreate) && !HasValue(AzureSubscriptionId))
                {
                    if (arm.PopulateSubscriptions() && arm.Subscriptions.Length != 1)
                    {
                        Log.Error($"this configuration requires AzureSubscriptionId to be configured", arm.Subscriptions);
                        retval &= false;
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
                KustoTable = CleanTableName(KustoTable, true);
                Log.Info($"adding prefix to KustoTable: {KustoTable}");

                if (IsKustoPurgeRequested())
                {
                    retval = IsKustoConfigured();
                }

                if (!Regex.IsMatch(KustoCluster, Constants.KustoUrlPattern))
                {
                    string errMessage = $"invalid kusto url. should match pattern {Constants.KustoUrlPattern}\r\nexample: https://ingest-{{kustocluster}}.{{optional location}}.kusto.windows.net/{{kustodatabase}}";
                    Log.Error(errMessage);
                    retval = false;
                }
            }

            if (LogAnalyticsCreate & !IsLogAnalyticsConfigured())
            {
                if (!HasValue(AzureResourceGroup) | !HasValue(AzureResourceGroupLocation) | !HasValue(LogAnalyticsWorkspaceName))
                {
                    Log.Error("LogAnalyticsWorkspaceName, AzureSubscriptionId, AzureResourceGroup, and AzureResourceGroupLocation are required for LogAnalyticsCreate");
                    retval = false;
                }
            }

            if (IsLogAnalyticsConfigured() | IsLogAnalyticsPurgeRequested())
            {
                LogAnalyticsName = CleanTableName(LogAnalyticsName, true);
                Log.Info($"adding prefix to logAnalyticsName: {LogAnalyticsName}");

                if (IsLogAnalyticsConfigured() & Unique & !HasValue(AzureSubscriptionId))
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
                    if (!HasValue(LogAnalyticsId) | !HasValue(LogAnalyticsKey) | !HasValue(LogAnalyticsName))
                    {
                        Log.Error("LogAnalyticsId, LogAnalyticsKey, and LogAnalyticsName are required for LogAnalyticsPurge");
                        retval = false;
                    }
                }

                if (LogAnalyticsRecreate)
                {
                    if (!HasValue(LogAnalyticsId) | !HasValue(LogAnalyticsKey) | !HasValue(LogAnalyticsName))
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

            if (FileType != FileTypesEnum.unknown && FileType != FileTypesEnum.trace && KustoUseBlobAsSource)
            {
                Log.Warning($"setting KustoUseBlobAsSource to false for FileType: {FileType}");
                KustoUseBlobAsSource = false;
            }

            if (FileUris.Length > 0 && KustoUseBlobAsSource)
            {
                Log.Warning($"setting KustoUseBlobAsSource to false for FileUris");
                KustoUseBlobAsSource = false;
            }

            return retval;
        }

        public bool ValidateSasKey()
        {
            if (HasValue(SasKey))
            {
                SasEndpointInfo = new SasEndpoints(SasKey);
                return SasEndpointInfo.IsValid();
            }

            return true;
        }

        public bool ValidateSource()
        {
            bool retval = true;

            if (!SasEndpointInfo.IsPopulated() & !IsCacheLocationPreConfigured() & FileUris.Length < 1)
            {
                Log.Error($"sasKey, fileUris, or cacheLocation should be populated as file source.");
                retval = false;
            }

            return retval;
        }

        public bool ValidateTime()
        {
            Log.Info("enter");
            bool retval = true;

            if (HasValue(StartTimeStamp) != HasValue(EndTimeStamp))
            {
                Log.Error("supply start and end time");
                retval = false;
            }

            if (HasValue(StartTimeStamp) & HasValue(EndTimeStamp))
            {
                if (ConvertToUtcTime(StartTimeStamp) == DateTime.MinValue | ConvertToUtcTime(EndTimeStamp) == DateTime.MinValue)
                {
                    retval = false;
                }
                else if (EndTimeUtc <= StartTimeUtc)
                {
                    Log.Error("supply start time less than end time");
                    retval = false;
                }
                else if ((EndTimeUtc - StartTimeUtc).TotalHours > Constants.WarningTimeSpanHours)
                {
                    Log.Warning($"current time range hours ({(EndTimeUtc - StartTimeUtc).TotalHours}) over maximum recommended time range hours ({Constants.WarningTimeSpanHours})");
                }
                else if ((EndTimeUtc - StartTimeUtc).TotalHours < Constants.WarningTimeSpanMinHours)
                {
                    Log.Warning($"current time range hours ({(EndTimeUtc - StartTimeUtc).TotalHours}) below minimum recommended time range hours ({Constants.WarningTimeSpanMinHours})");
                }
            }

            Log.Info($"exit:return:{retval}");
            return retval;
        }

        private static FileTypesEnum ConvertFileType(string fileTypeString)
        {
            if (string.IsNullOrEmpty(fileTypeString) || !Enum.TryParse(fileTypeString.ToLower(), out FileTypesEnum fileType))
            {
                return FileTypesEnum.unknown;
            }

            return fileType;
        }

        private void CheckCache()
        {
            if (!IsCacheLocationPreConfigured())
            {
                CacheLocation = _tempPath;
            }

            if (!Directory.Exists(CacheLocation))
            {
                CreateDirectory(CacheLocation);
            }
            else if (Directory.Exists(CacheLocation)
                & Directory.GetFileSystemEntries(CacheLocation).Length > 0
                & DeleteCache
                & SasEndpointInfo.IsPopulated())
            {
                // add working dir to outputlocation so it can be deleted
                string workDirPath = $"{CacheLocation}{Path.GetFileName(Path.GetTempFileName())}";
                Log.Warning($"outputlocation not empty and DeleteCache is enabled, creating work subdir {workDirPath}");
                CreateDirectory(workDirPath);
                CacheLocation = workDirPath;
            }

            if (!UseMemoryStream && !CacheLocation.StartsWith("\\\\"))
            {
                DriveInfo drive = DriveInfo.GetDrives().FirstOrDefault(x => String.Equals(x.Name, Path.GetPathRoot(CacheLocation), StringComparison.OrdinalIgnoreCase));
                if (HasValue(drive) && drive.AvailableFreeSpace < ((long)1024 * 1024 * 1024 * 100))
                {
                    Log.Warning($"available free space in {CacheLocation} is less than 100 GB");
                }
            }

            if (DeleteCache & !SasEndpointInfo.IsPopulated())
            {
                Log.Warning($"setting 'DeleteCache' is set to true but no sas information provided.\r\nfiles will be deleted at exit!\r\nctrl-c now if this incorrect.");
                Thread.Sleep(Constants.ThreadSleepMsWarning);
            }

            CacheLocation = FileManager.NormalizePath(CacheLocation);
            Log.Info($"output location set to: {CacheLocation}");
        }

        private void CheckEtwManifestsCache()
        {
            if (!HasValue(EtwManifestsCache))
            {
                EtwManifestsCache = Constants.EtwDefaultManifestsCache;
                Log.Info($"setting EtwManifestsCache default value:{EtwManifestsCache}");
            }

            EtwManifestsCache = FileManager.NormalizePath(EtwManifestsCache);

            if (!Directory.Exists(EtwManifestsCache))
            {
                Log.Info($"creating EtwManifestsCache:{EtwManifestsCache}");
                CreateDirectory(EtwManifestsCache);
                DownloadEtwManifests();
            }
        }

        private void CheckLogFile()
        {
            if (LogDebug == LoggingLevel.Verbose && !HasValue(LogFile))
            {
                LogFile = $"{CacheLocation}/{_tempName}.log";
                Log.Warning($"LogDebug 5 (Verbose) requires log file. setting LogFile:{LogFile}");
            }

            if (HasValue(LogFile))
            {
                LogFile = FileManager.NormalizePath(LogFile);
                CreateDirectory(Path.GetDirectoryName(LogFile));
                Log.Info($"setting output log file to: {LogFile}");
            }
        }

        private string CleanTableName(string tableName, bool withGatherType = false)
        {
            if (withGatherType)
            {
                return FileType + "_" + Regex.Replace(tableName, $"^({FileType}_)", "", RegexOptions.IgnoreCase);
            }

            return Regex.Replace(tableName, $"^({FileType}_)", "", RegexOptions.IgnoreCase);
        }

        private PropertyInfo[] InstanceProperties()
        {
            return GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        }

        private bool LoadDefaultConfig()
        {
            if (_defaultConfig == null)
            {
                if (File.Exists(Constants.DefaultOptionsFile))
                {
                    MergeConfig(Constants.DefaultOptionsFile);
                    return true;
                }
                else if (File.Exists(ExePath))
                {
                    MergeConfig(ExePath);
                    return true;
                }

                SetDefaultConfig(Clone());
                return false;
            }
            else
            {
                MergeConfig(_defaultConfig);
            }

            return true;
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
                        Log.Debug($"options / arguments properties mismatch: {argumentProperty.Name}");
                        throw new Exception($"options / arguments properties mismatch:{argumentProperty.Name}");
                    }

                    if (!((CommandOption)argumentProperty.GetValue(_cmdLineArgs)).HasValue())
                    {
                        Log.Debug($"empty / null argument value. skipping: {argumentProperty.Name}");
                        continue;
                    }

                    PropertyInfo instanceProperty = instanceProperties.First(x => x.Name.Equals(argumentProperty.Name));
                    object instanceValue = ((CommandOption)argumentProperty.GetValue(_cmdLineArgs)).Value();
                    Log.Debug($"argumentProperty: {argumentProperty.Name}");
                    Log.Debug($"instanceProperty: {instanceProperty.Name}");

                    if (instanceProperty.PropertyType == typeof(string[]))
                    {
                        SetPropertyValue(instanceProperty, instanceValue.ToString().Split(','));
                    }
                    else if (instanceProperty.PropertyType == typeof(string))
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

                        if (Regex.IsMatch(instanceValue.ToString(), Constants.TrueStringPattern, RegexOptions.IgnoreCase))
                        {
                            value = true;
                        }
                        else if (!Regex.IsMatch(instanceValue.ToString(), Constants.FalseStringPattern, RegexOptions.IgnoreCase))
                        {
                            string error = $"{instanceProperty.Name} bool argument values on command line should either be {Constants.TrueStringPattern} or {Constants.FalseStringPattern}";
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

                SetDefaultConfig(Clone());
                return 1;
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
                return 0;
            }
        }

        private bool ParseCmdLine(string[] args)
        {
            try
            {
                // can only be called once
                if (!_cmdLineExecuted & args.Any())
                {
                    _cmdLineExecuted = true;
                    _cmdLineArgs.CmdLineApp.OnExecute(() => MergeCmdLine());

                    if (_cmdLineArgs.CmdLineApp.Execute(args) == 0)
                    {
                        return false;
                    }
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

        private bool ProcessArguments()
        {
            try
            {
                if (_cmdLineExecuted)
                {
                    return true;
                }
                else if (_commandlineArguments.Length == 0 && !_defaultConfigLoaded && GatherType == FileTypesEnum.unknown.ToString())
                {
                    Log.Last(_cmdLineArgs.CmdLineApp.GetHelpText());
                    Log.Last("error: no configuration provided");
                    return false;
                }

                if (_commandlineArguments.Length == 1)
                {
                    // check for help and FTA
                    if (!_commandlineArguments[0].StartsWith("/?") && !_commandlineArguments[0].StartsWith("-") && _commandlineArguments[0].EndsWith(".json") && File.Exists(_commandlineArguments[0]))
                    {
                        ConfigurationFile = _commandlineArguments[0];
                        MergeConfig(ConfigurationFile);
                        Log.Info($"setting options to {Constants.DefaultOptionsFile}", ConsoleColor.Yellow);
                    }
                    else if (_commandlineArguments[0].StartsWith("/?") | _commandlineArguments[0].StartsWith("-?") | _commandlineArguments[0].StartsWith("--?"))
                    {
                        Log.Last(_cmdLineArgs.CmdLineApp.GetHelpText());
                        return false;
                    }
                }

                // check for name and value pair
                for (int i = 0; i < _commandlineArguments.Length - 1; i++)
                {
                    string name = _commandlineArguments[i];
                    string value = _commandlineArguments[++i];

                    if (!Regex.IsMatch($"{name} {value}", "-\\w+ [^-]"))
                    {
                        Log.Error($"invalid argument pair: parameter name: {name} parameter value: {value}");
                        Log.Error("all parameters are required to have a value.");
                        return false;
                    }
                }

                if (!ParseCmdLine(_commandlineArguments))
                {
                    return false;
                }

                if (HasValue(ConfigurationFile))
                {
                    foreach (string file in ConfigurationFile.Split(','))
                    {
                        MergeConfig(file);
                    }

                    MergeCmdLine();
                }

                EndTimeUtc = EndTimeUtc.AddHours(Constants.WarningTimeSpanMinHours);
                Log.Highlight($"adding {Constants.WarningTimeSpanMinHours * 60} minutes to EndTimeUtc to compensate for sf file upload timer. New EndTimeUtc: ({EndTimeUtc.ToString("o")})");

                if (VersionOption)
                {
                    CheckReleaseVersion();
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
                Log.Info($"reading {Path.GetFullPath(configFile)}", ConsoleColor.Yellow);
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
            Log.Debug($"checking:{propertyInstance.Name}:{thisValue} -> {instanceValue}");

            if ((HasValue(thisValue) && thisValue.Equals(instanceValue))
                | (!HasValue(thisValue) & !HasValue(instanceValue)))
            {
                Log.Debug("value same. skipping.");
                return;
            }

            if (propertyInstance.CanWrite)
            {
                try
                {
                    propertyInstance.SetValue(this, instanceValue);
                    Log.Debug($"set:{propertyInstance.Name}:{thisValue} -> {instanceValue}");
                }
                catch (Exception e)
                {
                    Log.Exception($"exception modifying:{propertyInstance.Name} {thisValue} -> {instanceValue}", e);
                }
            }
            else
            {
                Log.Debug($"property not writable:{propertyInstance.Name}");
            }
        }
    }
}