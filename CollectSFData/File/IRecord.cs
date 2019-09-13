// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace CollectSFData
{
    public interface IRecord
    {
        string RelativeUri { get; set; }

        string ResourceUri { get; set; }

        ITraceRecord Populate(FileObject fileObject, string dtrRecord, string resourceUri = null);

        string ToString();
    }
}