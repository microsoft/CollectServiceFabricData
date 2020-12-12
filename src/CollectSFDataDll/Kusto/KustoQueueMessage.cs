// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using System;
using System.Collections;

namespace CollectSFData.Kusto
{
    public class KustoQueueMessages : SynchronizedList<KustoQueueMessage>
    {
        public KustoQueueMessages(KustoQueueMessages messages = null)
        {
            if (messages != null)
            {
                this.AddRange(messages);
            }
        }

        public void Add(string fileUri = null, string relativeUri = null, string clientRequestId = null)
        {
            Add(new KustoQueueMessage(fileUri, relativeUri, clientRequestId));
        }

        public new bool Contains(KustoQueueMessage message)
        {
            return IndexOf(message) >= 0;
        }

        public bool Contains(string item)
        {
            return IndexOf(item) >= 0;
        }

        public int IndexOf(string item)
        {
            return IndexOf(new KustoQueueMessage(item, item, item));
        }

        public new int IndexOf(KustoQueueMessage message)
        {
            foreach (KustoQueueMessage queueMessage in new KustoQueueMessages(this))
            {
                if (queueMessage.Equals(message))
                {
                    return base.IndexOf(queueMessage);
                }
            }

            return -1;
        }

        public KustoQueueMessage Item(string item)
        {
            int index = IndexOf(item);
            if (index >= 0)
            {
                return this[index];
            }
            
            return null;
        }

        public new bool Remove(KustoQueueMessage message)
        {
            int index = IndexOf(message);
            if (index >= 0)
            {
                return RemoveAt(index);
            }

            return false;
        }

        public bool Remove(string item)
        {
            int index = IndexOf(item);
            if (index >= 0)
            {
                return RemoveAt(index);
            }

            return false;
        }
    }

    public class KustoQueueMessage : Constants, IEqualityComparer
    {
        public DateTime Completed { get; set; } //= DateTime.MaxValue;
        public string ClientRequestId { get; set; } //= string.Empty;
        public string FileUri { get; set; } //= string.Empty;
        public string RelativeUri { get; set; } //= string.Empty;
        public DateTime Started { get; set; } //= DateTime.MaxValue;
        public KustoQueueMessage(string fileUri = null, string relativeUri = null, string clientRequestId = null)
        {
            Started = DateTime.Now;
            ClientRequestId = clientRequestId;
            FileUri = fileUri;
            RelativeUri = relativeUri;
        }

        public bool CompareStrings(string self, string comparable)
        {
            if (!string.IsNullOrEmpty(self) & !string.IsNullOrEmpty(comparable))
            {
                if (self.Contains(comparable.TrimEnd(ZipExtension.ToCharArray())) | comparable.Contains(self.TrimEnd(ZipExtension.ToCharArray())))
                {
                    Log.Debug("match", comparable);
                    return true;
                }
            }

            return false;
        }

        public bool Equals(KustoQueueMessage message)
        {
            return Equals(this, message);
        }

        public new bool Equals(object self, object comparable)
        {
            if (self == null | comparable == null)
            {
                Log.Debug("both args null");
                return false;
            }

            if (!(self is KustoQueueMessage) | !(comparable is KustoQueueMessage))
            {
                Log.Debug("at least one object not KustoQueueMessage");
                return false;
            }

            KustoQueueMessage qSelf = self as KustoQueueMessage;
            KustoQueueMessage qComparable = comparable as KustoQueueMessage;

            if (CompareStrings(qSelf.ClientRequestId, qComparable.ClientRequestId))
            {
                Log.Debug("ClientRequestId match", comparable);
                return true;
            }

            if (CompareStrings(qSelf.FileUri, qComparable.FileUri))
            {
                Log.Debug("FileUri match", comparable);
                return true;
            }

            if (CompareStrings(qSelf.RelativeUri, qComparable.RelativeUri))
            {
                Log.Debug("RelativeUri match", comparable);
                return true;
            }

            Log.ToFile("no match: self:", self);
            Log.ToFile("no match: comparable:", comparable);
            return false;
        }

        public int GetHashCode(object obj)
        {
            int hashCode = ClientRequestId.GetHashCode() + FileUri.GetHashCode() + RelativeUri.GetHashCode();
            Log.Debug($"hashCode {hashCode}");
            return hashCode;
        }
    }
}
