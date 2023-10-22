﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using CollectSFData.DataFile;
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
//using Microsoft.OData.Edm;
//using System.Data.Entity.Core.Metadata.Edm;
using System.Threading.Tasks;
using System.Linq.Expressions;

namespace CollectSFData.Azure
{
    public class TableManager
    {
        private readonly CustomTaskManager _tableTasks = new CustomTaskManager();
        private ConfigurationOptions _config;
        private Instance _instance;

        //private TableClient _tableClient;
        private TableServiceClient _tableServiceClient;

        public Action<FileObject> IngestCallback { get; set; }

        public List<TableItem> TableList { get; set; } = new List<TableItem>();

        public TableManager(Instance instance)
        {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
            _config = _instance.Config;
        }

        public bool Connect()
        {
            //TableContinuationToken tableToken = null;
            //CancellationToken cancellationToken = new CancellationToken();

            if (!_config.SasEndpointInfo.IsPopulated())
            {
                Log.Warning("no table or token info. exiting:", _config.SasEndpointInfo);
                return false;
            }

            try
            {
                //CloudTable table = new CloudTable(new Uri(_config.SasEndpointInfo.TableEndpoint + _config.SasEndpointInfo.SasToken));
                _tableServiceClient = new TableServiceClient(new Uri(_config.SasEndpointInfo.TableEndpoint + _config.SasEndpointInfo.SasToken));
                //_tableClient = table.ServiceClient;
                TableList.AddRange(_tableServiceClient.Query(x => x.Name.StartsWith(FileTypesKnownUrisPrefix.fabriclog), Constants.MaxResults, _tableTasks.CancellationToken));

                //TableResultSegment tables = _tableClient.ListTablesSegmentedAsync(
                //    null,
                //    Constants.MaxResults,
                //    tableToken,
                //    new TableRequestOptions(),
                //    null,
                //    cancellationToken).Result;

                //TableList.AddRange(tables);
                return true;
            }
            catch (Exception e)
            {
                _config.CheckPublicIp();
                Log.Exception($"{e}");
                return false;
            }
        }

