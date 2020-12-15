// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace CollectSFData.Kusto
{
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