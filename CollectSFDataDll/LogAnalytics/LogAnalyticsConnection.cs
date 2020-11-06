// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Azure;
using CollectSFData.Common;
using CollectSFData.DataFile;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CollectSFData.LogAnalytics
{
    public class LogAnalyticsConnection : Instance
    {
        private readonly AzureResourceManager _arm = new AzureResourceManager();
        private string _armAuthResource = "https://management.core.windows.net";
        private readonly AzureResourceManager _laArm = new AzureResourceManager();
        private LogAnalyticsWorkspaceModel _currentWorkspaceModelModel = new LogAnalyticsWorkspaceModel();
        private Http _httpClient = Http.ClientFactory();
        private SynchronizedList<string> _ingestedUris = new SynchronizedList<string>();
        private string _logAnalyticsApiVer = "api-version=2020-08-01";
        private string _logAnalyticsAuthResource = "https://api.loganalytics.io";
        private string _logAnalyticsCustomLogSuffix = ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";
        private string _logAnalyticsQueryEndpoint = "https://api.loganalytics.io/v1/workspaces/";
        private string _timeStampField = "";

        private LogAnalyticsWorkspaceRecordResult CurrentWorkspace { get; set; }

        public void AddFile(FileObject fileObject)
        {
            Log.Debug("enter");

            if (!CanIngest(fileObject.RelativeUri))
            {
                Log.Warning($"file already ingested. skipping: {fileObject.RelativeUri}");
                return;
            }

            ImportJson(FileMgr.ProcessFile(fileObject));
        }

        public bool Connect()
        {
            if (Config.LogAnalyticsCreate | Config.LogAnalyticsRecreate | Config.Unique)
            {
                Authenticate();
                GetCurrentWorkspace();

                if (Config.LogAnalyticsRecreate)
                {
                    return RecreateWorkspace();
                }

                if (Config.LogAnalyticsCreate)
                {
                    return CreateWorkspace();
                }

                if (Config.Unique)
                {
                    _ingestedUris.AddRange(PostQueryList($"['{Config.LogAnalyticsName}_CL']|distinct RelativeUri_s"));
                    Log.Info($"listResults:", _ingestedUris);
                }
            }

            // send empty object to check connection
            FileObject fileObject = new FileObject();
            fileObject.Stream.Set(Encoding.UTF8.GetBytes("{}"));
            Log.Info($"Checking connection to log analytics workspace {Config.LogAnalyticsId} ", ConsoleColor.Green);

            if (!PostData(fileObject, true))
            {
                return false;
            }

            if (Config.IsLogAnalyticsPurgeRequested())
            {
                _arm.Authenticate();
                Purge();
                return false;
            }

            return true;
        }

        public void ImportJson(FileObjectCollection fileObjectCollection)
        {
            fileObjectCollection.ForEach(x => ImportJson(x));
        }

        public void ImportJson(FileObject fileObject)
        {
            int retry = 0;

            if (fileObject.Stream.Get().Length < 1 && (!fileObject.FileUri.ToLower().EndsWith(JsonExtension) | !fileObject.Exists))
            {
                Log.Warning($"no json data to send: {fileObject.FileUri}");
                return;
            }

            while (!PostData(fileObject) & retry++ < RetryCount)
            {
                Log.Error($"error importing: {fileObject.FileUri} retry:{retry}");

                if (retry == 1 && Config.LogDebug)
                {
                    File.WriteAllBytes(fileObject.FileUri, fileObject.Stream.Get().ToArray());
                    Log.Error($"json saved to {fileObject.FileUri}");
                }

                TotalErrors++;
            }
        }

        public bool Purge()
        {
            /*
                to clean workspaceModel, have to use rest and purge
                has a 30 day sla!
                informal testing was around 7 days
                faster / easier to delete / recreate workspaceModel
                https://docs.microsoft.com/en-us/azure/azure-monitor/platform/personal-data-mgmt#delete
                https://docs.microsoft.com/en-us/rest/api/loganalytics/workspaces%202015-03-20/purge

                While we expect the vast majority of purge operations to complete much quicker than our SLA,
                 due to their heavy impact on the data platform used by Log Analytics, the formal SLA for the completion of purge operations is set at 30 days.
            */

            Http http = default(Http);

            if (Config.LogAnalyticsPurge.ToLower() == "true")
            {
                string purgeUrl = $"{ManagementAzureCom}{CurrentWorkspace.id}/purge?{_logAnalyticsApiVer}"; //api-version=2015-03-20";

                Log.Warning($"deleting data for 'LogAnalyticsName':{Config.LogAnalyticsName}");
                Log.Warning("Ctrl-C now if this is incorrect!");
                Thread.Sleep(ThreadSleepMsWarning);

                LogAnalyticsPurge logAnalyticsPurge = new LogAnalyticsPurge()
                {
                    table = $"{Config.LogAnalyticsName}_CL", //"Usage",
                    filters = new[]
                    {
                        new LogAnalyticsPurge.Filters()
                        {
                            column = "TimeGenerated",
                            @operator = "<",
                            value = DateTime.Now.ToString("o")
                        }
                    }
                };

                http = _arm.SendRequest(purgeUrl, HttpMethod.Post, JsonConvert.SerializeObject(logAnalyticsPurge));
                string purgeStatusUrl = http.Response.Headers.GetValues("x-ms-status-location").First();
                int count = 0;

                if (!string.IsNullOrEmpty(purgeStatusUrl))
                {
                    while (count < RetryCount)
                    {
                        http = _arm.SendRequest(purgeStatusUrl);
                        if (http.ResponseStreamJson.GetValue("status").ToString() == "completed")
                        {
                            Log.Info("complete", ConsoleColor.Green);
                            break;
                        }

                        count++;
                        Log.Info($"iteration: {count}");
                        Thread.Sleep(ThreadSleepMs10000);
                    }
                }

                Log.Info("this may take a while as documented online for Log Analytics Purge", ConsoleColor.Green, ConsoleColor.DarkGray);
                Log.Info($"use this url from ('x-ms-status-location') response header to check status of purge:\r\n{purgeStatusUrl}", ConsoleColor.Green, ConsoleColor.DarkGray);
            }
            else
            {
                // check status of purge
                Log.Info("checking status of purge request", ConsoleColor.Green);
                http = _arm.SendRequest(Config.LogAnalyticsPurge);
            }

            Log.Info("response:", http.Success);

            return true;
        }

        private string BuildSignature(string datestring, byte[] jsonBytes)
        {
            if (jsonBytes.Length > MaxJsonTransmitBytes | jsonBytes.Length == 0)
            {
                string errMessage = $"json size too large to send or 0 bytes. max bytes that can be sent: {MaxJsonTransmitBytes} current json bytes: {jsonBytes.Length}";
                Log.Exception(errMessage);
                throw new ArgumentOutOfRangeException(errMessage);
            }

            string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
            Log.Debug($"stringToHash:{stringToHash}");
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(Config.LogAnalyticsKey);
            byte[] messageBytes = encoding.GetBytes(stringToHash);

            using (HMACSHA256 hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                string signature = "SharedKey " + Config.LogAnalyticsId + ":" + Convert.ToBase64String(hash);
                Log.Debug($"signature:{signature}");
                return signature;
            }
        }

        private bool CanIngest(string relativeUri)
        {
            if (!Config.Unique)
            {
                return true;
            }

            string cleanUri = Regex.Replace(relativeUri, $"\\.?\\d*?({ZipExtension}|{TableExtension})", "");
            return !_ingestedUris.Any(x => x.Contains(cleanUri));
        }

        private bool CreateWorkspace(LogAnalyticsWorkspaceModel workspaceModel = null)
        {
            Log.Info("enter");
            bool response = false;

            // create workspaceModel
            Log.Info($"creating workspaceModel {Config.LogAnalyticsWorkspaceName}");

            if (Config.LogAnalyticsCreate)
            {
                _arm.CreateResourceGroup($"/subscriptions/{Config.AzureSubscriptionId}/resourcegroups/{Config.AzureResourceGroup}", Config.AzureResourceGroupLocation);
            }

            string resourceId = $"/subscriptions/{Config.AzureSubscriptionId}/resourcegroups/{Config.AzureResourceGroup}/providers/Microsoft.OperationalInsights/workspaces/{Config.LogAnalyticsWorkspaceName}";

            if (workspaceModel == null)
            {
                Log.Info("creating default workspaceModel");
                workspaceModel = new LogAnalyticsWorkspaceModel()
                {
                    location = Config.AzureResourceGroupLocation,
                    properties = new LogAnalyticsWorkspaceModel.Properties()
                    {
                        retentionInDays = Config.LogAnalyticsWorkspaceSku.ToLower() == "free" ? 7 : 30,
                        source = "Azure",
                        sku = new LogAnalyticsWorkspaceModel.Properties.Sku()
                        {
                            name = Config.LogAnalyticsWorkspaceSku
                        }
                    }
                };
            }

            Http http = _arm.ProvisionResource(resourceId, JsonConvert.SerializeObject(workspaceModel), _logAnalyticsApiVer);

            if (!http.Success)
            {
                return false;
            }

            response = GetCurrentWorkspace(JsonConvert.DeserializeObject<LogAnalyticsWorkspaceRecordResult>(http.ResponseStreamString).properties.customerId);

            // set new key
            Config.LogAnalyticsKey = GetWorkspacePrimaryKey();
            return response;
        }

        private bool DeleteWorkspace()
        {
            Log.Info("enter");
            Log.Warning($"deleting workspaceModel {CurrentWorkspace.id}");
            Log.Warning("Ctrl-C now if this is incorrect!");
            Thread.Sleep(ThreadSleepMsWarning);

            // delete workspaceModel
            string url = $"{ManagementAzureCom}{CurrentWorkspace.id}?{_logAnalyticsApiVer}";
            return _arm.SendRequest(url, HttpMethod.Delete).Success;
        }

        private bool GetCurrentWorkspace(string workspaceId = null)
        {
            Log.Info("enter");
            string url = $"{ManagementAzureCom}/subscriptions/{Config.AzureSubscriptionId}/providers/Microsoft.OperationalInsights/workspaces?{_logAnalyticsApiVer}";
            Http http = _arm.SendRequest(url);
            workspaceId = workspaceId ?? Config.LogAnalyticsId;

            LogAnalyticsWorkspaceRecordResult[] workspaces = (JsonConvert.DeserializeObject<LogAnalyticsWorkspaceRecordResults>(http.ResponseStreamString)).value.ToArray();
            CurrentWorkspace = workspaces.FirstOrDefault(x => x.properties.customerId == workspaceId);

            Log.Info("current workspaceModel:", ConsoleColor.Green, null, CurrentWorkspace);
            if (CurrentWorkspace != null)
            {
                Config.AzureResourceGroupLocation = Config.AzureResourceGroupLocation ?? CurrentWorkspace.location;
                Config.LogAnalyticsWorkspaceName = Config.LogAnalyticsWorkspaceName ?? CurrentWorkspace.name;
                Config.LogAnalyticsId = CurrentWorkspace.properties.customerId;
                _currentWorkspaceModelModel = GetCurrentWorkspaceModel();
            }

            return http.Success & CurrentWorkspace != null;
        }

        private LogAnalyticsWorkspaceModel GetCurrentWorkspaceModel()
        {
            LogAnalyticsWorkspaceModel currentWorkspaceModel = new LogAnalyticsWorkspaceModel();

            if (ParseWorkspaceResourceId(CurrentWorkspace.id))
            {
                currentWorkspaceModel.location = CurrentWorkspace.location;
                currentWorkspaceModel.properties = new LogAnalyticsWorkspaceModel.Properties()
                {
                    retentionInDays = CurrentWorkspace.properties.retentionInDays,
                    sku = new LogAnalyticsWorkspaceModel.Properties.Sku()
                    {
                        name = CurrentWorkspace.properties.sku.name
                    }
                };
            }
            else
            {
                Log.Error("unable to get current workspaceModel model");
            }

            return currentWorkspaceModel;
        }

        private string GetWorkspacePrimaryKey()
        {
            string url = $"{ManagementAzureCom}{CurrentWorkspace.id}/sharedKeys?{_logAnalyticsApiVer}";
            Http http = _arm.SendRequest(url, HttpMethod.Post);

            if (http.Success)
            {
                return http.ResponseStreamJson["primarySharedKey"].ToString();
            }

            return null;
        }

        private bool ParseWorkspaceResourceId(string workspaceResourceId)
        {
            Log.Info("enter");
            // should be in form of workspaceModel resource id
            // to capture subscription, resourcegroup, and workspaceModel name
            string pattern = @"/subscriptions/(?<subscriptionId>.+?)/resourcegroups/(?<resourceGroup>.+?)/.+/workspaces/(?<workspaceName>.+?)(/|\?|$)";

            if (Regex.IsMatch(workspaceResourceId, pattern))
            {
                Match match = Regex.Match(workspaceResourceId, pattern);
                Config.AzureSubscriptionId = Config.AzureSubscriptionId ?? match.Groups["subscriptionId"].Value;
                Config.AzureResourceGroup = Config.AzureResourceGroup ?? match.Groups["resourceGroup"].Value;
                Config.LogAnalyticsWorkspaceName = Config.LogAnalyticsWorkspaceName ?? match.Groups["workspaceName"].Value;
                return true;
            }

            Log.Warning($"unable to parse resourceId: {workspaceResourceId} with pattern: {pattern}");
            return false;
        }

        private bool PostData(FileObject fileObject, bool connectivityCheck = false)
        {
            Log.Debug("enter");
            string jsonBody = Config.UseMemoryStream || connectivityCheck ? new StreamReader(fileObject.Stream.Get()).ReadToEnd() : File.ReadAllText(fileObject.FileUri);
            fileObject.Stream.Close();
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonBody);

            string date = DateTime.UtcNow.ToString("r");
            string signature = BuildSignature(date, jsonBytes);
            Log.Info($"signature length: {signature.Length} date: {date} json length: {jsonBytes.Length}\r\n file: {Path.GetFileName(fileObject.FileUri)}", ConsoleColor.Magenta);

            try
            {
                string uri = "https://" + Config.LogAnalyticsId + _logAnalyticsCustomLogSuffix;
                Log.Debug($"post uri:{uri}");
                Dictionary<string, string> headers = new Dictionary<string, string>();

                // send empty log name for connectivity check
                // successful connect will respond with badrequest
                headers.Add("Log-Type", connectivityCheck ? string.Empty : Config.LogAnalyticsName);
                headers.Add("Authorization", signature);
                headers.Add("x-ms-date", date);
                headers.Add("time-generated-field", _timeStampField);

                _httpClient.SendRequest(
                    uri: uri,
                    jsonBody: jsonBody,
                    httpMethod: HttpMethod.Post,
                    headers: headers,
                    okStatus: connectivityCheck ? HttpStatusCode.BadRequest : HttpStatusCode.OK);

                Log.Info($"{_httpClient.Response?.ReasonPhrase}");

                if (_httpClient.Success || (connectivityCheck && _httpClient.StatusCode == HttpStatusCode.BadRequest))
                {
                    return true;
                }

                Log.Error("unsuccessful response:", _httpClient.Response);
                return false;
            }
            catch (Exception e)
            {
                Log.Exception($"post exception:{e}");
                return false;
            }
        }

        private LogAnalyticsQueryResults PostQuery(string query)
        {
            LogAnalyticsQueryResults laResults = new LogAnalyticsQueryResults();
            string jsonBody = $"{{\"query\": \"{query}\"}}";

            try
            {
                string uri = $"{_logAnalyticsQueryEndpoint}{Config.LogAnalyticsId}/query";
                Log.Info($"post uri:{uri}", ConsoleColor.Blue, null, query);

                Authenticate();
                _httpClient.SendRequest(
                            uri: uri,
                            authToken: _laArm.BearerToken,
                            jsonBody: jsonBody,
                            httpMethod: HttpMethod.Post);

                Log.Info($"{_httpClient.Response?.ReasonPhrase}");

                if (!_httpClient.Success)
                {
                    Log.Error("unsuccessful response:", _httpClient.Response);
                    return null;
                }
                else
                {
                    laResults = JsonConvert.DeserializeObject<LogAnalyticsQueryResults>(_httpClient.ResponseStreamString);
                    Log.Info($"result rows count:{laResults?.tables?[0].rows?.Length}", ConsoleColor.DarkBlue);
                    Log.Debug($"result:", laResults);
                }

                return laResults;
            }
            catch (Exception e)
            {
                Log.Exception($"post exception:{e}");
                return null;
            }
        }

        private void Authenticate()
        {
            if (!_arm.IsAuthenticated)
            {
                _arm.Scopes = new List<string>() { $"{_armAuthResource}//user_impersonation" };

                if (Config.IsClientIdConfigured())
                {
                    _arm.Scopes = new List<string>() { $"{_armAuthResource}//.default" };
                }

                _arm.Authenticate(false, _armAuthResource);
            }

            if (!_laArm.IsAuthenticated)
            {
                _laArm.Scopes = new List<string>() { $"{_logAnalyticsAuthResource}//user_impersonation" };

                if (Config.IsClientIdConfigured())
                {
                    _laArm.Scopes = new List<string>() { $"{_logAnalyticsAuthResource}//.default" };
                }

                _laArm.Authenticate(true, _logAnalyticsAuthResource);
            }
        }

        private List<string> PostQueryList(string query)
        {
            LogAnalyticsQueryResults results = PostQuery(query);
            List<string> listResults = new List<string>();

            if (results?.tables.Length > 0)
            {
                foreach (object[] rowFields in results.tables[0].rows)
                {
                    listResults.Add(string.Join(",", rowFields));
                }
            }

            Log.Info($"list results:", ConsoleColor.DarkBlue, null, listResults);
            return listResults;
        }

        private bool RecreateWorkspace()
        {
            if (DeleteWorkspace() && CreateWorkspace(_currentWorkspaceModelModel))
            {
                Log.Info($"workspaceModel {CurrentWorkspace.id} recreated.");
                return true;
            }

            Log.Error($"error creating workspaceModel: {Config.LogAnalyticsName}");
            return false;
        }
    }
}