// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace CollectSFData
{
    [Serializable]
    public class CsvSetupRecord : ITraceRecord
    {
        private const int _fieldCount = 5;

        public CsvSetupRecord()
        {
        }

        public CsvSetupRecord(string traceRecord, FileObject fileObject, string resourceUri = null)
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
            string[] fields = ParseRecord(traceRecord);

            Timestamp = Convert.ToDateTime(fields[0].Replace("-", " "));
            Level = fields[1];
            PID = Convert.ToInt32(fields[2]);
            Type = fields[3];
            Text = fields[4];
            NodeName = fileObject.NodeName;
            FileType = fileObject.FileDataType.ToString();
            RelativeUri = fileObject.RelativeUri;
            ResourceUri = resourceUri;

            return this;
        }

        public override string ToString()
        {
            return $"{Timestamp:o},{Level},{PID},{Type},{Text},{NodeName},{FileType},{RelativeUri},{ResourceUri}{Environment.NewLine}";
        }

        private string[] ParseRecord(string record)
        {
            // format for csv compliance
            // kusto conforms to csv standards. service fabric trace (csv file) does not

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