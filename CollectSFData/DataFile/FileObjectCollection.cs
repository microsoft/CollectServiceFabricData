// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using System.Collections.Generic;

namespace CollectSFData.DataFile
{
    public class FileObjectCollection : SynchronizedList<FileObject>
    {
        public FileObjectCollection() : base(new List<FileObject>())
        {
        }
    }
}