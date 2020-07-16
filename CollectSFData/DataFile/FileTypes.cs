// ------------------------------------------------------------
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
        unknown
    }

    public enum FileExtensionTypesEnum
    {
        unknown,
        csv,
        blg,
        dmp,
        dtr,
        json,
        trace,
        zip
    }

    public enum FileTypesEnum
    {
        unknown,
        any,
        counter,
        exception,
        setup,
        trace,
        table
    }

    public static class FileTypes
    {
        private static readonly string[] _fileDataTypes = Enum.GetNames(typeof(FileDataTypesEnum));

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
                Log.Warning($"unable to determine datatypepattern:{fileUri} using values:{string.Join(",", _fileDataTypes)}");
            }

            Log.Debug($"returning FileDataTypesEnum.{fileDataType.ToString()}");
            return fileDataType;
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

        public static string MapFileTypeUriPrefix(FileTypesEnum fileType)
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

                case Constants.TraceFileExtension:
                    {
                        extension = FileExtensionTypesEnum.dtr;
                        break;
                    }

                case Constants.JsonExtension:
                    {
                        extension = FileExtensionTypesEnum.json;
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
        public static string fabriccounter = "fabriccounter";
        public static string fabriccrashdump = "fabriccrashdump";
        public static string fabriclog = "fabriclog";
        public static string unknown = "unknown";
    }
}