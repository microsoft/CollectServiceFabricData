// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace CollectSFData.Kusto
{
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
}