        public void DownloadTables(string tablePrefix = "")
        {
            Log.Info($"downloading tables: with prefix {tablePrefix}");
            Pageable<TableItem> tables = _tableServiceClient.Query(x => x.Name.StartsWith(tablePrefix), Constants.MaxResults, _tableTasks.CancellationToken);

            foreach (TableItem table in tables)
            {
                EnumerateTable(table, Constants.TableMaxResults);
            }
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
                        Thread.Sleep(Constants.ThreadSleepMsWarning);
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

        //private Task<TableResultSegment> DownloadTablesSegment(Task<TableResultSegment> tableSegment, string urlFilterPattern)
        //{
        //    foreach (CloudTable cloudTable in tableSegment.Result.Results)
        //    {
        //        _tableTasks.QueueTaskAction(() => EnumerateTableRecords(cloudTable, urlFilterPattern));
        //    }

        //    return tableSegment;
        //}

        //private IEnumerable<List<CsvTableRecord>> EnumerateTable(TableItem table, int maxResults = Constants.TableMaxResults, bool limitResults = false)
        private int EnumerateTable(TableItem table, int maxResults = Constants.TableMaxResults, bool limitResults = false)
        {
            Log.Info($"enumerating table: {table.Name}");
            int resultsCount = 0;
            string continuationToken = null;
            bool moreResultsAvailable = true;
            if (limitResults)
            {
                maxResults = 1;
            }

            while (moreResultsAvailable)
            {
                try
                {
                    TableClient tableClient = _tableServiceClient.GetTableClient(table.Name);
                    Page<TableEntity> page = tableClient
                       .Query<TableEntity>()
                       .AsPages(continuationToken, pageSizeHint: maxResults)
                       .FirstOrDefault(); // Note: Since the pageSizeHint only limits the number of results in a single page, we explicitly only enumerate the first page.

                    if (page == null)
                    {
                        break;
                    }

                    continuationToken = page.ContinuationToken;

                    IReadOnlyList<TableEntity> pageResults = page.Values;
                    moreResultsAvailable = pageResults.Any() && continuationToken != null;
                    resultsCount += pageResults.Count;

                    if (_config.List)
                    {
                        Log.Info($"cloudtable: {table.Name} results: {pageResults.Count}");
                        continue;
                    }

                    _tableTasks.QueueTaskAction(() => EnumerateTableRecords(pageResults, table.Name));
                }
                catch (Exception e)
                {
                    Log.Exception($"exception in table enumeration {e}");
                    break;
                }
            }
            Log.Info("finished table enumeration");
            _tableTasks.Wait();
            Log.Highlight($"processed table count:{resultsCount.ToString("#,#")} minutes:{(DateTime.Now - _instance.StartTime).TotalMinutes.ToString("F3")} ");
            return resultsCount;
        }

        //        private IEnumerable<List<CsvTableRecord>> EnumerateTable(CloudTable cloudTable, int maxResults = Constants.TableMaxResults, bool limitResults = false)
        //        {
        //            Log.Info($"enumerating table: {cloudTable.Name}", ConsoleColor.Yellow);
        //            TableContinuationToken token = new TableContinuationToken();
        //            List<CsvTableRecord> results = new List<CsvTableRecord>();
        //            int tableRecords = 0;
        //            TableQuery query = GenerateTimeQuery(maxResults);
        //            _instance.TotalFilesEnumerated++;

        //            while (token != null)
        //            {
        //                Log.Info($"querying table:{cloudTable.Name} total:{tableRecords}", query);
        //#if NETCOREAPP
        //                TableQuerySegment tableSegment = cloudTable.ExecuteQuerySegmentedAsync(query, token, null, null).Result;
        //#else
        //                TableQuerySegment<DynamicTableEntity> tableSegment = cloudTable.ExecuteQuerySegmentedAsync(query, token, null, null).Result;
        //#endif
        //                token = tableSegment.ContinuationToken;

        //                results.AddRange(FormatRecordResults(cloudTable, tableSegment));
        //                tableRecords += results.Count;

        //                if (results.Count == 0)
        //                {
        //                    break;
        //                }

        //                if (limitResults && (maxResults -= tableSegment.Results.Count) <= 0)
        //                {
        //                    break;
        //                }

        //                if (results.Count >= maxResults)
        //                {
        //                    Log.Info($"yielding chunk {results.Count}", ConsoleColor.DarkCyan);
        //                    yield return results;
        //                    results.Clear();
        //                }
        //            }

        //            Log.Info($"return: table {cloudTable.Name} records count:{tableRecords} query:", null, ConsoleColor.DarkCyan, query);
        //            yield return results;
        //        }

        private void EnumerateTableRecords(IReadOnlyList<TableEntity> tableEntities, string tableName)
        {
            if (tableEntities.Count < 1)
            {
                return;
            }

            string relativeUri = $"{_config.StartTimeUtc.Ticks}-{_config.EndTimeUtc.Ticks}-{tableName}.{tableEntities.Count}{Constants.TableExtension}";
            FileObject fileObject = new FileObject(relativeUri, _config.CacheLocation) { Status = FileStatus.enumerated };

            if (_instance.FileObjects.FindByUriFirstOrDefault(relativeUri).Status == FileStatus.existing)
            {
                Log.Info($"{relativeUri} already exists. skipping", ConsoleColor.DarkYellow);
                return;
            }

            //foreach (TableEntity entity in tableEntities)
            //{
            _instance.FileObjects.Add(fileObject);
            //tableEntities.ToList().ForEach(x => x.RelativeUri = relativeUri);
            fileObject.Stream.Write(FormatRecordResults(relativeUri, tableEntities));
            //fileObject.Stream.Write(tableEntities);

            _instance.TotalFilesDownloaded++;
            IngestCallback?.Invoke(fileObject);
            //}
        }

        //private void EnumerateTableRecords(CloudTable cloudTable, string urlFilterPattern)
        //{
        //    if (string.IsNullOrEmpty(urlFilterPattern) || Regex.IsMatch(cloudTable.Uri.ToString(), urlFilterPattern, RegexOptions.IgnoreCase))
        //    {
        //        int chunkCount = 0;

        //        foreach (IList<CsvTableRecord> resultsChunk in EnumerateTable(cloudTable, Constants.TableMaxResults))
        //        {
        //            if (resultsChunk.Count < 1)
        //            {
        //                continue;
        //            }

        //            if (_config.List)
        //            {
        //                Log.Info($"cloudtable: {cloudTable.Name} results: {resultsChunk.Count}");
        //                continue;
        //            }

        //            string relativeUri = $"{_config.StartTimeUtc.Ticks}-{_config.EndTimeUtc.Ticks}-{cloudTable.Name}.{chunkCount++}{Constants.TableExtension}";
        //            FileObject fileObject = new FileObject(relativeUri, _config.CacheLocation) { Status = FileStatus.enumerated };

        //            if (_instance.FileObjects.FindByUriFirstOrDefault(relativeUri).Status == FileStatus.existing)
        //            {
        //                Log.Info($"{relativeUri} already exists. skipping", ConsoleColor.DarkYellow);
        //                continue;
        //            }

        //            _instance.FileObjects.Add(fileObject);
        //            resultsChunk.ToList().ForEach(x => x.RelativeUri = relativeUri);
        //            fileObject.Stream.Write(resultsChunk);

        //            _instance.TotalFilesDownloaded++;
        //            IngestCallback?.Invoke(fileObject);
        //        }
        //    }
        //    else
        //    {
        //        _instance.TotalFilesSkipped++;
        //    }
        //}

        private List<CsvTableRecord> FormatRecordResults(string relativeUri, IReadOnlyList<TableEntity> tableEntities)
        {
            List<CsvTableRecord> csvRecords = new List<CsvTableRecord>();

            // todo: sort by timestamp?
            //foreach (TableEntity result in tableSegment.Results.OrderBy(x => x.Timestamp))
            foreach (TableEntity tableEntity in tableEntities)
            {
                foreach (KeyValuePair<string, string> entity in MapEntitiesTypes(tableEntity))
                {
                    string actualTimeStamp = string.Empty;

                    if (tableEntity.RowKey.Contains("_"))
                    {
                        try
                        {
                            // subtract rowkey prefix (ticks) from ticks maxvalue to get actual time
                            actualTimeStamp = new DateTime(DateTime.MaxValue.Ticks - Convert.ToInt64(tableEntity.RowKey.Substring(0, tableEntity.RowKey.IndexOf("_")))).ToString(Constants.DateTimeFormat);
                        }
                        catch (Exception e)
                        {
                            Log.Debug($"actualTimeStamp:{e}");
                        }
                    }

                    csvRecords.Add(new CsvTableRecord()
                    {
                        Timestamp = tableEntity.Timestamp.Value.UtcDateTime,
                        EventTimeStamp = actualTimeStamp,
                        ETag = tableEntity.ETag.ToString(),
                        PartitionKey = tableEntity.PartitionKey,
                        RowKey = tableEntity.RowKey,
                        PropertyName = entity.Key,
                        PropertyValue = $"\"{entity.Value.Trim('"').Replace("\"", "'")}\"",
                        RelativeUri = relativeUri,
                        ResourceUri = _config.ResourceUri
                    });
                }
            }

            return csvRecords;
        }

        //private TableQuery GenerateTimeQuery(int maxResults = Constants.TableMaxResults)
        //{
        //    TableQuery query = new TableQuery();
        //    string startDate = TableQuery.GenerateFilterConditionForDate("Timestamp", QueryComparisons.GreaterThanOrEqual, _config.StartTimeUtc);
        //    string endDate = TableQuery.GenerateFilterConditionForDate("Timestamp", QueryComparisons.LessThanOrEqual, _config.EndTimeUtc);

        //    query.FilterString = TableQuery.CombineFilters(startDate, TableOperators.And, endDate);
        //    query.TakeCount = Math.Min(maxResults, Constants.MaxResults);
        //    Log.Info("query string:", ConsoleColor.Cyan, null, query);

        //    return query;
        //}

        private Dictionary<string, string> MapEntitiesTypes(TableEntity tableEntity)
        {
            Dictionary<string, string> convertedEntities = new Dictionary<string, string>();
            string entityString = null;

            foreach (KeyValuePair<string, object> prop in tableEntity)
            {
                // Log.Debug($"kvp:{prop.Key}");
                object entity = null;

                switch (prop.Value)
                {
                    case BinaryData binaryData:
                        entity = binaryData;
                        entityString = Convert.ToBoolean(entity).ToString();
                        break;

                    case bool boolValue:
                        entity = boolValue;
                        entityString = Convert.ToBoolean(entity).ToString();
                        break;

                    case DateTime dateValue:
                        entity = dateValue;
                        entityString = Convert.ToDateTime(entity).ToString(Constants.DateTimeFormat);
                        break;

                    case Double doubleValue:
                        entity = doubleValue;
                        entityString = Convert.ToDouble(entity).ToString();
                        break;

                    case Guid guidValue:
                        entity = guidValue;
                        entityString = entity.ToString();
                        break;

                    case Int32 int32Value:
                        entity = int32Value;
                        entityString = Convert.ToInt32(entity).ToString();
                        break;

                    case Int64 int64Value:
                        entity = int64Value;
                        entityString = Convert.ToInt64(entity).ToString();
                        break;

                    case String stringValue:
                        entity = stringValue;
                        entityString = entity.ToString();
                        break;

                    default:
                        Log.Error($"unknown edmtype:{prop.Value.GetType()}");
                        break;
                }

                convertedEntities.Add(prop.Key, entityString);
            }

            return convertedEntities;
        }

        private bool TableRecordExists(string tableName)
        {
            // first record is header
            //bool recordExists = EnumerateTable(_tableClient.GetTableReference(tableName), 1, true).Count() > 1;
            bool recordExists = EnumerateTable(new TableItem(tableName), 1, true) > 0;

            Log.Highlight($"record exists in table:{recordExists}");
            return recordExists;
        }
    }
}