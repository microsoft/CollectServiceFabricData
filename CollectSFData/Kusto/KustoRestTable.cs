using Kusto.Cloud.Platform.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Configuration;

namespace CollectSFData
{
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

        public List<Dictionary<string, object>> Records()
        {
            List<Dictionary<string, object>> records = new List<Dictionary<string, object>>();
            if (Rows?.Length < 1)
            {
                Log.Warning("no rows in table");
                return records;
            }

            for (int r = 0; r < Rows.Length; r++)
            {
                Dictionary<string, object> record = new Dictionary<string, object>();
                object[] rowFields = Rows[r];

                if (rowFields.Length != Columns.Length)
                {
                    Log.Error($"mismatch in column count and row count {rowFields.Count()} {Columns.Length}");
                    return records;
                }

                for (int f = 0; f < rowFields.Length; f++)
                {
                    KustoRestResponseColumnV1 column = Columns[f];

                    if (column.DataType.ToLower().Contains("string"))
                    {
                        record.Add(column.ColumnName, rowFields[f]);
                    }
                    else if (column.DataType.ToLower().Contains("int32"))
                    {
                        record.Add(column.ColumnName, Convert.ToInt32(rowFields[f]));
                    }
                    else if (column.DataType.ToLower().Contains("int64"))
                    {
                        record.Add(column.ColumnName, Convert.ToInt64(rowFields[f]));
                    }
                    else if (column.DataType.ToLower().Contains("date"))
                    {
                        record.Add(column.ColumnName, DateTime.Parse(rowFields[f].ToString()));
                    }
                    else if (column.DataType.ToLower().Contains("bool"))
                    {
                        record.Add(column.ColumnName, Convert.ToBoolean(rowFields[f]).ToStringLowercase());
                    }
                    else if (column.DataType.ToLower().Contains("time"))
                    {
                        record.Add(column.ColumnName, new TimeSpan(DateTime.Parse(rowFields[f].ToString()).Ticks));
                    }
                    else if (column.DataType.ToLower().Contains("guid") || column.DataType.ToLower().Contains("uuid") || column.DataType.ToLower().Contains("uniqueid"))
                    {
                        record.Add(column.ColumnName, new Guid(rowFields[f].ToString()));
                    }
                    else if (column.DataType.ToLower().Contains("double"))
                    {
                        record.Add(column.ColumnName, Convert.ToDouble(rowFields[f]));
                    }
                    else if (column.DataType.ToLower().Contains("decimal"))
                    {
                        //record.Add(column.ColumnName, Convert.ToDecimal(rowFields[f]));
                        record.Add(column.ColumnName, Decimal.Parse(rowFields[f].ToString(), NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint));
                    }
                    else
                    {
                        record.Add(column.ColumnName, rowFields[f]);
                    }
                }
                records.Add(record);
            }
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
            return results;
        }
    }
}