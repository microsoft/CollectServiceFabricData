// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace CollectSFData.DataFile
{
    [Flags]
    public enum FileStatus
    {
        unknown,
        downloading,
        uploading,
        succeeded,
        failed,
        enumerated,
        queued,
        formatting,
        existing,
        all,
    }
}