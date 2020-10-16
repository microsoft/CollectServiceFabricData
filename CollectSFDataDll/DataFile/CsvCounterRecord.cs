// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace CollectSFData.DataFile
{
    [Serializable]
    public class CsvCounterRecord : IRecord
    {
        public string CounterName { get; set; }

        public Decimal CounterValue { get; set; }

        public string FileType { get; set; }

        public string NodeName { get; set; }

        public string RelativeUri { get; set; }

        public string ResourceUri { get; set; }

        public DateTime Timestamp { get; set; }

        public IRecord Populate(FileObject fileObject, string dtrRecord, string resourceUri = null)
        {
            return this as ITraceRecord;
        }

        public override string ToString()
        {
            return $"{Timestamp:o},{CounterName},{CounterValue},{NodeName},{FileType},{RelativeUri},{ResourceUri}{Environment.NewLine}";
        }
    }
}