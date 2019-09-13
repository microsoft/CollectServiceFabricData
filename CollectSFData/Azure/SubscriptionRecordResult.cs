// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace CollectSFData
{
    public class SubscriptionRecordResult
    {
        public string authorizationSource;
        public string displayName;
        public string id;
        public string state;
        public string subscriptionId;
        public SubscriptionPolicies subscriptionPolicies = new SubscriptionPolicies();

        public class SubscriptionPolicies
        {
            public string locationPlacementId;
            public string quotaId;
            public string spendingLimit;
        }
    }

    public class SubscriptionRecordResults
    {
        public SubscriptionRecordResult[] value;
    }
}