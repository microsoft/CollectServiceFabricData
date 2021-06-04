// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace CollectSFData.DataFile
{
    public struct EtlInputFields
    {
        public static int Level = 1;
        public static int PID = 3;
        public static int Text = 5;
        public static int TID = 2;
        public static int TimeStamp = 0;
        public static int Type = 4;

        public static int Count() => 6;
    }
}