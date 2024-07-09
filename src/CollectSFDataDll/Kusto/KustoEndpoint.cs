// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Azure;
using CollectSFData.Common;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CollectSFData.Kusto
{
    public class KustoEndpoint
    {
        private static ICslAdminProvider _adminClient;
        private static ICslAdminProvider _adminIngestClient;
        private static int _maxKustoClientTimeMs = 300 * 1000;
        private static ICslQueryProvider _queryClient;
        private readonly CustomTaskManager _kustoTasks = new CustomTaskManager();
        private AzureResourceManager _arm;
        private ConfigurationOptions _config;
        public string ClusterIngestUrl { get; set; }
        public string ClusterName { get; private set; }
        public string DatabaseName { get; set; }
        public string HostName { get; private set; }
        public string IdentityToken { get; private set; }
        public KustoConnectionStringBuilder IngestConnection { get; set; }
        public IngestionResourcesSnapshot IngestionResources { get; set; }
        public bool LogLargeResults { get; set; } = true;
        public KustoConnectionStringBuilder ManagementConnection { get; set; }
        public string ManagementUrl { get; private set; }
        public string RestMgmtUri { get; private set; }
        public string RestQueryUri { get; private set; }
        public string TableName { get; private set; }
        private Timer _adminIngestTimer { get; set; }
        private Timer _adminTimer { get; set; }
        private Timer _queryTimer { get; set; }

        public KustoEndpoint(ConfigurationOptions config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _arm = new AzureResourceManager(_config);

            if (!_config.IsKustoConfigured())
            {
                string errMessage = "kusto not configured";
                Log.Error(errMessage);
                throw new ArgumentNullException(errMessage);
            }

            if (Regex.IsMatch(_config.KustoCluster, Constants.KustoUrlPattern))
            {
                Match matches = Regex.Match(_config.KustoCluster, Constants.KustoUrlPattern);
                string domainName = matches.Groups["domainName"].Value;
                DatabaseName = matches.Groups["databaseName"].Value;
                TableName = _config.KustoTable;
                string ingestPrefix = matches.Groups["ingest"].Value;
                ClusterName = matches.Groups["clusterName"].Value;
                string location = matches.Groups["location"].Value;
                HostName = $"{ClusterName}.{location}.{domainName}";
                ManagementUrl = $"https://{HostName}";
                ClusterIngestUrl = $"https://{ingestPrefix}{ClusterName}.{location}.{domainName}";
                RestMgmtUri = $"{ClusterIngestUrl}/v1/rest/mgmt";
                RestQueryUri = $"{ManagementUrl}/v1/rest/query";
            }
            else if (Regex.IsMatch(_config.KustoCluster, Constants.LocalWebServerPattern))
            {
                Match matches = Regex.Match(_config.KustoCluster, Constants.LocalWebServerPattern);
                DatabaseName = matches.Groups["databaseName"].Value;
                TableName = _config.KustoTable;
                ClusterName = _config.KustoCluster;
                ManagementUrl = ClusterName;
                ClusterIngestUrl = ClusterName;
            }
            else
            {
                string errMessage = $"invalid kusto url.";
                Log.Error(errMessage);
                throw new ArgumentException(errMessage);
            }
        }

        public void Authenticate(bool throwOnError = false)
        {
            _arm.Scopes = new List<string>() { $"{ClusterIngestUrl}/user_impersonation" };

            if (_config.IsClientIdConfigured())
            {
                _arm.Scopes = new List<string>() { $"{ClusterIngestUrl}/.default" };
            }

            if (_config.IsKustoConfigured() && _config.IsARMValid && _arm.Authenticate(throwOnError, ClusterIngestUrl))
            {
                if (_arm.ClientIdentity.IsAppRegistration)
                {
                    Log.Info($"connecting to kusto with app registration {_config.AzureClientId}");

                    IngestConnection = new KustoConnectionStringBuilder(ClusterIngestUrl)
                    {
                        FederatedSecurity = true,
                        InitialCatalog = DatabaseName,
                        ApplicationClientId = _config.AzureClientId,
                        ApplicationCertificateBlob = _config.ClientCertificate,
                        Authority = _config.AzureTenantId,
                        ApplicationCertificateSendX5c = true
                    };

                    ManagementConnection = new KustoConnectionStringBuilder(ManagementUrl)
                    {
                        FederatedSecurity = true,
                        InitialCatalog = DatabaseName,
                        ApplicationClientId = _config.AzureClientId,
                        ApplicationCertificateBlob = _config.ClientCertificate,
                        Authority = _config.AzureTenantId,
                        ApplicationCertificateSendX5c = true
                    };
                }
                else
                {
                    IngestConnection = new KustoConnectionStringBuilder(ClusterIngestUrl)
                    {
                        FederatedSecurity = true,
                        InitialCatalog = DatabaseName,
                        Authority = _config.AzureTenantId,
                        UserToken = _arm.BearerToken
                    };

                    ManagementConnection = new KustoConnectionStringBuilder(ManagementUrl)
                    {
                        FederatedSecurity = true,
                        InitialCatalog = DatabaseName,
                        Authority = _config.AzureTenantId,
                        UserToken = _arm.BearerToken
                    };
                }
            }
            else if (Regex.IsMatch(_config.KustoCluster, Constants.KustoUrlPattern))
            {
                // use federated security to connect to kusto directly and use kusto identity token instead of arm token
                IngestConnection = new KustoConnectionStringBuilder(ClusterIngestUrl)
                {
                    FederatedSecurity = true,
                    InitialCatalog = DatabaseName,
                    Authority = _config.AzureTenantId
                };

                ManagementConnection = new KustoConnectionStringBuilder(ManagementUrl)
                {
                    FederatedSecurity = true,
                    InitialCatalog = DatabaseName,
                    Authority = _config.AzureTenantId
                };
            }
            else
            {
                // set up connection to localhost server
                IngestConnection = new KustoConnectionStringBuilder(ClusterIngestUrl)
                {
                    InitialCatalog = DatabaseName,
                };

                ManagementConnection = new KustoConnectionStringBuilder(ManagementUrl)
                {
                    InitialCatalog = DatabaseName,
                };
            }

            if (!_config.IsIngestionLocal)
            {
                IdentityToken = RetrieveKustoIdentityToken();
                IngestionResources = RetrieveIngestionResources();
            }
        }

        public async Task<List<string>> CommandAsync(string command)
        {
            Log.Info($"command:{command}", ConsoleColor.Blue);
            return await _kustoTasks.TaskFunction((responseList) =>
            {
                ICslAdminProvider adminClient = CreateAdminClient();
                List<string> results = EnumerateResultsCsv(adminClient.ExecuteControlCommand(command));
                return results;
            }) as List<string>;
        }

        public ICslAdminProvider CreateAdminClient()
        {
            if (_adminClient == null)
            {
                _adminClient = KustoClientFactory.CreateCslAdminProvider(ManagementConnection);
            }

            if (_adminTimer == null)
            {
                _adminTimer = new Timer(DisposeAdminClient, null, _maxKustoClientTimeMs, _maxKustoClientTimeMs);
            }

            _adminTimer.Change(_maxKustoClientTimeMs, _maxKustoClientTimeMs);
            return _adminClient;
        }

        public ICslAdminProvider CreateAdminIngestClient()
        {
            if (_adminIngestClient == null)
            {
                _adminIngestClient = KustoClientFactory.CreateCslAdminProvider(IngestConnection);
            }

            if (_adminIngestTimer == null)
            {
                _adminIngestTimer = new Timer(DisposeAdminIngestClient, null, _maxKustoClientTimeMs, _maxKustoClientTimeMs);
            }

            _adminIngestTimer.Change(_maxKustoClientTimeMs, _maxKustoClientTimeMs);
            return _adminIngestClient;
        }

        public ICslQueryProvider CreateQueryClient()
        {
            if (_queryClient == null)
            {
                _queryClient = KustoClientFactory.CreateCslQueryProvider(ManagementConnection);
            }

            if (_queryTimer == null)
            {
                _queryTimer = new Timer(DisposeQueryClient, null, _maxKustoClientTimeMs, _maxKustoClientTimeMs);
            }

            _queryTimer.Change(_maxKustoClientTimeMs, _maxKustoClientTimeMs);
            return _queryClient;
        }

        public bool CreateTable(string tableName, string tableSchema)
        {
            if (!HasTable(tableName))
            {
                // string.Format("@'{0}',@'{1}'", $"c:\\kustodata\\dbs\\{Endpoint.DatabaseName}\\md", $"c:\\kustodata\\dbs\\{Endpoint.DatabaseName}\\data")
                Log.Info($"creating table: {tableName}");
                return CommandAsync($".create table ['{tableName}'] ( {tableSchema} )").Result.Count > 0;
            }

            return true;
        }

        public bool CreateDatabase(string databaseName, string databaseLocation)
        {
            if (!HasDatabase(databaseName))
            {
                Log.Info($"creating database: {databaseName}");
                return CommandAsync($".create database {databaseName} persist ( {databaseLocation} )").Result.Count > 0;
            }
            return true;
        }

        public bool DropTable(string tableName)
        {
            if (HasTable(tableName))
            {
                Log.Warning($"dropping table: {tableName}");
                return CommandAsync($".drop table ['{tableName}'] ifexists skip-seal | project TableName | where TableName == '{tableName}'").Result.Count == 0;
            }

            return true;
        }

        public string GetCursor()
        {
            string cursor = QueryAsCsvAsync("print Cursor=current_cursor()").Result.FirstOrDefault();
            Log.Info($"returning cursor: {cursor}");
            return cursor;
        }

        public bool HasDatabase(string databaseName)
        {
            bool result = QueryAsCsvAsync($".show databases | project DatabaseName | where DatabaseName == '{databaseName}'").Result.Count() > 0;
            return result;
        }

        public bool HasTable(string tableName)
        {
            return QueryAsCsvAsync($".show tables | project TableName | where TableName == '{tableName}'").Result.Count > 0;
        }

        public bool IngestInlineWithMapping(string tableName, string mapping, string stream)
        {
            return CommandAsync($".ingest inline into table ['{tableName}'] with (format='csv', ingestionMapping = '{mapping}') <| {stream}").Result.Count > 0;
        }

        public bool IngestInline(string tableName, string csv)
        {
            Log.Info($"inline ingesting data: {csv} into table: {tableName}");
            return CommandAsync($".ingest inline into table ['{tableName}'] <| {csv}").Result.Count > 0;
        }

        public async Task<List<string>> QueryAsCsvAsync(string query)
        {
            Log.Info($"query:{query}", ConsoleColor.Blue);

            try
            {
                List<string> response = await _kustoTasks.TaskFunction((responseList) =>
                {
                    ICslQueryProvider queryClient = CreateQueryClient();
                    IDataReader reader = queryClient.ExecuteQuery(query);

                    if (reader == null)
                    {
                        Log.Info($"no results:", ConsoleColor.DarkBlue);
                        return new List<string>();
                    }

                    List<string> results = EnumerateResultsCsv(reader);
                    reader.Close();
                    return results;
                }) as List<string>;

                return response;
            }
            catch (Exception e)
            {
                Log.Exception($"exception executing query: {query}\r\n{e}");
                return new List<string>();
            }
        }

        public async Task<KustoRestRecords> QueryAsListAsync(string query)
        {
            Log.Info($"query:{query}", ConsoleColor.Blue);
            KustoRestRecords response = new KustoRestRecords();

            try
            {
                response = await _kustoTasks.TaskFunction((responseList) =>
                {
                    ICslQueryProvider queryClient = CreateQueryClient();
                    IDataReader reader = queryClient.ExecuteQuery(query);

                    if (reader == null)
                    {
                        Log.Info($"no results:", ConsoleColor.DarkBlue);
                        return null;
                    }

                    KustoRestRecords results = EnumerateResultsList(reader);
                    reader.Close();
                    return results;
                }) as KustoRestRecords;

                return response;
            }
            catch (Exception e)
            {
                Log.Exception($"exception executing query: {query}\r\n{e}");
                return null;
            }
        }

        private static void DisposeAdminClient(object state)
        {
            if (_adminClient != null)
            {
                Log.Info("Disposing kusto admin client");
                _adminClient.Dispose();
                _adminClient = null;
            }
        }

        private static void DisposeAdminIngestClient(object state)
        {
            if (_adminIngestClient != null)
            {
                Log.Info("Disposing kusto admin ingest client");
                _adminIngestClient.Dispose();
                _adminIngestClient = null;
            }
        }

        private static void DisposeQueryClient(object state)
        {
            if (_queryClient != null)
            {
                Log.Info("Disposing kusto query client");
                _queryClient.Dispose();
                _queryClient = null;
            }
        }

        private KustoRestTable CreateResponseTable(IDataReader reader)
        {
            KustoRestTable test = new KustoRestTable(reader);
            return test;
        }

        private List<string> EnumerateResultsCsv(IDataReader reader)
        {
            int maxRecords = 1000;
            int index = 0;
            KustoRestTable dataTable = CreateResponseTable(reader);
            List<string> csvRecords = dataTable.RecordsCsv();

            if (csvRecords.Count < maxRecords | LogLargeResults)
            {
                while (index < csvRecords.Count)
                {
                    Log.Info($"results:", ConsoleColor.DarkBlue, null, csvRecords.GetRange(index, Math.Min(maxRecords, csvRecords.Count - index)));
                    index = Math.Min(index += maxRecords, csvRecords.Count);
                }
            }
            else
            {
                Log.Info($"results: {csvRecords.Count}");
            }

            return csvRecords;
        }

        private KustoRestRecords EnumerateResultsList(IDataReader reader)
        {
            int maxRecords = 1000;
            string jsonRecords = string.Empty;
            KustoRestTable dataTable = CreateResponseTable(reader);
            KustoRestRecords dictionary = dataTable.RecordsList();

            if (dataTable.Rows.Count < maxRecords | LogLargeResults)
            {
                Log.Info($"results:\r\n{jsonRecords}", ConsoleColor.DarkBlue, null, dataTable);
            }
            else
            {
                Log.Info($"results: {dataTable.Rows.Count}");
            }

            return dictionary;
        }

        private IngestionResourcesSnapshot RetrieveIngestionResources()
        {
            string command = CslCommandGenerator.GenerateIngestionResourcesGetCommand();
            ICslAdminProvider ingestClient = CreateAdminIngestClient();
            IngestionResourcesSnapshot ingestionResources = new IngestionResourcesSnapshot();
            IEnumerable<string> results = EnumerateResultsCsv(ingestClient.ExecuteControlCommand(command));

            foreach (string result in results)
            {
                if (string.IsNullOrEmpty(result) || !result.Contains(','))
                {
                    Log.Warning($"invalid ingestion resource: {result}");
                    continue;
                }
                string propertyName = result.Split(',')[0];
                string propertyValue = result.Split(',')[1];

                if (propertyName.Equals("SecuredReadyForAggregationQueue"))
                {
                    ingestionResources.IngestionQueues.Add(propertyValue);
                }
                else if (propertyName.Equals("TempStorage"))
                {
                    ingestionResources.TempStorageContainers.Add(propertyValue);
                }
                else if (propertyName.Equals("FailedIngestionsQueue"))
                {
                    ingestionResources.FailureNotificationsQueue = propertyValue;
                }
                else if (propertyName.Equals("SuccessfulIngestionsQueue"))
                {
                    ingestionResources.SuccessNotificationsQueue = propertyValue;
                }
            }

            Log.Info("ingestion resources:", ingestionResources);
            return ingestionResources;
        }

        private string RetrieveKustoIdentityToken()
        {
            // retrieve kusto identity token that will be added to every ingest message
            string command = CslCommandGenerator.GenerateKustoIdentityTokenGetCommand();
            ICslAdminProvider ingestClient = CreateAdminIngestClient();
            string identityToken = EnumerateResultsCsv(ingestClient.ExecuteControlCommand(command)).FirstOrDefault();

            Log.Info($"identityToken:{identityToken}");
            return identityToken;
        } 
    }
}