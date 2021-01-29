// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;

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
            Log.Debug($"adding message: file:{fileUri} relative:{relativeUri}");
            Add(new KustoQueueMessage(fileUri, relativeUri, clientRequestId));
        }

        public new bool Contains(KustoQueueMessage message)
        {
            return IndexOf(message) >= 0;
        }

        public bool Contains(string searchItem)
        {
            return IndexOf(searchItem) >= 0;
        }

        public int IndexOf(string searchItem)
        {
            return IndexOf(new KustoQueueMessage(searchItem));
        }

        public new int IndexOf(KustoQueueMessage message)
        {
            foreach (KustoQueueMessage queueMessage in this.Take())
            {
                if (queueMessage.Equals(message))
                {
                    return base.IndexOf(queueMessage);
                }
            }

            return -1;
        }

        public KustoQueueMessage Item(string searchItem)
        {
            int index = IndexOf(searchItem);
            return index >= 0 ? this[index] : null;
        }

        public new bool Remove(KustoQueueMessage message)
        {
            int index = IndexOf(message);
            bool retval = false;

            if (index >= 0)
            {
                retval = RemoveAt(index);
            }

            Log.Debug($"removing message: index:{index} retval:{retval}", message);
            return retval;
        }

        public bool Remove(string searchItem)
        {
            int index = IndexOf(searchItem);
            return index >= 0 ? RemoveAt(index) : false;
        }
    }
}