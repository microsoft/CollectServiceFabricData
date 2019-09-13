// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CollectSFData
{
    public class CustomTaskScheduler : TaskScheduler
    {
        private readonly ConfigurationOptions _cfg;
        private readonly int _minThreadPoolCount = 2;

        public CustomTaskScheduler(ConfigurationOptions configurationOptions)
        {
            _cfg = configurationOptions;
        }

        public int DelegatesQueuedOrRunning { get; set; }

        public SynchronizedList<Task> Tasks { get; set; } = new SynchronizedList<Task>();

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return Tasks;
        }

        protected override void QueueTask(Task task)
        {
            Log.Debug($"cts queue task {task.Id}");
            Tasks.Add(task);
            ++DelegatesQueuedOrRunning;

            if (DelegatesQueuedOrRunning < Math.Max(_minThreadPoolCount, _cfg.Threads * Constants.MinThreadMultiplier))
            {
                NotifyThreadPoolOfPendingWork();
            }
        }

        protected override bool TryDequeue(Task task)
        {
            Log.Debug($"cts dequeue task {task.Id}");

            Tasks.Remove(task);
            return true;
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // dont run task on thread scheduling custom tasks
            return false;
        }

        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                while (true)
                {
                    Task item;
                    lock (Tasks)
                    {
                        if (!Tasks.Any())
                        {
                            break;
                        }

                        item = Tasks.DeListAt(0);
                        --DelegatesQueuedOrRunning;
                    }

                    Log.Debug($"cts tp task execute {item.Id}");
                    bool retval = TryExecuteTask(item);

                    if (retval)
                    {
                        Log.Debug($"cts tp task execute finished {item.Id} return: {retval}");
                    }
                    else
                    {
                        Log.Warning($"cts tp task execute finished {item.Id} return: {retval}", item);
                    }
                }
            }, null);
        }
    }
}