// ------------------------------------------------------------
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

namespace CollectSFData.Azure
{
    public class TableManager
    {
        private readonly CustomTaskManager _tableTasks = new CustomTaskManager();
        private ConfigurationOptions _config;
        private Instance _instance;
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
            if (!_config.SasEndpointInfo.IsPopulated())
            {
                Log.Warning("no table or token info. exiting:", _config.SasEndpointInfo);
                return false;
            }

            return EnumerateTables();
        }

        public void DownloadTables(string tablePrefix = "")
        {
            Log.Info($"downloading tables: with prefix {tablePrefix}");
            if (!EnumerateTables(tablePrefix))
            {
                Log.Warning("error enumerating tables.");
                return;
            }

            foreach (TableItem table in TableList)
            {
                EnumerateTable(table, Constants.MaxEnumerationResults);
            }
        }

        public bool EnumerateTables(string prefix = "")
        {
            try
            {
                TableList = new List<TableItem>();
                _tableServiceClient = new TableServiceClient(new Uri(_config.SasEndpointInfo.TableEndpoint + _config.SasEndpointInfo.SasToken));
                Pageable<TableItem> tables = _tableServiceClient.Query(x => x.Name != "", Constants.MaxEnumerationResults, _tableTasks.CancellationToken);
                foreach (TableItem table in tables)
                {
                    if (!string.IsNullOrEmpty(prefix) && !Regex.IsMatch(table.Name, prefix))
                    {
                        Log.Info($"skipping table {table.Name}. does not match {prefix}.");
                        continue;
                    }
                    TableList.Add(table);
                }

                return true;
            }
            catch (Exception e)
            {
                _config.CheckPublicIp();
                Log.Exception($"{e}");
                return false;
            }
        }

        public string QueryTablesForClusterId()
        {
            string tablePattern = Constants.TableNamePattern;
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

        private int EnumerateTable(TableItem table, int maxResults = Constants.MaxEnumerationResults, bool limitResults = false)
        {
            Log.Info($"enumerating table: {table.Name}");
            int resultsCount = 0;
            string continuationToken = "";
            int iteration = 0;
            bool moreResultsAvailable = true;
            if (limitResults)
            {
                maxResults = 1;
            }

            try
            {
                TableClient tableClient = _tableServiceClient.GetTableClient(table.Name);

                while (!_tableTasks.CancellationToken.IsCancellationRequested && moreResultsAvailable)
                {
                    Page<TableEntity> page = tableClient
                       .Query<TableEntity>()
                       .AsPages(continuationToken, maxResults)
                       .FirstOrDefault(); // Note: Since the pageSizeHint only limits the number of results in a single page, we explicitly only enumerate the first page.

                    if (page == null)
                    {
                        break;
                    }

                    continuationToken = page.ContinuationToken;

                    IReadOnlyList<TableEntity> pageResults = page.Values;
                    moreResultsAvailable = pageResults.Any() && !string.IsNullOrEmpty(continuationToken);
                    resultsCount += pageResults.Count;

                    if (_config.List)
                    {
                        Log.Info($"cloudtable: {table.Name} results: {pageResults.Count}");
                        continue;
                    }

                    _tableTasks.QueueTaskAction(() => EnumerateTableRecords(pageResults, table.Name, iteration++));
                }

                Log.Info("finished table enumeration");
                _tableTasks.Wait();
                Log.Highlight($"processed table count:{resultsCount.ToString("#,#")} minutes:{(DateTime.Now - _instance.StartTime).TotalMinutes.ToString("F3")} ");
                return resultsCount;
            }
            catch (Exception e)
            {
                Log.Exception($"exception in table enumeration {e}");
                return 0;
            }
        }

        private void EnumerateTableRecords(IReadOnlyList<TableEntity> tableEntities, string tableName, int tableEntitiesCount)
        {
            Log.Debug($"table:{tableName} records:{tableEntities.Count}");
            if (tableEntities.Count < 1)
            {
                return;
            }

            string relativeUri = $"{_config.StartTimeUtc.Ticks}-{_config.EndTimeUtc.Ticks}-{tableName}.{tableEntitiesCount}{Constants.TableExtension}";
            FileObject fileObject = new FileObject(relativeUri, _config.CacheLocation) { Status = FileStatus.enumerated };

            if (_instance.FileObjects.FindByUriFirstOrDefault(relativeUri).Status == FileStatus.existing)
            {
                Log.Info($"{relativeUri} already exists. skipping", ConsoleColor.DarkYellow);
                return;
            }

            _instance.FileObjects.Add(fileObject);
            fileObject.Stream.Write(FormatRecordResults(relativeUri, tableEntities));

            _instance.TotalFilesDownloaded++;
            IngestCallback?.Invoke(fileObject);
        }

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

        private Dictionary<string, string> MapEntitiesTypes(TableEntity tableEntity)
        {
            Dictionary<string, string> convertedEntities = new Dictionary<string, string>();
            string entityString = null;

            foreach (KeyValuePair<string, object> prop in tableEntity)
            {
                Log.Trivial($"kvp:{prop.Key}");
                object entity = null;

                switch (prop.Value)
                {
                    case BinaryData binaryData:
                        entity = binaryData;
                        entityString = Convert.ToBoolean(entity).ToString();
                        break;

                    case bool boolValue:
                        entity = boolValue;
                        entityString = boolValue.ToString();
                        break;

                    case DateTime dateValue:
                        entity = dateValue;
                        entityString = dateValue.ToString(Constants.DateTimeFormat);
                        break;

                    case DateTimeOffset dateTimeOffsetValue:
                        entity = dateTimeOffsetValue;
                        entityString = dateTimeOffsetValue.ToUniversalTime().ToString(Constants.DateTimeFormat);
                        break;

                    case double doubleValue:
                        entity = doubleValue;
                        entityString = doubleValue.ToString();
                        break;

                    case Guid guidValue:
                        entity = guidValue;
                        entityString = guidValue.ToString();
                        break;

                    case int int32Value:
                        entity = int32Value;
                        entityString = int32Value.ToString();
                        break;

                    case long int64Value:
                        entity = int64Value;
                        entityString = int64Value.ToString();
                        break;

                    case string stringValue:
                        entity = stringValue;
                        entityString = stringValue;
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
            bool recordExists = EnumerateTable(new TableItem(tableName), 1, true) > 0;

            Log.Highlight($"record exists in table:{recordExists}");
            return recordExists;
        }
    }
}