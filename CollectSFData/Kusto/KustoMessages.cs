// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace CollectSFData
{
    public class KustoAdditionalProperties
    {
        public string authorizationContext;
        public bool compressed;
        public string csvMapping;
        public string jsonMapping;
    }

    public class KustoErrorMessage
    {
        public string Database;
        public string Details;
        public string ErrorCode;
        public DateTime FailedOn;
        public string FailureStatus;
        public string IngestionSourceId;
        public string IngestionSourcePath;
        public string OperationId;
        public bool OriginatesFromUpdatePolicy;
        public string RootActivityId;
        public bool ShouldRetry;
        public string Table;
    }

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

    public class KustoSuccessMessage
    {
        public string Database;
        public string FailureStatus;
        public string IngestionSourceId;
        public string IngestionSourcePath;
        public string OperationId;
        public string RootActivityId;
        public DateTime SucceededOn;
        public string Table;
    }
}