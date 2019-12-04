// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CollectSFData
{
    public class FileObject
    {
        private readonly string _fileDataTypesPattern = string.Join("|", Enum.GetNames(typeof(FileDataTypesEnum)));
        private string _fileUri;
        private string _nodePattern = string.Empty;

        public FileObject(string fileUri = null, string baseUri = null)
        {
            Stream = new StreamManager(this);
            BaseUri = baseUri;
            FileUri = fileUri;
        }

        public string BaseUri { get; set; }

        public Action DownloadAction { get; set; }

        public bool Exists => File.Exists(FileUri);

        public FileDataTypesEnum FileDataType { get; set; }

        public FileExtensionTypesEnum FileExtensionType => FileTypes.MapKnownFileExtension(FileUri);

        public FileTypesEnum FileType => FileTypes.MapFileTypeUri(FileUri);

        public string FileUri { get => _fileUri; set => ExtractProperties(value); }

        public DateTimeOffset LastModified { get; set; }

        public long Length { get; set; }

        public string NodeName { get; private set; } = FileDataTypesEnum.unknown.ToString();

        public string RelativeUri { get; private set; }

        public StreamManager Stream { get; set; }
        
        private void ExtractNodeName(string fileUri)
        {
            // ARM nodename should be surrounded by / or _ or trailing $ and have _  and digit in name
            // standalone nodename should be surrounded by /
            _nodePattern = $@"(/|\.)(?<nodeName>[^/^\.]+?)(/|\.)({_fileDataTypesPattern}|[^/]+?\.dmp)(/|\.|_|$)";

            if (Regex.IsMatch(fileUri, _nodePattern, RegexOptions.IgnoreCase))
            {
                Match match = Regex.Match(fileUri, _nodePattern, RegexOptions.IgnoreCase);
                NodeName = match.Groups["nodeName"].Value;
                Log.Debug($"node name: {NodeName}");
            }
        }

        private string ExtractProperties(string fileUri)
        {
            if (!string.IsNullOrEmpty(fileUri))
            {
                fileUri = FileManager.NormalizePath(fileUri);
                ExtractNodeName(fileUri);
                FileDataType = FileTypes.MapFileDataTypeUri(fileUri);

                if (string.IsNullOrEmpty(NodeName))
                {
                    if (FileDataType != FileDataTypesEnum.table)
                    {
                        Log.Error($"unable to determine nodename:{fileUri} using pattern {_nodePattern}");
                    }
                }
            }

            if (!string.IsNullOrEmpty(BaseUri))
            {
                if (!fileUri.ToLower().StartsWith(BaseUri.ToLower()))
                {
                    fileUri = BaseUri.TrimEnd('/') + "/" + fileUri.TrimStart('/');
                }

                RelativeUri = fileUri.Replace(BaseUri,"").TrimStart('/');
            }

            _fileUri = fileUri;
            Log.Info($"extracted node properties: node: {NodeName}: filetype: {FileDataType.ToString()}\r\n file: {RelativeUri}", ConsoleColor.Cyan);
            return fileUri;
        }
    }
}