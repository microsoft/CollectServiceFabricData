// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;

namespace CollectSFData.DataFile
{
    public class FileObject : IEqualityComparer
    {
        private readonly string _fileDataTypesPattern = string.Join("|", Enum.GetNames(typeof(FileDataTypesEnum)));
        private FileStatus _fileObjectStatus = FileStatus.unknown;
        private string _fileUri = string.Empty;
        private string _nodePattern = string.Empty;

        public string BaseUri { get; set; } = string.Empty;

        public Action DownloadAction { get; set; }

        public bool Exists { get => File.Exists(FileUri); }

        public FileDataTypesEnum FileDataType { get; set; }

        public FileExtensionTypesEnum FileExtensionType { get => FileTypes.MapKnownFileExtension(FileUri); }

        public FileExtensionTypesEnum FileExtensionSubType { get => FileTypes.MapKnownFileExtension(Path.GetFileNameWithoutExtension(FileUri)); }

        public FileTypesEnum FileType { get => FileTypes.MapFileTypeUri(FileUri); }

        public string FileUri { get => _fileUri; set => ExtractProperties(value); }

        public FileUriTypesEnum FileUriType { get => FileTypes.MapFileUriType(FileUri); }

        public bool IsPopulated { get => !string.IsNullOrEmpty(FileUri) | !string.IsNullOrEmpty(BaseUri); }

        public DateTimeOffset LastModified { get; set; }

        public long Length { get => (long)Stream?.Length; }

        public string MessageId { get; set; }

        public string NodeName { get; private set; } = FileDataTypesEnum.unknown.ToString();

        public int RecordCount { get; set; }

        public string RelativeUri { get => Regex.Replace(_fileUri ?? "", BaseUri ?? "", "", RegexOptions.IgnoreCase).TrimStart('/'); }

        public FileStatus Status
        {
            get
            {
                Log.Debug($"fileobject:status:get:{_fileObjectStatus}:{RelativeUri}");
                return _fileObjectStatus;
            }
            set
            {
                _fileObjectStatus = value;
                Log.Debug($"fileobject:status:set:{_fileObjectStatus}:{RelativeUri}");
            }
        }

        public StreamManager Stream { get; set; }

        public FileObject(string fileUri = null, string baseUri = null)
        {
            Stream = new StreamManager(this);
            BaseUri = baseUri;
            FileUri = fileUri;
        }

        public bool Equals(FileObject fileObject)
        {
            return Equals(this, fileObject);
        }

        public new bool Equals(object self, object comparable)
        {
            if (self == null | comparable == null)
            {
                Log.Debug("both args null");
                return false;
            }

            if (!(self is FileObject) | !(comparable is FileObject))
            {
                Log.Debug("at least one object not FileObject");
                return false;
            }

            FileObject qSelf = self as FileObject;
            FileObject qComparable = comparable as FileObject;

            if (Compare(qSelf.MessageId, qComparable.MessageId))
            {
                Log.Debug("ClientRequestId match", comparable);
                return true;
            }

            if (Compare(qSelf.FileUri, qComparable.FileUri))
            {
                Log.Debug("FileUri match", comparable);
                return true;
            }

            if (Compare(qSelf.RelativeUri, qComparable.RelativeUri))
            {
                Log.Debug("RelativeUri match", comparable);
                return true;
            }

            Log.Debug("no match: self:", self);
            Log.Debug("no match: comparable:", comparable);
            return false;
        }

        public int GetHashCode(object obj)
        {
            int hashCode = (MessageId.GetHashCode() + FileUri.GetHashCode() + RelativeUri.GetHashCode()) / 3;
            Log.Debug($"hashCode {hashCode}");
            return hashCode;
        }

        public bool HasKey(string searchItem)
        {
            return HasKey(this, searchItem);
        }

        public bool IsSourceFileLinkCompliant()
        {
            // csv compliant type files (trace dtr zips)
            // and gather types that use links (gather type exception uses links)
            bool retval = false;
            if (FileType == FileTypesEnum.exception)
            {
                retval = true;
            }
            else if(FileType == FileTypesEnum.trace && FileUriType == FileUriTypesEnum.azureStorageUri
                    && (FileExtensionType == FileExtensionTypesEnum.zip && FileExtensionSubType == FileExtensionTypesEnum.dtr))
            {
                retval = true;
            }

            Log.Debug("exit:{retval}");
            return retval;
        }

        private bool Compare(string self, string comparable)
        {
            if (string.IsNullOrEmpty(self) | string.IsNullOrEmpty(comparable))
            {
                return false;
            }

            self = self.ToLower().TrimEnd(Constants.ZipExtension.ToCharArray()).TrimEnd(Constants.CsvExtension.ToCharArray());
            comparable = comparable.ToLower().TrimEnd(Constants.ZipExtension.ToCharArray()).TrimEnd(Constants.CsvExtension.ToCharArray());

            if (self.EndsWith(comparable) | comparable.EndsWith(self))
            {
                Log.Debug("full match", comparable);
                return true;
            }

            if (self.EndsWith(Path.GetFileName(comparable)) | comparable.EndsWith(Path.GetFileName(self)))
            {
                Log.Debug("partial (file name) match", comparable);
                return true;
            }

            return false;
        }

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

            if (!string.IsNullOrEmpty(BaseUri) & Uri.IsWellFormedUriString(fileUri, UriKind.Relative))
            {
                if (!fileUri.ToLower().StartsWith(BaseUri.ToLower()))
                {
                    fileUri = BaseUri.TrimEnd('/') + "/" + fileUri.TrimStart('/');
                    Log.Debug($"concatenated baseUri + fileUri:{fileUri}");
                }
            }

            _fileUri = fileUri;
            if (!string.IsNullOrEmpty(fileUri))
            {
                Log.Info($"extracted node properties:node:{NodeName} filetype:{FileDataType.ToString()}\r\n relativeUri:{RelativeUri}", ConsoleColor.Cyan);
            }
            else
            {
                Log.Debug($"extracted node properties:node:{NodeName} filetype:{FileDataType.ToString()}\r\n relativeUri:{RelativeUri}");
            }

            return fileUri;
        }

        private bool HasKey(FileObject self, string searchItem)
        {
            if (Compare(self.MessageId, searchItem))
            {
                Log.Debug("ClientRequestId match", searchItem);
                return true;
            }

            if (Compare(self.FileUri, searchItem))
            {
                Log.Debug("FileUri match", searchItem);
                return true;
            }

            if (Compare(self.RelativeUri, searchItem))
            {
                Log.Debug("RelativeUri match", searchItem);
                return true;
            }

            return false;
        }
    }
}