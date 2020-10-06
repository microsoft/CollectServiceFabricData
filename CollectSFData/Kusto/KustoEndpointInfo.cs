// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Azure;
using CollectSFData.Common;
using Kusto.Cloud.Platform.Utils;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Kusto.Data.Results;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CollectSFData.Kusto
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
        private static ICslAdminProvider _kustoAdminClient;
        private static ICslQueryProvider _kustoQueryClient;
        private static int maxKustoClientTimeMs = 300 * 1000;
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
                HostName = $"{ClusterName}.{location}.{domainName}";
                ManagementUrl = $"https://{HostName}";
                ClusterIngestUrl = $"https://{ingestPrefix}{ClusterName}.{location}.{domainName}";
                RestMgmtUri = $"{ClusterIngestUrl}/v1/rest/mgmt";
                RestQueryUri = $"{ManagementUrl}/v1/rest/query";
            }
            else
            {
                string errMessage = $"invalid url. should match pattern {_pattern}";
                Log.Error(errMessage);
                throw new ArgumentException(errMessage);
            }
        }

        public string ClusterIngestUrl { get; set; }
        public string ClusterName { get; private set; }
        public string Cursor { get; set; } = "''";
        public KustoConnectionStringBuilder DatabaseConnection { get; set; }
        public string DatabaseName { get; set; }
        public bool DeleteSourceOnSuccess { get; set; }
        public KustoRestTable ExtendedPropertiesTable { get; private set; } = new KustoRestTable();
        public List<string> ExtendedResults { get; private set; }
        public string HostName { get; private set; }
        public string IdentityToken { get; private set; }
        public IngestionResourcesSnapshot IngestionResources { get; private set; }
        public bool LogLargeResults { get; set; } = true;
        public string ManagementUrl { get; private set; }
        public KustoRestTable PrimaryResultTable { get; private set; } = new KustoRestTable();
        public KustoRestResponseV1 ResponseDataSet { get; private set; } = new KustoRestResponseV1();
        public string RestMgmtUri { get; private set; }
        public string RestQueryUri { get; private set; }
        public string TableName { get; private set; }
        public KustoRestTableOfContentsV1 TableOfContents { get; private set; } = new KustoRestTableOfContentsV1();
        private Timer adminTimer { get; set; } // = new Timer(DisposeClient, kustoAdminClient, maxKustoClientTimeMs, maxKustoClientTimeMs);
        private KustoConnectionStringBuilder ManagementConnection { get; set; }
        private Timer queryTimer { get; set; } // = new Timer(DisposeClient, kustoQueryClient, maxKustoClientTimeMs, maxKustoClientTimeMs);

        public void Authenticate(bool throwOnError = false, bool prompt = false)
        {
            _arm.Scopes = new List<string>() { $"{ClusterIngestUrl}/kusto.read", $"{ClusterIngestUrl}/kusto.write" };

            if (Config.IsKustoConfigured() && _arm.Authenticate(throwOnError, ClusterIngestUrl, prompt))
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

        public bool CreateTable(string tableName, string tableSchema)
        {
            if (!HasTable(tableName))
            {
                Log.Info($"creating table: {tableName}");
                return Command($".create table ['{tableName}'] ( {tableSchema} )").Count > 0;
            }

            return true;
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

        public bool IngestInline(string tableName, string csv)
        {
            Log.Info($"inline ingesting data: {csv} into table: {tableName}");
            return Command($".ingest inline into table ['{tableName}'] <| {csv}").Count > 0;
        }

        public List<string> Query(string query)
        {
            Log.Info($"query:{query}", ConsoleColor.Blue);

            if (_kustoQueryClient == null)
            {
                _kustoQueryClient = KustoClientFactory.CreateCslQueryProvider(ManagementConnection);
                queryTimer = new Timer(DisposeQueryClient, null, maxKustoClientTimeMs, maxKustoClientTimeMs);
            }

            try
            {
                queryTimer.Change(maxKustoClientTimeMs, maxKustoClientTimeMs);
                // unable to parse multiple tables v1 or v2 using kusto so using httpclient and rest
                string requestBody = "{ \"db\": \"" + DatabaseName + "\", \"csl\": \"" + query + "\" }";
                string requestId = new Guid().ToString();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("accept", "application/json");
                headers.Add("host", HostName);
                headers.Add("x-ms-client-request-id", requestId);

                Log.Info($"query:", requestBody);
                _httpClient.DisplayResponse = Config.LogDebug;
                _httpClient.SendRequest(uri: RestQueryUri, authToken: _arm.BearerToken, jsonBody: requestBody, httpMethod: HttpMethod.Post, headers: headers);
                ResponseDataSet = JsonConvert.DeserializeObject<KustoRestResponseV1>(_httpClient.ResponseStreamString);

                if (!ResponseDataSet.HasData())
                {
                    Log.Info($"no tables:", ResponseDataSet);
                    return new List<string>();
                }

                KustoRestTableOfContentsV1 toc = SetTableOfContents(ResponseDataSet);

                if (toc.HasData)
                {
                    SetExtendedProperties();

                    long index = toc.Rows.FirstOrDefault(x => x.Kind.Equals("QueryResult")).Ordinal;
                    PrimaryResultTable = new KustoRestTable(ResponseDataSet.Tables[index]);
                    return PrimaryResultTable.RecordsCsv();
                }
                else
                {
                    TableOfContents = new KustoRestTableOfContentsV1();
                    Cursor = "''";
                    ExtendedPropertiesTable = new KustoRestTable();
                    PrimaryResultTable = new KustoRestTable(ResponseDataSet.Tables[0]);
                    return PrimaryResultTable.RecordsCsv();
                }
            }
            catch (Exception e)
            {
                Log.Exception($"exception executing query: {query}\r\n{e}");
                return new List<string>();
            }
        }

        private static void DisposeAdminClient(object state)
        {
            if (_kustoAdminClient != null)
            {
                Log.Info("Disposing kusto admin client");
                _kustoAdminClient.Dispose();
                _kustoAdminClient = null;
            }
        }

        private static void DisposeQueryClient(object state)
        {
            if (_kustoQueryClient != null)
            {
                Log.Info("Disposing kusto query client");
                _kustoQueryClient.Dispose();
                _kustoQueryClient = null;
            }
        }

        private List<string> Command(string command)
        {
            Log.Info($"command:{command}", ConsoleColor.Blue);
            if (_kustoAdminClient == null)
            {
                _kustoAdminClient = KustoClientFactory.CreateCslAdminProvider(ManagementConnection);
                adminTimer = new Timer(DisposeAdminClient, null, maxKustoClientTimeMs, maxKustoClientTimeMs);
            }

            adminTimer.Change(maxKustoClientTimeMs, maxKustoClientTimeMs);
            return EnumerateResults(_kustoAdminClient.ExecuteControlCommand(command));
        }

        private List<string> EnumerateResults(ProgressiveDataSet reader)
        {
            List<string> csvRecords = new List<string>();
            bool finalResults = false;
            bool isProgressive = false;

            try
            {
                IEnumerator<ProgressiveDataSetFrame> resultFrames = reader.GetFrames();

                while (!finalResults && resultFrames.MoveNext())
                {
                    ProgressiveDataSetFrame resultFrame = resultFrames.Current;
                    Log.Debug($"resultFrame:", resultFrame);

                    switch (resultFrame.FrameType)
                    {
                        case FrameType.DataSetCompletion:
                            {
                                ProgressiveDataSetCompletionFrame result = resultFrame as ProgressiveDataSetCompletionFrame;
                                Log.Info($"{result.GetType()}", result);
                                break;
                            }
                        case FrameType.DataSetHeader:
                            {
                                ProgressiveDataSetHeaderFrame result = resultFrame as ProgressiveDataSetHeaderFrame;
                                Log.Info($"{result.GetType()}", result);
                                isProgressive = result.IsProgressive;
                                break;
                            }
                        case FrameType.DataTable:
                            {
                                ProgressiveDataSetDataTableFrame result = resultFrame as ProgressiveDataSetDataTableFrame;
                                Log.Info($"{result.GetType()}", result);

                                if (result.TableName.Equals("@ExtendedProperties"))
                                {
                                    ExtendedResults = EnumerateResults(result.TableData);
                                    if (ExtendedResults.Any(x => x.Contains("Cursor")))
                                    {
                                        string cursorRecord = ExtendedResults.FirstOrDefault(x => x.Contains("Cursor"));
                                        Cursor = $"'{Regex.Match(cursorRecord, @"Cursor,(?<cursor>\d+?)(?:,|$)").Groups["cursor"].Value}'";
                                        Log.Info($"setting db cursor to {Cursor}");
                                    }
                                }

                                if (result.TableKind.Equals(WellKnownDataSet.PrimaryResult))
                                {
                                    csvRecords = EnumerateResults(result.TableData);
                                }

                                // if non progressive, this may be last frame
                                if (!isProgressive)
                                {
                                    finalResults = true;
                                }

                                break;
                            }
                        case FrameType.TableCompletion:
                            {
                                ProgressiveDataSetTableCompletionFrame result = resultFrame as ProgressiveDataSetTableCompletionFrame;
                                Log.Info($"{result.GetType()}", result);
                                break;
                            }
                        case FrameType.TableFragment:
                            {
                                ProgressiveDataSetDataTableFragmentFrame result = resultFrame as ProgressiveDataSetDataTableFragmentFrame;
                                Log.Error($"not implemented: {result.GetType()}", result);
                                result.ToDataTable();
                                break;
                            }
                        case FrameType.TableHeader:
                            {
                                ProgressiveDataSetHeaderFrame result = resultFrame as ProgressiveDataSetHeaderFrame;
                                Log.Info($"{result.GetType()}", result);
                                break;
                            }
                        case FrameType.TableProgress:
                            {
                                ProgressiveDataSetTableProgressFrame result = resultFrame as ProgressiveDataSetTableProgressFrame;
                                Log.Info($"{result.GetType()}", result);
                                break;
                            }
                        case FrameType.LastInvalid:
                        default:
                            {
                                Log.Warning($"unknown frame type:{resultFrame.FrameType}");
                                return csvRecords;
                            }
                    }
                }

                return csvRecords;
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
                return csvRecords;
            }
        }

        private List<string> EnumerateResults(IDataReader reader)
        {
            int maxRecords = 1000;
            int index = 0;
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

        private void SetExtendedProperties()
        {
            // extended properties stored in single 'Value' column as key value pair in json string
            string columnName = "Value";
            string extendedProperty = "Cursor";
            string tableName = "@ExtendedProperties";

            if (TableOfContents.Rows.Any(x => x.Name.Equals(tableName)))
            {
                long index = TableOfContents.Rows.FirstOrDefault(x => x.Name.Equals(tableName)).Ordinal;
                ExtendedPropertiesTable = new KustoRestTable(ResponseDataSet.Tables[index]);
                Dictionary<string, object> jsonString = ExtendedPropertiesTable.Records().FirstOrDefault(record => record[columnName].ToString().Contains(extendedProperty));

                if (jsonString != null)
                {
                    JObject jObject = (JObject)JsonConvert.DeserializeObject(jsonString[columnName].ToString());
                    Cursor = $"'{jObject.GetValue(extendedProperty)}'";
                }
            }
        }

        private KustoRestTableOfContentsV1 SetTableOfContents(KustoRestResponseV1 responseDataSet)
        {
            KustoRestTableOfContentsV1 content = new KustoRestTableOfContentsV1();

            if (responseDataSet == null || responseDataSet.Tables?.Length < 2)
            {
                Log.Debug($"no table of content table");
                return content;
            }

            KustoRestResponseTableV1 tableOfContents = responseDataSet.Tables.Last();

            for (int c = 0; c < tableOfContents.Columns.Length; c++)
            {
                content.Columns.Add(
                    new KustoRestTableOfContentsColumnV1
                    {
                        _index = c,
                        ColumnName = tableOfContents.Columns[c].ColumnName,
                        ColumnType = tableOfContents.Columns[c].ColumnType,
                        DataType = tableOfContents.Columns[c].DataType
                    });
            }

            for (int r = 0; r < tableOfContents.Rows.Length; r++)
            {
                Hashtable record = new Hashtable();
                KustoRestTableOfContentsRowV1 row = new KustoRestTableOfContentsRowV1();

                object[] rowFields = tableOfContents.Rows[r];
                if (rowFields.Length != tableOfContents.Columns.Length)
                {
                    Log.Error($"mismatch in column count and row count {rowFields.Count()} {tableOfContents.Columns.Length}");
                    return content;
                }

                row._index = r;
                row.Id = rowFields[content.Columns.First(x => x.ColumnName.Equals("Id"))._index].ToString();
                row.Kind = rowFields[content.Columns.First(x => x.ColumnName.Equals("Kind"))._index].ToString();
                row.Name = rowFields[content.Columns.First(x => x.ColumnName.Equals("Name"))._index].ToString();
                row.Ordinal = Convert.ToInt64(rowFields[content.Columns.First(x => x.ColumnName.Equals("Ordinal"))._index]);
                row.PrettyName = rowFields[content.Columns.First(x => x.ColumnName.Equals("PrettyName"))._index].ToString();
                content.Rows.Add(row);
            }

            return content;
        }
    }
}