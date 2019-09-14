// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace CollectSFData
{
    [Serializable]
    public class DtrTraceRecord : ITraceRecord
    {
        private const int _fieldCount = 6;

        public DtrTraceRecord()
        {
        }

        public DtrTraceRecord(string traceRecord, FileObject fileObject, string resourceUri = null)
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

        public ITraceRecord Populate(FileObject fileObject, string dtrRecord, string resourceUri = null)
        {
            string[] fields = ParseRecord(dtrRecord);

            Timestamp = Convert.ToDateTime(fields[0]);
            Level = fields[1];
            TID = Convert.ToInt32(fields[2]);
            PID = Convert.ToInt32(fields[3]);
            Type = fields[4];
            Text = fields[5];
            NodeName = fileObject.NodeName;
            FileType = fileObject.FileDataType.ToString();
            RelativeUri = fileObject.RelativeUri;
            ResourceUri = resourceUri;

            return this;
        }

        public override string ToString()
        {
            return $"{Timestamp:o},{Level},{TID},{PID},{Type},{Text},{NodeName},{FileType},{RelativeUri},{ResourceUri}{Environment.NewLine}";
        }

        private string[] ParseRecord(string record)
        {
            // format for csv compliance
            // by default the Text field is not quoted, contains commas, contains quotes
            // kusto conforms to csv standards. service fabric dtr.zip (csv file) does not

            string[] newLine = record.Split(new string[] { "," }, _fieldCount, StringSplitOptions.None);
            string additionalCommas = string.Empty;

            if (newLine.Length < _fieldCount)
            {
                additionalCommas = new string(',', _fieldCount - newLine.Length);
            }

            newLine[newLine.Length - 1] = newLine[newLine.Length - 1].Replace("\"", "'").TrimEnd('\r', '\n');
            newLine[newLine.Length - 1] = $"{additionalCommas}\"{newLine[newLine.Length - 1]}\"";
            return newLine;
        }
    }
}