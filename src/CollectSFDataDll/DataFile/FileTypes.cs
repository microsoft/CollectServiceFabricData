﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CollectSFData.DataFile
{
    // do not modify order without testing MapFileDataTypeUri
    public enum FileDataTypesEnum
    {
        fabric,
        lease,
        counter,
        table,
        fabricsetup,
        fabricdeployer,
        bootstrap,
        data,
        fabriccrashdumps,
        sfextlog,
        unknown
    }

    public enum FileExtensionTypesEnum
    {
        unknown,
        csv,
        blg,
        dmp,
        dtr,
        etl,
        json,
        log,
        trace,
        zip
    }

    public enum FileTypesEnum
    {
        unknown,
        any,
        counter,
        exception,
        sfextlog,
        setup,
        trace,
        table
    }

    public enum FileUriTypesEnum
    {
        unknown,
        azureStorageUri,
        fileUri,
        httpUri,
        azureKeyVaultUri
    }

    public static class FileTypes
    {
        private static readonly string[] _fileDataTypes = Enum.GetNames(typeof(FileDataTypesEnum));

        public static FileDataTypesEnum MapFileDataTypeExtension(string fileUri)
        {
            string file = fileUri?.ToLower();
            FileDataTypesEnum extension = FileDataTypesEnum.unknown;

            if (file == null)
            {
            }
            else if (fileUri.EndsWith(Constants.PerfCsvExtension) | fileUri.EndsWith(Constants.PerfCtrExtension))
            {
                extension = FileDataTypesEnum.counter;
            }
            else if (fileUri.EndsWith(Constants.TableExtension))
            {
                extension = FileDataTypesEnum.table;
            }
            else if (fileUri.EndsWith(Constants.DumpExtension))
            {
                extension = FileDataTypesEnum.fabriccrashdumps;
            }
            else if (fileUri.EndsWith(Constants.ZipExtension) | fileUri.EndsWith(Constants.DtrExtension))
            {
                // using default fabric / lease
                extension = FileDataTypesEnum.fabric;
            }
            else if (fileUri.EndsWith(Constants.SetupExtension))
            {
                // using default fabricsetup / fabricdeployer
                extension = FileDataTypesEnum.fabricsetup;
            }
            else if (fileUri.EndsWith(Constants.LogExtension))
            {
                extension = FileDataTypesEnum.sfextlog;
            }
            else if (fileUri.EndsWith(Constants.ZipExtension))
            {
                // todo: implement
                extension = FileDataTypesEnum.unknown;
            }
            else if (fileUri.EndsWith(Constants.JsonExtension))
            {
                // todo: implement
                extension = FileDataTypesEnum.unknown;
            }
            else if (fileUri.EndsWith(Constants.EtlExtension))
            {
                // todo: implement
                extension = FileDataTypesEnum.unknown;
            }
            else if (fileUri.EndsWith(Constants.CsvExtension))
            {
                // todo: implement
                extension = FileDataTypesEnum.unknown;
            }

            Log.Debug($"returning {extension}");
            return extension;
        }

        public static FileDataTypesEnum MapFileDataTypeUri(string fileUri)
        {
            FileDataTypesEnum fileDataType = FileDataTypesEnum.unknown;

            bool found = false;

            foreach (string dataType in _fileDataTypes)
            {
                string dataTypePattern = $@"(\.|_)(?<fileType>{dataType})s?(\.|_|-)";

                if (Regex.IsMatch(fileUri, dataTypePattern, RegexOptions.IgnoreCase))
                {
                    Match match = Regex.Match(fileUri, dataTypePattern, RegexOptions.IgnoreCase);
                    fileDataType = (FileDataTypesEnum)Enum.Parse(typeof(FileDataTypesEnum), dataType);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                foreach (string dataType in _fileDataTypes)
                {
                    string dataTypePattern = $@"(\\|/)(?<fileType>{dataType})s?(\\|/|-|_)";

                    if (Regex.IsMatch(fileUri, dataTypePattern, RegexOptions.IgnoreCase))
                    {
                        Match match = Regex.Match(fileUri, dataTypePattern, RegexOptions.IgnoreCase);
                        fileDataType = (FileDataTypesEnum)Enum.Parse(typeof(FileDataTypesEnum), dataType);
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                // try by just file extension
                fileDataType = MapFileDataTypeExtension(fileUri);
            }

            if (fileDataType == FileDataTypesEnum.unknown)
            {
                Log.Warning($"unable to determine datatypepattern:{fileUri} using values:{string.Join(",", _fileDataTypes)}");
            }

            Log.Debug($"returning FileDataTypesEnum.{fileDataType.ToString()}");
            return fileDataType;
        }

        public static string MapFileTypeRelativeUriPrefix(FileTypesEnum fileType)
        {
            string knownPrefix = string.Empty;

            switch (fileType)
            {
                case FileTypesEnum.any:
                    knownPrefix = FileTypesKnownUrisPrefix.any;
                    Log.Warning($"returning FileTypesKnownUrisPrefix.{knownPrefix}");
                    break;

                case FileTypesEnum.counter:
                    knownPrefix = FileTypesKnownUrisPrefix.fabriccounter;
                    break;

                case FileTypesEnum.exception:
                    knownPrefix = FileTypesKnownUrisPrefix.fabriccrashdump;
                    break;

                case FileTypesEnum.sfextlog:
                    knownPrefix = FileTypesKnownUrisPrefix.extension;
                    break;

                case FileTypesEnum.setup:
                case FileTypesEnum.table:
                case FileTypesEnum.trace:
                    knownPrefix = FileTypesKnownUrisPrefix.fabriclog;
                    break;

                default:
                    knownPrefix = FileTypesKnownUrisPrefix.unknown;
                    Log.Warning($"returning FileTypesKnownUrisPrefix.{knownPrefix}");
                    break;
            }

            Log.Debug($"returning FileTypesKnownUrisPrefix.{knownPrefix}");
            return knownPrefix;
        }

        public static FileTypesEnum MapFileTypeUri(string fileUri)
        {
            FileTypesEnum fileTypesEnum = FileTypesEnum.unknown;

            switch (MapFileDataTypeUri(fileUri))
            {
                case FileDataTypesEnum.bootstrap:
                case FileDataTypesEnum.fabricdeployer:
                case FileDataTypesEnum.fabricsetup:
                    {
                        fileTypesEnum = FileTypesEnum.setup;
                        break;
                    }
                case FileDataTypesEnum.data:
                case FileDataTypesEnum.fabriccrashdumps:
                    {
                        fileTypesEnum = FileTypesEnum.exception;
                        break;
                    }
                case FileDataTypesEnum.counter:
                    {
                        fileTypesEnum = FileTypesEnum.counter;
                        break;
                    }
                case FileDataTypesEnum.fabric:
                case FileDataTypesEnum.lease:
                    {
                        fileTypesEnum = FileTypesEnum.trace;
                        break;
                    }
                case FileDataTypesEnum.table:
                    {
                        fileTypesEnum = FileTypesEnum.table;
                        break;
                    }
                case FileDataTypesEnum.sfextlog:
                    {
                        fileTypesEnum = FileTypesEnum.sfextlog;
                        break;
                    }
                default:
                    {
                        fileTypesEnum = FileTypesEnum.any;
                        Log.Warning($"unknown filetype: {fileUri}");
                        break;
                    }
            }

            Log.Debug($"returning {fileTypesEnum}");
            return fileTypesEnum;
        }

        public static FileUriTypesEnum MapFileUriType(string fileUri)
        {
            Log.Debug($"enter:{fileUri}");

            FileUriTypesEnum fileUriTypesEnum = FileUriTypesEnum.unknown;

            if (string.IsNullOrEmpty(fileUri))
            {
            }
            else if (fileUri.ToLower().StartsWith("http"))
            {
                fileUriTypesEnum = FileUriTypesEnum.httpUri;

                if (fileUri.ToLower().Contains(Constants.AzureStorageSuffix))
                {
                    fileUriTypesEnum = FileUriTypesEnum.azureStorageUri;
                }

                if (fileUri.ToLower().Contains(Constants.AzureKeyVaultSuffix))
                {
                    fileUriTypesEnum = FileUriTypesEnum.azureKeyVaultUri;
                }
            }
            else
            {
                if (fileUri.IndexOfAny(Path.GetInvalidPathChars()) < 0
                    && Path.GetPathRoot(fileUri).Length > 0 | fileUri.StartsWith(".")
                    && Path.GetFileName(fileUri).IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                {
                    fileUriTypesEnum = FileUriTypesEnum.fileUri;
                }
            }

            Log.Debug($"returning {fileUriTypesEnum}");
            return fileUriTypesEnum;
        }

        public static FileExtensionTypesEnum MapKnownFileExtension(string fileUri)
        {
            FileExtensionTypesEnum extension = FileExtensionTypesEnum.unknown;
            switch (Path.GetExtension(fileUri.ToLower()))
            {
                case Constants.PerfCtrExtension:
                    {
                        extension = FileExtensionTypesEnum.blg;
                        break;
                    }

                case Constants.CsvExtension:
                case Constants.PerfCsvExtension:
                case Constants.TableExtension:
                    {
                        extension = FileExtensionTypesEnum.csv;
                        break;
                    }

                case Constants.DumpExtension:
                    {
                        extension = FileExtensionTypesEnum.dmp;
                        break;
                    }

                case Constants.EtlExtension:
                    {
                        extension = FileExtensionTypesEnum.etl;
                        break;
                    }
                case Constants.DtrExtension:
                    {
                        extension = FileExtensionTypesEnum.dtr;
                        break;
                    }

                case Constants.JsonExtension:
                    {
                        extension = FileExtensionTypesEnum.json;
                        break;
                    }

                case Constants.LogExtension:
                    {
                        extension = FileExtensionTypesEnum.log;
                        break;
                    }

                case Constants.SetupExtension:
                    {
                        extension = FileExtensionTypesEnum.trace;
                        break;
                    }

                case Constants.ZipExtension:
                    {
                        extension = FileExtensionTypesEnum.zip;
                        break;
                    }

                default:
                    {
                        extension = FileExtensionTypesEnum.unknown;
                        break;
                    }
            }

            Log.Debug($"returning {extension}");
            return extension;
        }
    }

    public class FileTypesKnownUrisPrefix
    {
        public static string any = "";
        public static string extension = "vmextlog";
        public static string fabriccounter = "fabriccounters";
        public static string fabriccrashdump = "fabriccrashdumps";
        public static string fabriclog = "fabriclogs";
        public static string unknown = "unknown";
    }
}