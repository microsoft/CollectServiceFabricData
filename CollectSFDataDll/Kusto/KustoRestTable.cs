// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using Kusto.Cloud.Platform.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CollectSFData.Kusto
{
    public class KustoRestRecord : Dictionary<string, object> { }

    public class KustoRestRecords : List<KustoRestRecord> { }

    public class KustoRestTable : KustoRestResponseTableV1
    {
        public KustoRestTable(KustoRestResponseTableV1 table = null)
        {
            if (table != null)
            {
                TableName = table.TableName;
                Columns = table.Columns;
                Rows = table.Rows;
            }
        }

        public KustoRestRecords Records()
        {
            KustoRestRecords records = new KustoRestRecords();
            if (Rows?.Length < 1)
            {
                Log.Info("no rows in table", ConsoleColor.White);
                return records;
            }

            for (int r = 0; r < Rows.Length; r++)
            {
                KustoRestRecord record = new KustoRestRecord();
                object[] rowFields = Rows[r];

                if (rowFields.Length != Columns.Length)
                {
                    Log.Error($"mismatch in column count and row count {rowFields.Count()} {Columns.Length}");
                    return records;
                }

                for (int f = 0; f < rowFields.Length; f++)
                {
                    string columnType = Columns[f].DataType.ToLower();
                    string columnName = Columns[f].ColumnName;

                    if (columnType.Contains("string"))
                    {
                        record.Add(columnName, rowFields[f]);
                    }
                    else if (columnType.Contains("int32"))
                    {
                        record.Add(columnName, Convert.ToInt32(rowFields[f]));
                    }
                    else if (columnType.Contains("int64"))
                    {
                        record.Add(columnName, Convert.ToInt64(rowFields[f]));
                    }
                    else if (columnType.Contains("date"))
                    {
                        record.Add(columnName, DateTime.Parse(rowFields[f].ToString()));
                    }
                    else if (columnType.Contains("bool"))
                    {
                        record.Add(columnName, Convert.ToBoolean(rowFields[f]).ToStringLowercase());
                    }
                    else if (columnType.Contains("time"))
                    {
                        record.Add(columnName, new TimeSpan(DateTime.Parse(rowFields[f].ToString()).Ticks));
                    }
                    else if (columnType.Contains("guid") || columnType.Contains("uuid") || columnType.Contains("uniqueid"))
                    {
                        record.Add(columnName, new Guid(rowFields[f].ToString()));
                    }
                    else if (columnType.Contains("double"))
                    {
                        record.Add(columnName, Convert.ToDouble(rowFields[f]));
                    }
                    else if (columnType.Contains("decimal"))
                    {
                        //record.Add(columnName, Convert.ToDecimal(rowFields[f]));
                        record.Add(columnName, Decimal.Parse(rowFields[f].ToString(), NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint));
                    }
                    else
                    {
                        record.Add(columnName, rowFields[f]);
                    }
                }
                records.Add(record);
            }
            Log.Info($"returning {records.Count} records", ConsoleColor.Cyan);
            return records;
        }

        public List<string> RecordsCsv()
        {
            List<string> results = new List<string>();

            if (Rows?.Length < 1)
            {
                Log.Info("no rows in table", ConsoleColor.White);
                return results;
            }

            Rows.ForEach(r => results.Add(string.Join(",", (Array.ConvertAll(r, ra => ra.ToString())))));
            Log.Info($"returning {results.Count} record csv's", ConsoleColor.Cyan);
            return results;
        }
    }
}