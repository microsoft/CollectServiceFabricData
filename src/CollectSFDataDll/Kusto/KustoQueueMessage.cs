// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using System;
using System.Collections;

namespace CollectSFData.Kusto
{

    public class KustoQueueMessage : Constants, IEqualityComparer
    {
        public string ClientRequestId { get; set; } //= string.Empty;
        public DateTime Failed { get; set; } //= DateTime.MaxValue;
        public string FileUri { get; set; } //= string.Empty;
        public KustoRestRecord KustoRestRecord {get; set;}
        public string RelativeUri { get; set; } //= string.Empty;
        public DateTime Started { get; set; } = DateTime.MinValue;
        public DateTime Succeeded { get; set; } //= DateTime.MaxValue;

        public KustoQueueMessage(string searchItem)
        {
            Initialize(searchItem, searchItem, searchItem);
        }
        public KustoQueueMessage(string fileUri, string relativeUri, string clientRequestId)
        {
            Initialize(fileUri, relativeUri, clientRequestId);
        }

        private void Initialize(string fileUri, string relativeUri, string clientRequestId)
        {
            Started = DateTime.Now;
            ClientRequestId = clientRequestId;
            FileUri = fileUri;
            RelativeUri = relativeUri;
        }

        private bool Compare(string self, string comparable)
        {
            if (!string.IsNullOrEmpty(self) & !string.IsNullOrEmpty(comparable))
            {
                if (self.ToLower().Contains(comparable.ToLower().TrimEnd(ZipExtension.ToCharArray())) 
                    | comparable.ToLower().Contains(self.ToLower().TrimEnd(ZipExtension.ToCharArray())))
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

            if (Compare(qSelf.ClientRequestId, qComparable.ClientRequestId))
            {
                Log.Debug("ClientRequestId match", comparable);
                return true;
            }

            if (Compare(qSelf.FileUri, qComparable.FileUri))
            {
                Log.Debug("FileUri match", comparable);
                return true;
            }

            if (Compare(qSelf.RelativeUri, qComparable.RelativeUri))
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
            int hashCode = (ClientRequestId.GetHashCode() + FileUri.GetHashCode() + RelativeUri.GetHashCode() + Started.GetHashCode()) / 4;
            Log.Debug($"hashCode {hashCode}");
            return hashCode;
        }
    }
}
