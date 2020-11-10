// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace CollectSFData.Common
{
    public class LogMessage : EventArgs
    {
        public ConsoleColor? BackgroundColor { get; set; }
        public ConsoleColor? ForegroundColor { get; set; }
        public bool IsError { get; set; }
        public string Message { get; set; }
        public string TimeStamp { get; set; }
    }
}