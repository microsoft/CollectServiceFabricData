using CollectSFData.Common;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CollectSFData.DataFile
{
    public class TraceObserver<T> : IObserver<T>, IDisposable
    {
        public ManualResetEventSlim Completed = new ManualResetEventSlim(false);
        public int InitialRecordSize { get; set; } = 1000000; // 256mb file has this many
        public List<T> Records { get; private set; }

        public TraceObserver()
        {
            Records = new List<T>(InitialRecordSize);
        }

        public void Dispose()
        {
            Log.Debug("disposed");
        }

        public void OnCompleted()
        {
            Log.Debug("completed");
            Completed.Set();
        }

        public void OnError(Exception error)
        {
            Log.Exception($"{error}");
        }

        public void OnNext(T value)
        {
            Records.Add(value);
            return;
        }
    }
}