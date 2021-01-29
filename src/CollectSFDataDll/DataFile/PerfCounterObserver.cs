using CollectSFData;
using CollectSFData.Common;
using System;
using System.Collections.Generic;
using Tx.Windows;

namespace CollectSFData.DataFile
{
    public class PerfCounterObserver<T> : IObserver<T>, IDisposable
    {
        public bool Complete;
        public List<PerformanceSample> Records = new List<PerformanceSample>();

        public PerfCounterObserver()
        {
        }

        public void Dispose()
        {
            Log.Debug("disposed");
        }

        public void OnCompleted()
        {
            Complete = true;
        }

        public void OnError(Exception error)
        {
            Log.Exception($"{error}");
        }

        public void OnNext(T value)
        {
            if (value is PerformanceSample)
            {
                Records.Add(value as PerformanceSample);
                return;
            }
            else
            {
                Log.Error("value not PerformanceSample");
            }
        }
    }
}