// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace CollectSFData.Kusto
{
    public class KustoIngestionMessage
    {
        public KustoAdditionalProperties AdditionalProperties;
        public string BlobPath;
        public string DatabaseName;
        public bool FlushImmediately;
        public string Format;
        public string Id;
        public int RawDataSize;
        public int ReportLevel;
        public int ReportMethod;
        public bool RetainBlobOnSuccess;
        public string TableName;
    }
}