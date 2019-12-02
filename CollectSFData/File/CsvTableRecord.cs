// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace CollectSFData
{
    [Serializable]
    public class CsvTableRecord : IRecord
    {
        public string ETag { get; set; }

        public string EventTimeStamp { get; set; }

        public string PartitionKey { get; set; }

        public string PropertyName { get; set; }

        public string PropertyValue { get; set; }

        public string RelativeUri { get; set; }

        public string ResourceUri { get; set; }

        public string RowKey { get; set; }

        public DateTime Timestamp { get; set; }

        public IRecord Populate(FileObject fileObject, string dtrRecord, string resourceUri = null)
        {
            return this;
        }

        public override string ToString()
        {
            return $"{Timestamp:o},{EventTimeStamp},{ETag},{PartitionKey},{RowKey},{PropertyName},{PropertyValue},{RelativeUri},{ResourceUri}{Environment.NewLine}";
        }
    }
}