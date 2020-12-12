// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace CollectSFData.DataFile
{
    public class StreamManager : Constants
    {
        public int StreamBufferSize { get; set; } = 1024;
        private bool _leaveStreamOpen { get; set; } = true;
        public long Length => (long)Open().Length;
        private FileObject _fileObject;
        private MemoryStream _memoryStream = new MemoryStream();

        public StreamManager(FileObject fileObject = null)
        {
            _fileObject = fileObject;
        }

        public void Dispose()
        {
            if (_memoryStream.CanRead || _memoryStream.CanWrite)
            {
                _memoryStream.Dispose();
            }
        }

        public MemoryStream Compress(FileObject fileObject = null)
        {
            fileObject = fileObject ?? _fileObject;

            if (_fileObject.FileExtensionType.Equals(FileExtensionTypesEnum.zip))
            {
                string error = $"{_fileObject.FileUri} is already zip file";
                Log.Error(error);
                throw new ArgumentException();
            }

            Open(true);
            Log.Debug($"compressing memoryStream start. start size: {fileObject.Length} dest size: {_memoryStream.Length} position: {_memoryStream.Position}");
            MemoryStream compressedStream = new MemoryStream();

            using (ZipArchive archive = new ZipArchive(compressedStream, ZipArchiveMode.Create, true))
            {
                ZipArchiveEntry entry = archive.CreateEntry(Path.GetFileName(_fileObject.FileUri));

                using (Stream archiveStream = entry.Open())
                {
                    _memoryStream.CopyTo(archiveStream);
                }
            }

            compressedStream.Position = 0;
            _fileObject.FileUri += ZipExtension;
            _fileObject.Stream.Set(compressedStream);

            Log.Debug($"compressing memoryStream complete. size: {compressedStream.Length} position: {compressedStream.Position}");
            return compressedStream;
        }

        public MemoryStream Decompress(FileObject fileObject = null)
        {
            fileObject = fileObject ?? _fileObject;

            if (!fileObject.FileExtensionType.Equals(FileExtensionTypesEnum.zip))
            {
                string error = $"{fileObject.FileUri} not a zip file";
                Log.Error(error);
                throw new ArgumentException();
            }

            fileObject.FileUri = Regex.Replace(fileObject.FileUri, ZipExtension, "", RegexOptions.IgnoreCase);
            Open();

            Log.Debug($"decompressing memoryStream start. start size: {_memoryStream.Length} position: {_memoryStream.Position}");

            MemoryStream uncompressedStream = new MemoryStream();

            using (ZipArchive archive = new ZipArchive(_memoryStream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    Log.Debug($"zip entry:{entry.FullName}");
                    entry.Open().CopyTo(uncompressedStream);
                }

                uncompressedStream.Position = 0;
                fileObject.Stream.Set(uncompressedStream);

                Log.Debug($"decompressing memoryStream complete. size: {uncompressedStream.Length} position: {uncompressedStream.Position}");
                return uncompressedStream;
            }
        }

        public MemoryStream Get()
        {
            Open(true);
            return _memoryStream;
        }

        public IEnumerable<T> Read<T>()
        {
            Open(true);
            Log.Debug($"enter: memoryStream length: {_memoryStream.Length}");
            char[] trimChars = new char[] { '[', ']', ',' };

            using (StreamReader reader = new StreamReader(_memoryStream, Encoding.UTF8, false, StreamBufferSize, _leaveStreamOpen))
            {
                if (_leaveStreamOpen)
                {
                    while (reader.Peek() >= 0)
                    {
                        string stringRecord = reader.ReadLine().Trim(trimChars);
                        T record = JsonConvert.DeserializeObject<T>(stringRecord);

                        if (record != null)
                        {
                            yield return record;
                        }
                    }
                }
                else
                {
                    T[] records;

                    try
                    {
                        records = JsonConvert.DeserializeObject<T[]>(reader.ReadToEnd());
                    }
                    catch
                    {
                        Log.Debug("exception deserializing T[] array, trying as T");
                        ResetPosition();
                        records = new T[] { JsonConvert.DeserializeObject<T>(reader.ReadToEnd()) };
                    }

                    foreach (T record in records)
                    {
                        if (record != null)
                        {
                            yield return record;
                        }
                    }
                }
            }
        }

        public IList<string> Read()
        {
            return ReadLine().ToList();
        }

        public MemoryStream ReadFromFile(string fileUri = null)
        {
            fileUri = fileUri ?? _fileObject.FileUri;
            Open(true, true);
            Log.Info($"reading memoryStream from file: {fileUri}", ConsoleColor.Green);

            using (FileStream fileStream = File.OpenRead(fileUri))
            {
                fileStream.CopyTo(_memoryStream);
            }

            return _memoryStream;
        }

        public IEnumerable<string> ReadLine()
        {
            Open(true);
            Log.Debug($"enter: memoryStream length: {_memoryStream.Length}");

            using (StreamReader reader = new StreamReader(_memoryStream, Encoding.UTF8, false, StreamBufferSize, _leaveStreamOpen))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        public string ReadToEnd()
        {
            Open(true);
            return new StreamReader(_memoryStream).ReadToEnd();
        }
        
        public void ResetPosition()
        {
            Open(true);
        }

        public void SaveToFile(string fileUri = null)
        {
            fileUri = fileUri ?? _fileObject.FileUri;
            Open(true);

            Directory.CreateDirectory(Path.GetDirectoryName(fileUri));
            Log.Info($"writing memoryStream ({_memoryStream.Length} bytes) to file: {fileUri}", ConsoleColor.Green);

            using (FileStream fileStream = File.OpenWrite(fileUri))
            {
                _memoryStream.CopyTo(fileStream);
            }

            ResetPosition();
        }

        private void Set(MemoryStream stream)
        {
            Open(true, true);
            _memoryStream = stream;
        }

        public void Set(byte[] byteArray)
        {
            Open(true, true);
            _memoryStream.SetLength(byteArray.Length);
            _memoryStream.Write(byteArray, 0, byteArray.Length);
        }

        public MemoryStream Write<T>(IList<T> records, bool append = false)
        {
            Log.Debug($"enter: record length: {records.Count}");

            if (_leaveStreamOpen && append)
            {
                Open();

                if (_memoryStream.Position > 0)
                {
                    byte[] lastByte = new byte[1];
                    _memoryStream.Position--;
                    _memoryStream.Read(lastByte, 0, 1);

                    string lastChar = Encoding.UTF8.GetString(lastByte);

                    Log.Debug($"last character:{lastChar}");

                    if (lastChar.Equals("]"))
                    {
                        _memoryStream.Position--;
                    }
                }
            }
            else
            {
                Open(true, true);
                _fileObject.RecordCount = 0;
            }

            _fileObject.RecordCount += records.Count;

            using (StreamWriter writer = new StreamWriter(_memoryStream, Encoding.UTF8, StreamBufferSize, _leaveStreamOpen))
            {
                if (_leaveStreamOpen)
                {
                    if (_memoryStream.Position == 0 && _memoryStream.Length == 0)
                    {
                        writer.Write("[");
                    }

                    foreach (T record in records)
                    {
                        writer.Write($"{JsonConvert.SerializeObject(record)},{Environment.NewLine}");
                    }

                    writer.Write("]");
                }
                else
                {
                    writer.Write($"{JsonConvert.SerializeObject(records)}{Environment.NewLine}");
                }


            }

            return _memoryStream;
        }

        private MemoryStream Open(bool resetPosition = false, bool reset = false)
        {
            if (_memoryStream == null | reset)
            {
                _memoryStream = new MemoryStream();
                Log.Debug("creating new stream");
            }
            else if (!_memoryStream.CanRead && !_memoryStream.CanWrite)
            {
                _memoryStream = new MemoryStream(_memoryStream.ToArray());
                Log.Debug($"opening stream. stream length: {_memoryStream.Length}");
            }
            else
            {
                Log.Debug($"stream already open. stream length: {_memoryStream.Length}");

                if (resetPosition)
                {
                    _memoryStream.Position = 0;
                }
            }

            return _memoryStream;
        }
    }
}