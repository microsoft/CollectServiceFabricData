// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Text.RegularExpressions;

namespace CollectSFData
{
    [Serializable]
    public class CsvExceptionRecord : ITraceRecord
    {
        private const int _fieldCount = 3;

        public CsvExceptionRecord()
        {
        }

        public CsvExceptionRecord(string traceRecord, FileObject fileObject, string resourceUri = null)
        {
            Populate(fileObject, traceRecord, resourceUri);
        }

        public string FileType { get; set; }

        public string Level { get; set; }

        public string NodeName { get; set; }

        public int PID { get; set; }

        public string RelativeUri { get; set; }

        public string ResourceUri { get; set; }

        public string Text { get; set; }

        public int TID { get; set; }

        public DateTime Timestamp { get; set; }

        public string Type { get; set; }

        public IRecord Populate(FileObject fileObject, string traceRecord, string resourceUri = null)
        {
            Match match = Regex.Match(traceRecord, @"/(?<Type>\w+?)(\.exe)?\.(?<PID>\d+?)\.dmp");

            Timestamp = fileObject.LastModified.UtcDateTime;
            PID = Convert.ToInt32(match.Groups["PID"].Value);
            Type = match.Groups["Type"].Value;
            Text = traceRecord;
            NodeName = fileObject.NodeName;
            FileType = fileObject.FileDataType.ToString();
            RelativeUri = fileObject.RelativeUri;
            ResourceUri = resourceUri;

            return this;
        }

        public override string ToString()
        {
            return $"{Timestamp:o},{PID},{Type},{Text},{NodeName},{FileType},{RelativeUri},{ResourceUri}{Environment.NewLine}";
        }
    }
}