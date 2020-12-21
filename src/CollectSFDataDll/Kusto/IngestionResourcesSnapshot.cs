// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Collections.Generic;

namespace CollectSFData.Kusto
{
    public class IngestionResourcesSnapshot
    {
        public string FailureNotificationsQueue { get; set; } = string.Empty;

        public IList<string> IngestionQueues { get; set; } = new List<string>();

        public string SuccessNotificationsQueue { get; set; } = string.Empty;

        public IList<string> TempStorageContainers { get; set; } = new List<string>();
    }
}