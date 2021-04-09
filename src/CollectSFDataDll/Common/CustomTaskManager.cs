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
    public class CustomTaskManager : Constants
    {
        private static SynchronizedList<CustomTaskManager> _allInstances = new SynchronizedList<CustomTaskManager>();
        private static CustomTaskScheduler _customScheduler;

        // init in constructor after starting _taskMonitor to avoid exception
        private static Instance _instance;

        private static bool _isRunning;
        private static Task _taskMonitor = new Task(TaskMonitor);
        private static object _taskMonLock = new object();
        private static ConfigurationOptions Config;
        private string CallerName;
        public static CancellationTokenSource CancellationTokenSource { get; private set; } = new CancellationTokenSource();
        public SynchronizedList<Task> AllTasks { get; set; } = new SynchronizedList<Task>();

        public TaskContinuationOptions ContinuationOptions { get; set; } = TaskContinuationOptions.OnlyOnRanToCompletion;

        public TaskCreationOptions CreationOptions { get; set; } = TaskCreationOptions.PreferFairness;

        public bool IsCancellationRequested { get => CancellationTokenSource.IsCancellationRequested; }

        public SynchronizedList<TaskObject> QueuedTaskObjects { get; set; } = new SynchronizedList<TaskObject>();

        public bool RemoveWhenComplete { get; set; }

        static CustomTaskManager()
        {
        }

        public CustomTaskManager(bool removeWhenComplete = false, [CallerMemberName] string callerName = "")
        {
            RemoveWhenComplete = removeWhenComplete;
            CallerName = callerName;

            Log.Debug($"{CallerName} waiting on lock. taskmonitor status: {_taskMonitor.Status}", ConsoleColor.White);
            lock (_taskMonLock)
            {
                Log.Debug($"{CallerName} in lock. taskmonitor status: {_taskMonitor.Status}", ConsoleColor.White);

                if (!_isRunning && _taskMonitor.Status == TaskStatus.Created)
                {
                    _isRunning = true;
                    Log.Highlight($"{CallerName} starting taskmonitor. status: {_taskMonitor.Status}", ConsoleColor.White);
                    _instance = Instance.Singleton();
                    Config = _instance.Config;
                    _customScheduler = new CustomTaskScheduler(Config);
                    _taskMonitor.Start();
                    Log.Highlight($"{CallerName} started taskmonitor. status: {_taskMonitor.Status}", ConsoleColor.White);
                }

                if (_taskMonitor.Status != TaskStatus.RanToCompletion)
                {
                    Log.Info($"{CallerName} adding task instance. taskmonitor status: {_taskMonitor.Status}", ConsoleColor.White);
                    _allInstances.Add(this);
                }
                else
                {
                    Log.Error("taskmonitor already completed. not adding task");
                }
            }
        }

        public static void Cancel()
        {
            Log.Info("taskmanager cancelling", ConsoleColor.White);
            CancellationTokenSource.Cancel();
            _taskMonitor.Wait();
            _isRunning = false;
            Log.Info("taskmanager cancelled", ConsoleColor.White);
        }

        public static void Resume()
        {
            if (!_isRunning)
            {
                Log.Info("taskmanager resuming", ConsoleColor.White);
                _allInstances.Clear();
                CancellationTokenSource = new CancellationTokenSource();
                _taskMonitor = new Task(TaskMonitor);
                _taskMonitor.Start();
                Log.Info("taskmanager resumed", ConsoleColor.White);
                _isRunning = true;
            }
        }

        public static void WaitAll()
        {
            // dont block all instances
            _allInstances.ToList().ForEach(x => x.Wait());
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

                if (Config.LogDebug >= LoggingLevel.Verbose)
                {
                    instance.AllTasks.ForEach(x => taskUpdate += $"\r\n  ID:{x.Id.ToString().PadRight(6)} {x.Status.ToString().PadRight(15)} ({x.CreationOptions.ToString()})");
                }

                Log.Info(taskUpdate, ConsoleColor.White);

                foreach (Task badTask in instance.AllTasks.FindAll(x => x.Status > TaskStatus.RanToCompletion))
                {
                    Log.Error("task failure: ", badTask);
                    _instance.TotalErrors++;
                }

                instance.AllTasks.RemoveAll(x => x.IsCompleted);
            }

            while ((instance.CreationOptions == TaskCreationOptions.AttachedToParent | !instance.IsAboveQuota()) && instance.QueuedTaskObjects.Any())
            {
                if (CancellationTokenSource.IsCancellationRequested)
                {
                    Log.Info("cancellation requested:removing all queued tasks.");
                    instance.QueuedTaskObjects.Clear();
                    break;
                }

                TaskObject taskObject = instance.QueuedTaskObjects[0];
                instance.ScheduleTask(taskObject);
                instance.QueuedTaskObjects.Remove(taskObject);

                Log.Info($"scheduled task {instance.GetHashCode()} " +
                    $"instance:{instance.CallerName} " +
                    $"total:{instance.AllTasks.Count()} " +
                    $"incomplete:{instance.AllTasks.Count(x => !x.IsCompleted)} " +
                    $"queued:{instance.QueuedTaskObjects.Count()} " +
                    $"GC:{GC.GetTotalMemory(false)} " +
                    $"total records:{_instance.TotalRecords} " +
                    $"rps:{_instance.TotalRecords / (DateTime.Now - _instance.StartTime).TotalSeconds}", ConsoleColor.Magenta);
            }

            return instance.AllTasks.Count(x => !x.IsCompleted);
        }

        private static void TaskMonitor()
        {
            while (true)
            {
                int incompleteTasks = 0;
                _allInstances.ForEach(x => incompleteTasks += ManageTasks(x));

                if (CancellationTokenSource.IsCancellationRequested && incompleteTasks == 0)
                {
                    Log.Info("stopping monitor. cancellation requested.", ConsoleColor.White);
                    return;
                }

                if (CancellationTokenSource.IsCancellationRequested)
                {
                    Log.Debug("cancel requested, but there are incomplete tasks");
                }

                Thread.Sleep(ThreadSleepMs10);
            }
        }

        private Task AddToQueue(TaskObject taskObject, bool taskWait = false)
        {
            int workerThreads = 0;
            int completionPortThreads = 0;
            int count = 0;

            if (CancellationTokenSource.IsCancellationRequested)
            {
                return taskObject.Task;
            }

            while (taskWait && workerThreads < (Config.Threads * MinThreadMultiplier))
            {
                ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);
                Thread.Sleep(ThreadSleepMs10);
                count++;
            }

            QueuedTaskObjects.Add(taskObject);
            Log.Info($"added new taskobject to queue: {CallerName} throttle ms: {count * 10}");
            TimeSpan delay = new TimeSpan();

            if (taskWait)
            {
                taskObject.TaskScheduled.WaitOne();
            }

            Log.Debug($"added new taskobject to queue:{CallerName} delay:{delay.TotalMilliseconds}ms");
            return taskObject.Task;
        }

        private bool IsAboveQuota()
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
            Task task = Task.Factory.StartNew(action, state, CancellationTokenSource.Token, CreationOptions, _customScheduler);

            AllTasks.Add(task);
            Log.Debug($"schedule action state task scheduled {task.Id}");
            return task;
        }

        private Task ScheduleTaskAction(TaskObject taskObject)
        {
            bool hasContinueWith = taskObject.ContinueWith != null;
            Log.Debug($"scheduling action continuewith:{hasContinueWith}");
            Task task = Task.Factory.StartNew(taskObject.Action, CancellationTokenSource.Token, CreationOptions, _customScheduler);

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
            Task<object> task = Task.Factory.StartNew(taskObject.Func, null, CancellationTokenSource.Token, CreationOptions, _customScheduler);

            if (hasContinueWith)
            {
                AllTasks.Add(task.ContinueWith(taskObject.ContinueWith, ContinuationOptions));
            }

            Log.Debug($"scheduling action func scheduled:{task.Id} continuewith:{hasContinueWith} ");
            AllTasks.Add(task);
            return task;
        }
    }
}