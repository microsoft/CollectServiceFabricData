// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace CollectSFData.LogAnalytics
{
    public class LogAnalyticsQueryResults
    {
        public Table[] tables;

        public class Table
        {
            public Column[] columns;
            public string name;
            public object[][] rows;

            public class Column
            {
                public string name;
                public string type;
            }
        }
    }
}