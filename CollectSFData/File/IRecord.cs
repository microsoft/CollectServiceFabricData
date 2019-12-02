// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace CollectSFData
{
    public interface IRecord
    {
        string RelativeUri { get; set; }

        string ResourceUri { get; set; }

        DateTime Timestamp { get; set; }

        IRecord Populate(FileObject fileObject, string record, string resourceUri = null);

        string ToString();
    }
}