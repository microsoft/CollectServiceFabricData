// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CollectSFData.Common
{
    public class CustomTaskManager : Instance
    {
        private static readonly SynchronizedList<CustomTaskManager> _allInstances = new SynchronizedList<CustomTaskManager>();
        private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static readonly Task _taskMonitor = new Task(TaskMonitor);
        private readonly CustomTaskScheduler _customScheduler = new CustomTaskScheduler(Config);
        private string CallerName;

        static CustomTaskManager()
        {
        }

        public CustomTaskManager(bool removeWhenComplete = false, [CallerMemberName] string callerName = "")
        {
            RemoveWhenComplete = removeWhenComplete;
            CallerName = callerName;
            Log.Info($"adding task instance for:{CallerName}", ConsoleColor.White);
            _allInstances.Add(this);

            if (_taskMonitor.Status == TaskStatus.Created)
            {
                _taskMonitor.Start();
            }
        }

        public SynchronizedList<Task> AllTasks { get; set; } = new SynchronizedList<Task>();

        public TaskContinuationOptions ContinuationOptions { get; set; } = TaskContinuationOptions.OnlyOnRanToCompletion;

        public TaskCreationOptions CreationOptions { get; set; } = TaskCreationOptions.PreferFairness;

        public SynchronizedList<TaskObject> QueuedTaskObjects { get; set; } = new SynchronizedList<TaskObject>();

        public bool RemoveWhenComplete { get; set; }

        public static void Close()
        {
            Log.Info("taskmanager closing", ConsoleColor.White);
            _cancellationTokenSource.Cancel();
            _taskMonitor.Wait();
            Log.Info("taskmanager closed", ConsoleColor.White);
        }

        public static void WaitAll()
        {
            // dont block all instances
            _allInstances.ToList().ForEach(x => x.Wait());
        }

        public bool IsAboveQuota()
        {
            int thisIncompleteTaskCount = 0;
            int allIncompleteTaskCount = 0;
            int activeTaskMgrInstances = Math.Max(1, _allInstances.Count(x => x.AllTasks.Any(y => !y.IsCompleted)));

            _allInstances.ForEach(x => allIncompleteTaskCount += x.AllTasks.Count(y => !y.IsCompleted));
            thisIncompleteTaskCount = AllTasks.Count(x => !x.IsCompleted);
            bool retval = (AllTasks.Count(x => !x.IsCompleted) >= (Config.Threads / activeTaskMgrInstances)) & allIncompleteTaskCount >= Config.Threads;

            if (retval)
            {
                Log.Debug($"all instances:{_allInstances.Count()}" +
                    $" active instances:{activeTaskMgrInstances}" +
                    $" instance tasks:{thisIncompleteTaskCount}" +
                    $" all tasks:{allIncompleteTaskCount}" +
                    $" above quota:{retval}");
            }

            return retval;
        }

        public void QueueTaskAction(Action action)
        {
            AddToQueue(new TaskObject() { Action = action });
        }

        public Task TaskAction(Action action)
        {
            return AddToQueue(new TaskObject() { Action = action }, true);
        }

        public Task<object> TaskFunction(Func<object, object> function)
        {
            return AddToQueue(new TaskObject() { Func = function }, true) as Task<object>;
        }

        public void Wait(int milliseconds = 0)
        {
            Log.Debug("taskmanager:enter", CallerName);

            while (QueuedTaskObjects.Any())
            {
                Thread.Sleep(ThreadSleepMs100);
            }

            Task.WaitAll(AllTasks.ToArray());
            Log.Debug("taskmanager:exit", CallerName);
        }

        private static int ManageTasks(CustomTaskManager instance)
        {
            if (instance.RemoveWhenComplete && instance.AllTasks.Any(x => x.IsCompleted))
            {
                string taskUpdate = $"removing completed tasks from instance {instance.CallerName} " +
                    $"scheduled:{instance.AllTasks.Count()} " +
                    $"queued:{instance.QueuedTaskObjects.Count()}";

                if (Config.LogDebug)
                {
                    instance.AllTasks.ForEach(x => taskUpdate += $"\r\n  ID:{x.Id.ToString().PadRight(6)} {x.Status.ToString().PadRight(15)} ({x.CreationOptions.ToString()})");
                }

                Log.Info(taskUpdate, ConsoleColor.White);

                foreach (Task badTask in instance.AllTasks.FindAll(x => x.Status > TaskStatus.RanToCompletion))
                {
                    Log.Error("task failure: ", badTask);
                    TotalErrors++;
                }

                instance.AllTasks.RemoveAll(x => x.IsCompleted);
            }

            while ((instance.CreationOptions == TaskCreationOptions.AttachedToParent | !instance.IsAboveQuota()) && instance.QueuedTaskObjects.Any())
            {
                TaskObject taskObject = instance.QueuedTaskObjects[0];
                instance.ScheduleTask(taskObject);
                instance.QueuedTaskObjects.Remove(taskObject);

                Log.Info($"scheduled task {instance.GetHashCode()} " +
                    $"instance:{instance.CallerName} " +
                    $"total:{instance.AllTasks.Count()} " +
                    $"incomplete:{instance.AllTasks.Count(x => !x.IsCompleted)} " +
                    $"queued:{instance.QueuedTaskObjects.Count()} " +
                    $"GC:{GC.GetTotalMemory(false)} " +
                    $"total records:{TotalRecords} " +
                    $"rps:{TotalRecords / (DateTime.Now - StartTime).TotalSeconds}", ConsoleColor.Magenta);
            }

            return instance.AllTasks.Count(x => !x.IsCompleted);
        }

        private static void TaskMonitor()
        {
            while (true)
            {
                int incompleteTasks = 0;
                _allInstances.ForEach(x => incompleteTasks += ManageTasks(x));

                if (_cancellationTokenSource.IsCancellationRequested && incompleteTasks == 0)
                {
                    Log.Info("stopping monitor. cancellation requested.", ConsoleColor.White);
                    return;
                }

                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    Log.Debug("cancel requested, but there are incomplete tasks");
                }

                Thread.Sleep(ThreadSleepMs10);
            }
        }

        private Task AddToQueue(TaskObject taskObject, bool taskWait = false)
        {
            QueuedTaskObjects.Add(taskObject);
            Log.Debug($"adding new taskobject to queue: {CallerName}");
            TimeSpan delay = new TimeSpan();

            if (taskWait)
            {
                taskObject.TaskScheduled.WaitOne();
            }

            Log.Debug($"added new taskobject to queue:{CallerName} delay:{delay.TotalMilliseconds}ms");
            return taskObject.Task;
        }

        private void ScheduleTask(TaskObject taskObject)
        {
            if (taskObject.Action != null)
            {
                taskObject.Task = ScheduleTaskAction(taskObject);
            }
            else if (taskObject.ActionObject != null)
            {
                taskObject.Task = ScheduleTaskAction(taskObject.ActionObject, new object());
            }
            else if (taskObject.Func != null)
            {
                taskObject.Task = ScheduleTaskFunction(taskObject);
            }
            else
            {
                Log.Error($"invalid taskObject: {CallerName}", taskObject);
            }

            taskObject.TaskScheduled.Set();
            Log.Debug($"schedule task {CallerName} scheduled");
        }

        private Task ScheduleTaskAction(Action<object> action, object state)
        {
            CancellationToken token = new CancellationToken();
            Task task = Task.Factory.StartNew(action, state, token, CreationOptions, _customScheduler);

            AllTasks.Add(task);
            Log.Debug($"schedule action state task scheduled {task.Id}");
            return task;
        }

        private Task ScheduleTaskAction(TaskObject taskObject)
        {
            bool hasContinueWith = taskObject.ContinueWith != null;
            Log.Debug($"scheduling action continuewith:{hasContinueWith}");
            CancellationToken token = new CancellationToken();
            Task task = Task.Factory.StartNew(taskObject.Action, token, CreationOptions, _customScheduler);

            if (hasContinueWith)
            {
                AllTasks.Add(task.ContinueWith(taskObject.ContinueWith, ContinuationOptions));
            }

            Log.Debug($"scheduling action task scheduled:{task.Id} continuewith:{hasContinueWith} ");
            AllTasks.Add(task);
            return task;
        }

        private Task<object> ScheduleTaskFunction(TaskObject taskObject)
        {
            bool hasContinueWith = taskObject.ContinueWith != null;
            Log.Debug("scheduling function state continuewith");
            CancellationToken token = new CancellationToken();
            Task<object> task = Task.Factory.StartNew(taskObject.Func, null, token, CreationOptions, _customScheduler);

            if (hasContinueWith)
            {
                AllTasks.Add(task.ContinueWith(taskObject.ContinueWith, ContinuationOptions));
            }

            Log.Debug($"scheduling action func scheduled:{task.Id} continuewith:{hasContinueWith} ");
            AllTasks.Add(task);
            return task;
        }
    }

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