// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.RetryPolicies;
using System;

namespace CollectSFData.Common
{
    public class IngestRetryPolicy : Constants, IRetryPolicy
    {
        IRetryPolicy IRetryPolicy.CreateInstance()
        {
            return new IngestRetryPolicy();
        }

        bool IRetryPolicy.ShouldRetry(int currentRetryCount, int statusCode, Exception lastException, out TimeSpan retryInterval, OperationContext operationContext)
        {
            Log.Exception($"ingest retry policy retry count: {currentRetryCount} exception: {lastException}");
            retryInterval = new TimeSpan(0, 0, 1);

            if (currentRetryCount < RetryCount)
            {
                return true;
            }

            return false;
        }
    }
}