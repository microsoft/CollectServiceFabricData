// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;

namespace CollectSFData.Common
{
    public class SynchronizedList<T> : List<T>, IEnumerable<T>
    {
        private readonly ReaderWriterLockSlim _rwl = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public SynchronizedList()
        {
        }

        public SynchronizedList(List<T> items) : base(items)
        {
        }

        public new void Add(T item)
        {
            _rwl.EnterWriteLock();

            try
            {
                base.Add(item);
            }
            finally
            {
                _rwl.ExitWriteLock();
            }
        }

        public bool AddUnique(T item, bool failIfExists = false)
        {
            if (!Exists(x => x.Equals(item)))
            {
                _rwl.EnterWriteLock();

                try
                {

                    base.Add(item);
                }
                finally
                {
                    _rwl.ExitWriteLock();
                }
            }
            else
            {
                if (failIfExists)
                {
                    Log.Warning("item exists", item);
                    return false;
                }
            }

            return true;
        }

        public bool Any()
        {
            return Count() > 0;
        }

        public bool Any(Predicate<T> predicate)
        {
            return Exists(predicate);
        }

        public new void Clear()
        {
            _rwl.EnterWriteLock();

            try
            {
                base.Clear();
            }
            finally
            {
                _rwl.ExitWriteLock();
            }
        }

        public new int Count()
        {
            _rwl.EnterReadLock();

            try
            {
                return base.Count;
            }
            finally
            {
                _rwl.ExitReadLock();
            }
        }

        public new int Count(Predicate<T> predicate)
        {
            _rwl.EnterReadLock();

            try
            {
                return base.FindAll(predicate).Count;
            }
            catch
            {
                return 0;
            }
            finally
            {
                _rwl.ExitReadLock();
            }
        }

        public IEnumerable<T> DeListAll()
        {
            _rwl.EnterWriteLock();

            try
            {
                IEnumerable<T> items = new List<T>(this);
                Clear();
                return items;
            }
            finally
            {
                _rwl.ExitWriteLock();
            }
        }

        public T DeListAt(int index)
        {
            _rwl.EnterWriteLock();

            try
            {
                T item = default(T);

                if (base.Count > index)
                {
                    item = base[index];
                    base.RemoveAt(index);
                }
                else
                {
                    throw new ArgumentOutOfRangeException();
                }

                return item;
            }
            finally
            {
                _rwl.ExitWriteLock();
            }
        }

        public new bool Exists(Predicate<T> predicate)
        {
            _rwl.EnterReadLock();

            try
            {
                return base.Exists(predicate);
            }
            finally
            {
                _rwl.ExitReadLock();
            }
        }

        public new T Find(Predicate<T> predicate)
        {
            _rwl.EnterReadLock();

            try
            {
                return base.Find(predicate);
            }
            catch
            {
                return default(T);
            }
            finally
            {
                _rwl.ExitReadLock();
            }
        }

        public new List<T> FindAll(Predicate<T> predicate)
        {
            _rwl.EnterReadLock();

            try
            {
                return base.FindAll(predicate);
            }
            finally
            {
                _rwl.ExitReadLock();
            }
        }

        public new void ForEach(Action<T> action)
        {
            _rwl.EnterReadLock();

            try
            {
                base.ForEach(action);
            }
            finally
            {
                _rwl.ExitReadLock();
            }
        }

        public new IEnumerator<T> GetEnumerator()
        {
            _rwl.EnterReadLock();

            try
            {
                return base.GetEnumerator();
            }
            finally
            {
                _rwl.ExitReadLock();
            }
        }

        public new int IndexOf(T item)
        {
            _rwl.EnterReadLock();

            try
            {
                return Array.IndexOf<T>(ToArray(), item);
            }
            finally
            {
                _rwl.ExitReadLock();
            }
        }

        public new void Remove(T item)
        {
            _rwl.EnterWriteLock();

            try
            {
                if (Exists(x => x.Equals(item)))
                {
                    base.Remove(item);
                }
                else
                {
                    Log.Warning("item not found", item);
                }
            }
            finally
            {
                _rwl.ExitWriteLock();
            }
        }

        public new int RemoveAll(Predicate<T> predicate)
        {
            _rwl.EnterWriteLock();

            try
            {
                return base.RemoveAll(predicate);
            }
            finally
            {
                _rwl.ExitWriteLock();
            }
        }

        public new bool RemoveAt(int index)
        {
            _rwl.EnterWriteLock();

            try
            {
                if (base.Count > index)
                {
                    base.RemoveAt(index);
                }
                else
                {
                    throw new ArgumentOutOfRangeException();
                }

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                _rwl.ExitWriteLock();
            }
        }

        public IEnumerable<T> Take(int count = int.MaxValue)
        {
            _rwl.EnterReadLock();

            try
            {
                for (int i = 0; i < Math.Min(count, Count()); i++)
                {
                    yield return this[i];
                }
            }
            finally
            {
                _rwl.ExitReadLock();
            }
        }

        public IEnumerable<T> Where(Predicate<T> predicate)
        {
            _rwl.EnterReadLock();

            try
            {
                return base.FindAll(predicate);
            }
            catch
            {
                return default(IEnumerable<T>);
            }
            finally
            {
                _rwl.ExitReadLock();
            }
        }
    }
}