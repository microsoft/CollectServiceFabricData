// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using CollectSFData.DataFile;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CollectSFData.Azure
{
    public class TableManager : Constants
    {
        private readonly CustomTaskManager _tableTasks = new CustomTaskManager(true);
        private Instance _instance = Instance.Singleton();
        private CloudTableClient _tableClient;
        private ConfigurationOptions Config => _instance.Config;
        public Action<FileObject> IngestCallback { get; set; }
        public List<CloudTable> TableList { get; set; } = new List<CloudTable>();

        public bool Connect()
        {
            TableContinuationToken tableToken = null;
            CancellationToken cancellationToken = new CancellationToken();

            if (!Config.SasEndpointInfo.IsPopulated())
            {
                Log.Warning("no table or token info. exiting:", Config.SasEndpointInfo);
                return false;
            }

            try
            {
                CloudTable table = new CloudTable(new Uri(Config.SasEndpointInfo.TableEndpoint + Config.SasEndpointInfo.SasToken));
                _tableClient = table.ServiceClient;

                TableResultSegment tables = _tableClient.ListTablesSegmentedAsync(
                    null,
                    MaxResults,
                    tableToken,
                    new TableRequestOptions(),
                    null,
                    cancellationToken).Result;

                TableList.AddRange(tables);
                return true;
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
                return false;
            }
        }

        public void DownloadTables(string tablePrefix = "")
        {
            Log.Info($"enumerating tables: with prefix {tablePrefix}");
            int resultsCount = 0;
            TableContinuationToken token = new TableContinuationToken();
            tablePrefix = string.IsNullOrEmpty(Config.UriFilter) ? tablePrefix : Config.UriFilter;

            while (token != null)
            {
                try
                {
                    Task<TableResultSegment> tableSegment = _tableClient.ListTablesSegmentedAsync(tablePrefix, MaxResults, token, null, null);
                    Task<TableResultSegment> task = DownloadTablesSegment(tableSegment, Config.ContainerFilter);

                    token = task.Result.ContinuationToken;
                    resultsCount += task.Result.Results.Count;
                }
                catch (Exception e)
                {
                    Log.Exception($"exception in table enumeration { e }");
                    break;
                }
            }

            Log.Info("finished table enumeration");
            _tableTasks.Wait();
            Log.Highlight($"processed table count:{ resultsCount.ToString("#,#") } minutes:{ (DateTime.Now - _instance.StartTime).TotalMinutes.ToString("F3") } ");
        }

        public string QueryTablesForClusterId()
        {
            string tablePattern = FileTypesKnownUrisPrefix.fabriclog + "(?<guidString>[A-Fa-f0-9]{32})GlobalTime$";
            Log.Info($"querying table names for pattern:{tablePattern}", ConsoleColor.Green);
            string clusterId = null;

            try
            {
                IEnumerable<string> tableNames = TableList.Where(x => Regex.IsMatch(x.Name, tablePattern, RegexOptions.IgnoreCase)).Select(x => x.Name);
                Dictionary<string, string> tableGuids = new Dictionary<string, string>();

                foreach (string tableName in tableNames)
                {
                    string clusterGuid = Regex.Match(tableName, tablePattern, RegexOptions.IgnoreCase).Groups["guidString"].Value;
                    clusterGuid = clusterGuid.Insert(8, "-").Insert(13, "-").Insert(18, "-").Insert(23, "-");

                    if (!tableGuids.ContainsKey(clusterGuid))
                    {
                        tableGuids.Add(clusterGuid, tableName);
                    }
                }

                if (tableGuids.Count == 1)
                {
                    clusterId = tableGuids.First().Key;
                }
                else if (tableGuids.Count > 1)
                {
                    Log.Warning("there is more than one distinct blob table guid (service fabric deployment).");
                    int tablesWithRecords = 0;

                    foreach (KeyValuePair<string, string> deployment in tableGuids)
                    {
                        Log.Info($"querying table for given time range:{deployment}");

                        if (TableRecordExists(deployment.Value))
                        {
                            clusterId = deployment.Key;
                            tablesWithRecords++;
                        }
                    }

                    if (tablesWithRecords > 1)
                    {
                        Log.Warning("there is more than one blob table (service fabric deployment) containing records in configured time range. not setting container prefix:", tableGuids.Distinct());
                        Log.Warning("for quicker enumeration, ctrl-c to quit and use --containerFilter argument to specify correct blob.");
                        Thread.Sleep(ThreadSleepMsWarning);
                        clusterId = null;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning($"unable to determine blob name from table name:{e}");
            }

            Log.Info($"returning cluster id:{clusterId}");
            return clusterId;
        }

        private Task<TableResultSegment> DownloadTablesSegment(Task<TableResultSegment> tableSegment, string urlFilterPattern)
        {
            foreach (CloudTable cloudTable in tableSegment.Result.Results)
            {
                _tableTasks.QueueTaskAction(() => EnumerateTableRecords(cloudTable, urlFilterPattern));
            }

            return tableSegment;
        }

        private IEnumerable<List<CsvTableRecord>> EnumerateTable(CloudTable cloudTable, int maxResults = TableMaxResults, bool limitResults = false)
        {
            Log.Info($"enumerating table: {cloudTable.Name}", ConsoleColor.Yellow);
            TableContinuationToken token = new TableContinuationToken();
            List<CsvTableRecord> results = new List<CsvTableRecord>();
            int tableRecords = 0;
            TableQuery query = GenerateTimeQuery(maxResults);
            _instance.TotalFilesEnumerated++;

            while (token != null)
            {
                Log.Info($"querying table:{cloudTable.Name} total:{tableRecords}", query);
#if NETCOREAPP
                TableQuerySegment tableSegment = cloudTable.ExecuteQuerySegmentedAsync(query, token, null, null).Result;
#else
                TableQuerySegment<DynamicTableEntity> tableSegment = cloudTable.ExecuteQuerySegmentedAsync(query, token, null, null).Result;
#endif
                token = tableSegment.ContinuationToken;

                results.AddRange(FormatRecordResults(cloudTable, tableSegment));
                tableRecords += results.Count;

                if (results.Count == 0)
                {
                    break;
                }

                if (limitResults && (maxResults -= tableSegment.Results.Count) <= 0)
                {
                    break;
                }

                if (results.Count >= maxResults)
                {
                    Log.Info($"yielding chunk {results.Count}", ConsoleColor.DarkCyan);
                    yield return results;
                    results.Clear();
                }
            }

            Log.Info($"return: table {cloudTable.Name} records count:{tableRecords} query:", null, ConsoleColor.DarkCyan, query);
            yield return results;
        }

        private void EnumerateTableRecords(CloudTable cloudTable, string urlFilterPattern)
        {
            if (string.IsNullOrEmpty(urlFilterPattern) || Regex.IsMatch(cloudTable.Uri.ToString(), urlFilterPattern, RegexOptions.IgnoreCase))
            {
                int chunkCount = 0;

                foreach (IList<CsvTableRecord> resultsChunk in EnumerateTable(cloudTable, TableMaxResults))
                {
                    if (resultsChunk.Count < 1)
                    {
                        continue;
                    }

                    if (Config.List)
                    {
                        Log.Info($"cloudtable: {cloudTable.Name} results: {resultsChunk.Count}");
                        continue;
                    }

                    string relativeUri = $"{Config.StartTimeUtc.Ticks}-{Config.EndTimeUtc.Ticks}-{cloudTable.Name}.{chunkCount++}{TableExtension}";
                    FileObject fileObject = new FileObject(relativeUri, Config.CacheLocation);
                    resultsChunk.ToList().ForEach(x => x.RelativeUri = relativeUri);

                    fileObject.Stream.Write(resultsChunk);

                    _instance.TotalFilesDownloaded++;
                    IngestCallback?.Invoke(fileObject);
                }
            }
            else
            {
                _instance.TotalFilesSkipped++;
            }
        }

#if NETCOREAPP

        private List<CsvTableRecord> FormatRecordResults(CloudTable cloudTable, TableQuerySegment tableSegment)
#else
        private List<CsvTableRecord> FormatRecordResults(CloudTable cloudTable, TableQuerySegment<DynamicTableEntity> tableSegment)
#endif
        {
            List<CsvTableRecord> results = new List<CsvTableRecord>();

            foreach (DynamicTableEntity result in tableSegment.Results.OrderBy(x => x.Timestamp))
            {
                foreach (KeyValuePair<string, string> entity in MapEntitiesTypes(result))
                {
                    string actualTimeStamp = string.Empty;

                    if (result.RowKey.Contains("_"))
                    {
                        try
                        {
                            // subtract rowkey prefix (ticks) from ticks maxvalue to get actual time
                            actualTimeStamp = new DateTime(DateTime.MaxValue.Ticks - Convert.ToInt64(result.RowKey.Substring(0, result.RowKey.IndexOf("_")))).ToString(DateTimeFormat);
                        }
                        catch (Exception e)
                        {
                            Log.Debug($"actualTimeStamp:{e}");
                        }
                    }

                    results.Add(new CsvTableRecord()
                    {
                        Timestamp = result.Timestamp.UtcDateTime,
                        EventTimeStamp = actualTimeStamp,
                        ETag = result.ETag,
                        PartitionKey = result.PartitionKey,
                        RowKey = result.RowKey,
                        PropertyName = entity.Key,
                        PropertyValue = $"\"{entity.Value}\"",
                        RelativeUri = cloudTable.Name,
                        ResourceUri = Config.ResourceUri
                    });
                }
            }

            return results;
        }

        private TableQuery GenerateTimeQuery(int maxResults = TableMaxResults)
        {
            TableQuery query = new TableQuery();
            string startDate = TableQuery.GenerateFilterConditionForDate("Timestamp", QueryComparisons.GreaterThanOrEqual, Config.StartTimeUtc);
            string endDate = TableQuery.GenerateFilterConditionForDate("Timestamp", QueryComparisons.LessThanOrEqual, Config.EndTimeUtc);

            query.FilterString = TableQuery.CombineFilters(startDate, TableOperators.And, endDate);
            query.TakeCount = Math.Min(maxResults, MaxResults);
            Log.Info("query string:", ConsoleColor.Cyan, null, query);

            return query;
        }

        private Dictionary<string, string> MapEntitiesTypes(DynamicTableEntity result)
        {
            Dictionary<string, string> convertedEntities = new Dictionary<string, string>();
            string entityString = null;

            foreach (KeyValuePair<string, EntityProperty> prop in result.Properties)
            {
                // Log.Debug($"kvp:{prop.Key}");
                object entity = null;

                switch (prop.Value.PropertyType)
                {
                    case EdmType.Binary:
                        entity = prop.Value.BinaryValue;
                        entityString = Convert.ToBoolean(entity).ToString();
                        break;

                    case EdmType.Boolean:
                        entity = prop.Value.BooleanValue;
                        entityString = Convert.ToBoolean(entity).ToString();
                        break;

                    case EdmType.DateTime:
                        entity = prop.Value.DateTime;
                        entityString = Convert.ToDateTime(entity).ToString(DateTimeFormat);
                        break;

                    case EdmType.Double:
                        entity = prop.Value.DoubleValue;
                        entityString = Convert.ToDouble(entity).ToString();
                        break;

                    case EdmType.Guid:
                        entity = prop.Value.GuidValue;
                        entityString = entity.ToString();
                        break;

                    case EdmType.Int32:
                        entity = prop.Value.Int32Value;
                        entityString = Convert.ToInt32(entity).ToString();
                        break;

                    case EdmType.Int64:
                        entity = prop.Value.Int64Value;
                        entityString = Convert.ToInt64(entity).ToString();
                        break;

                    case EdmType.String:
                        entity = prop.Value.StringValue;
                        entityString = entity.ToString();
                        break;

                    default:
                        Log.Error($"unknown edmtype:{prop.Value.PropertyType}");
                        break;
                }

                convertedEntities.Add(prop.Key, entityString);
            }

            return convertedEntities;
        }

        private bool TableRecordExists(string tableName)
        {
            // first record is header
            bool recordExists = EnumerateTable(_tableClient.GetTableReference(tableName), 1, true).Count() > 1;
            Log.Highlight($"record exists in table:{recordExists}");
            return recordExists;
        }
    }
}