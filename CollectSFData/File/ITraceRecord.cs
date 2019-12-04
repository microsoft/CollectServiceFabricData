// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace CollectSFData
{
    public interface ITraceRecord : IRecord
    {
        string FileType { get; set; }

        string Level { get; set; }

        string NodeName { get; set; }

        int PID { get; set; }
        
        string Text { get; set; }

        int TID { get; set; }
        
        string Type { get; set; }
    }
}