// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Text.RegularExpressions;

namespace CollectSFData.DataFile
{
    [Serializable]
    public class LogExtensionRecord : ITraceRecord
    {
        private const int _fieldCount = 4;
        private const string _pidPattern = @"\[(?<pid>\d+?):\d+?\]";
        private const string _timePattern = @"(?<time>[0-9]{2,4}(-|/)[0-9]{1,2}(-|/)[0-9]{1,2}(-|T)[0-9]{1,2}:[0-9]{1,2}:[0-9]{1,2}\.[0-9]{1,3}):\d+?";
        private const string _levelPattern = @"\[(?<level>\w+?)\]";
        private const string _typePattern = @"(\s+?-\s+?){0,1}(?<type>\w+?|)";
        private string _eventPattern = $@"^{_pidPattern} {_timePattern} {_levelPattern} {_typePattern} - (?<text>.+)";

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

        public LogExtensionRecord()
        {
        }

        public LogExtensionRecord(string traceRecord, FileObject fileObject, string resourceUri = null)
        {
            Populate(fileObject, traceRecord, resourceUri);
        }

        public IRecord Populate(FileObject fileObject, string traceRecord, string resourceUri = null)
        {
            // format for csv compliance
            // kusto conforms to csv standards. service fabric trace (csv file) does not
            // [3104:5] 2022-07-29T14:38:52.233:637947023322330619 [INFO] Utility - Starting process sc.exe with arguments create FabricInstallerSvc binPath="\"C:\Program Files\Microsoft Service Fabric\FabricInstallerService.Code\FabricInstallerService.exe\"" DisplayName="Service Fabric Installer Service" .. 

            Match matchResult = Regex.Match(traceRecord, _eventPattern);

            if (matchResult.Success)
            {
                Timestamp = Convert.ToDateTime(matchResult.Groups["time"].Value);
                Level = matchResult.Groups["level"].Value;
                PID = Convert.ToInt32(matchResult.Groups["pid"].Value);
                Type = matchResult.Groups["type"].Value;
                Text = "\"" + matchResult.Groups["text"].Value.TrimStart('\"').TrimEnd('\"', '\r', '\n').Replace("\"", "'") + "\"";
                NodeName = fileObject.NodeName;
                FileType = fileObject.FileDataType.ToString();
                RelativeUri = fileObject.RelativeUri;
                ResourceUri = resourceUri;
            }
            
            return this;
        }

        public override string ToString()
        {
            return $"{Timestamp:o},{Level},{PID},{Type},{Text},{NodeName},{FileType},{RelativeUri},{ResourceUri}{Environment.NewLine}";
        }
    }
}