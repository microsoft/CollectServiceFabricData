// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

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