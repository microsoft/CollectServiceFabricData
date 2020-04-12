using System;
using System.Collections.Generic;

namespace CollectSFData
{
    public class KustoRestResponseColumnV1
    {
        public string ColumnName;
        public string ColumnType;
        public string DataType;
    }

    public class KustoRestResponseTableV1
    {
        public KustoRestResponseColumnV1[] Columns;
        public object[][] Rows;
        public string TableName;

        public bool HasData() => Rows?.Length > 0;
    }

    public class KustoRestResponseV1
    {
        public KustoRestResponseTableV1[] Tables;

        public bool HasData() => Tables?.Length > 0;
    }
}