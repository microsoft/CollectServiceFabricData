// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace CollectSFData.DataFile
{
    [Flags]
    public enum FileStatus : int
    {
        unknown = 0,
        enumerated = 1,
        existing = 2,
        queued = 4,
        downloading = 8,
        formatting = 16,
        uploading = 32,
        succeeded = 64,
        failed = 128,
        all = 256,
    }
}