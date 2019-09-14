// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace CollectSFData
{
    public class IngestionResourcesSnapshot
    {
        public string FailureNotificationsQueue { get; set; } = string.Empty;

        public IList<string> IngestionQueues { get; set; } = new List<string>();

        public string SuccessNotificationsQueue { get; set; } = string.Empty;

        public IList<string> TempStorageContainers { get; set; } = new List<string>();
    }

    public class KustoEndpointInfo : Instance
    {
        private AzureResourceManager _arm = new AzureResourceManager();
        private Http _httpClient = Http.ClientFactory();
        private string _pattern = "https://(?<ingest>ingest-){0,1}(?<clusterName>.+?)\\.(?<location>.+?)\\.(?<domainName>.+?)(/|$)(?<databaseName>.+?){0,1}(/|$)(?<tableName>.+?){0,1}(/|$)";

        public KustoEndpointInfo()
        {
            if (!Config.IsKustoConfigured())
            {
                string errMessage = "kusto not configured";
                Log.Error(errMessage);
                throw new ArgumentNullException(errMessage);
            }

            DeleteSourceOnSuccess = !Config.KustoUseBlobAsSource;

            if (Regex.IsMatch(Config.KustoCluster, _pattern))
            {
                Match matches = Regex.Match(Config.KustoCluster, _pattern);
                string domainName = matches.Groups["domainName"].Value;
                DatabaseName = matches.Groups["databaseName"].Value;
                TableName = Config.KustoTable;
                string ingestPrefix = matches.Groups["ingest"].Value;
                ClusterName = matches.Groups["clusterName"].Value;
                string location = matches.Groups["location"].Value;
                ManagementUrl = $"https://{ClusterName}.{location}.{domainName}";
                ClusterIngestUrl = $"https://{ingestPrefix}{ClusterName}.{location}.{domainName}";
                RestMgmtUri = $"{ClusterIngestUrl}/v1/rest/mgmt";
            }
            else
            {
                string errMessage = $"invalid url. should match pattern {_pattern}";
                Log.Error(errMessage);
                throw new ArgumentException(errMessage);
            }
        }

        public string ClusterIngestUrl { get; set; }

        public object ClusterName { get; private set; }

        public KustoConnectionStringBuilder DatabaseConnection { get; set; }

        public string DatabaseName { get; set; }

        public bool DeleteSourceOnSuccess { get; set; }

        public string IdentityToken { get; private set; }

        public IngestionResourcesSnapshot IngestionResources { get; private set; }

        public string ManagementUrl { get; private set; }

        public string RestMgmtUri { get; private set; }

        public string TableName { get; private set; }
        private KustoConnectionStringBuilder ManagementConnection { get; set; }

        public void Authenticate(bool throwOnError = false, bool prompt = false)
        {
            if (Config.IsKustoConfigured() && _arm.Authenticate(throwOnError, ClusterIngestUrl, prompt ? PromptBehavior.Auto : PromptBehavior.Never))
            {
                DatabaseConnection = new KustoConnectionStringBuilder(ClusterIngestUrl) { FederatedSecurity = true, InitialCatalog = DatabaseName, UserToken = _arm.BearerToken };
                ManagementConnection = new KustoConnectionStringBuilder(ManagementUrl) { FederatedSecurity = true, InitialCatalog = DatabaseName, UserToken = _arm.BearerToken };
            }
            else
            {
                DatabaseConnection = new KustoConnectionStringBuilder(ClusterIngestUrl) { FederatedSecurity = true, InitialCatalog = DatabaseName };
                ManagementConnection = new KustoConnectionStringBuilder(ManagementUrl) { FederatedSecurity = true, InitialCatalog = DatabaseName };
            }

            IdentityToken = RetrieveKustoIdentityToken();
            IngestionResources = RetrieveIngestionResources();
        }

        public bool DropTable(string tableName)
        {
            if (HasTable(tableName))
            {
                Log.Warning($"dropping table: {tableName}");
                return Command($".drop table ['{tableName}'] ifexists skip-seal | project TableName | where TableName == '{tableName}'").Count == 0;
            }

            return true;
        }

        public bool HasTable(string tableName)
        {
            return Query($".show tables | project TableName | where TableName == '{tableName}'").Count > 0;
        }

        public List<string> Query(string query)
        {
            Log.Info($"query:{query}", ConsoleColor.Blue);
            List<string> list = new List<string>();

            using (ICslQueryProvider kustoQueryClient = KustoClientFactory.CreateCslQueryProvider(ManagementConnection))
            {
                return EnumerateResults(kustoQueryClient.ExecuteQuery(query));
            }
        }

        private List<string> Command(string command)
        {
            Log.Info($"command:{command}", ConsoleColor.Blue);

            using (ICslAdminProvider kustoAdminClient = KustoClientFactory.CreateCslAdminProvider(ManagementConnection))
            {
                return EnumerateResults(kustoAdminClient.ExecuteControlCommand(command));
            }
        }

        private List<string> EnumerateResults(IDataReader reader)
        {
            List<string> csvRecords = new List<string>();

            while (reader.Read())
            {
                StringBuilder csvRecord = new StringBuilder();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    csvRecord.Append(reader.GetValue(i) + ",");
                }

                csvRecords.Add(csvRecord.ToString().TrimEnd(','));
            }

            Log.Info($"results:", ConsoleColor.DarkBlue, null, csvRecords);
            return csvRecords;
        }

        private IngestionResourcesSnapshot RetrieveIngestionResources()
        {
            // retrieve ingestion resources (queues and blob containers) with SAS from specified Kusto Ingestion service using supplied Access token
            string requestBody = "{ \"csl\": \".get ingestion resources\" }";

            IngestionResourcesSnapshot ingestionResources = new IngestionResourcesSnapshot();
            _httpClient.SendRequest(RestMgmtUri, _arm.BearerToken, requestBody, HttpMethod.Post);
            JObject responseJson = _httpClient.ResponseStreamJson;

            // input queues
            IEnumerable<JToken> tokens = responseJson.SelectTokens("Tables[0].Rows[?(@.[0] == 'SecuredReadyForAggregationQueue')]");
            foreach (JToken token in tokens)
            {
                ingestionResources.IngestionQueues.Add((string)token.Last);
            }

            // temp storage containers
            tokens = responseJson.SelectTokens("Tables[0].Rows[?(@.[0] == 'TempStorage')]");

            foreach (JToken token in tokens)
            {
                ingestionResources.TempStorageContainers.Add((string)token.Last);
            }

            // failure notifications queue
            JToken singleToken = responseJson.SelectTokens("Tables[0].Rows[?(@.[0] == 'FailedIngestionsQueue')].[1]").FirstOrDefault();
            ingestionResources.FailureNotificationsQueue = (string)singleToken;

            // success notifications queue
            singleToken = responseJson.SelectTokens("Tables[0].Rows[?(@.[0] == 'SuccessfulIngestionsQueue')].[1]").FirstOrDefault();
            ingestionResources.SuccessNotificationsQueue = (string)singleToken;

            Log.Info("ingestion resources:", ingestionResources);
            return ingestionResources;
        }

        private string RetrieveKustoIdentityToken()
        {
            // retrieve kusto identity token that will be added to every ingest message
            string requestBody = "{ \"csl\": \".get kusto identity token\" }";
            string jsonPath = "Tables[0].Rows[*].[0]";

            _httpClient.SendRequest(RestMgmtUri, _arm.BearerToken, requestBody, HttpMethod.Post);
            JToken identityToken = _httpClient.ResponseStreamJson.SelectTokens(jsonPath).FirstOrDefault();

            Log.Info("identityToken:", identityToken);
            return ((string)identityToken);
        }
    }
}