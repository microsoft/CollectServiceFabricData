// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using Kusto.Cloud.Platform.Data;
using Kusto.Cloud.Platform.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace CollectSFData.Kusto
{
    public class KustoRestRecord : Dictionary<string, object>
    { }

    public class KustoRestRecords : List<KustoRestRecord>
    { }

    public class KustoRestTable : DataTable //KustoRestResponseTableV1
    {
        private IDataReader _reader;

        public KustoRestTable(IDataReader reader) : base()
        {
            _reader = reader;
            DataTable schemaTable = reader.GetSchemaTable();

            if (schemaTable == null)
            {
                Log.Error("no schema table");
                return;
            }

            TableName = schemaTable.TableName;

            if (schemaTable.Columns.Count < 1)
            {
                Log.Error("no columns in table");
                return;
            }

            for (int i = 0; i < reader.FieldCount; i++)
            {
                DataColumn newColumn = new DataColumn();
                newColumn.ColumnName = reader.GetName(i);
                newColumn.DataType = reader.GetFieldType(i);
                Columns.Add(newColumn);
            }

            while (reader.Read())
            {
                object[] rowValues = new object[reader.FieldCount];
                reader.GetValues(rowValues);
                foreach (object value in rowValues)
                {
                    Log.Info(value.ToString());
                }
                Rows.Add(rowValues);
            }
        }

        public KustoRestRecords Records()
        {
            KustoRestRecords records = new KustoRestRecords();
            if (Rows?.Count < 1)
            {
                Log.Info("no rows in table", ConsoleColor.White);
                return records;
            }

            for (int r = 0; r < Rows.Count; r++)
            {
                KustoRestRecord record = new KustoRestRecord();
                object[] rowFields = Rows[r].ItemArray;

                if (rowFields.Length != Columns.Count)
                {
                    Log.Error($"mismatch in column count and row count {rowFields.Count()} {Columns.Count}");
                    return records;
                }

                for (int f = 0; f < rowFields.Length; f++)
                {
                    string columnType = Columns[f].DataType.ToString().ToLower();
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

            if (Rows?.Count < 1)
            {
                Log.Info("no rows in table", ConsoleColor.White);
                return results;
            }

            foreach (DataRow row in Rows)
            {
                results.Add(string.Join(",", (Array.ConvertAll<object, string>(row.ItemArray, ra => ra.ToString()))));
            }

            Log.Info($"returning {results.Count} record csv's", ConsoleColor.Cyan);
            return results;
        }

        public KustoRestRecords RecordsList()
        {
            KustoRestRecords list = new KustoRestRecords();

            if (Rows?.Count < 1)
            {
                Log.Info("no rows in table", ConsoleColor.White);
                return list;
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                KustoRestRecord row = new KustoRestRecord();

                for (int j = 0; j < Columns.Count; j++)
                {
                    row.Add(Columns[j].ColumnName, Rows[i][j]);
                }

                list.Add(row);
            }

            Log.Info($"returning {list.Count} records", ConsoleColor.Cyan);
            return list;
        }
    }
}