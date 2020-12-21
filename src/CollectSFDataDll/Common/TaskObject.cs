// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace CollectSFData.Common
{
    public class TaskObject
    {
        public Action Action { get; set; }
        public Action<object> ActionObject { get; set; }
        public Action<Task> ContinueWith { get; set; }
        public Func<object, object> Func { get; set; }
        public Task Task { get; set; }
        public ManualResetEvent TaskScheduled { get; set; } = new ManualResetEvent(false);
    }
}