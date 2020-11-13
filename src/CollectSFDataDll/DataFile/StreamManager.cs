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
        public bool LeaveStreamOpen { get; set; } = true;
        private FileObject _fileObject;
        private MemoryStream _memoryStream = new MemoryStream();

        public StreamManager(FileObject fileObject = null)
        {
            _fileObject = fileObject;
        }

        public void Close()
        {
            if (_memoryStream.CanRead || _memoryStream.CanWrite)
            {
                _memoryStream.Close();
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
            fileObject.Length = _memoryStream.Length;

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
            char[] trimChars = new char[] { '[', ',', ']' };

            using (StreamReader reader = new StreamReader(_memoryStream, Encoding.UTF8, false, StreamBufferSize, LeaveStreamOpen))
            {
                if (LeaveStreamOpen)
                {
                    while (reader.Peek() >= 0)
                    {
                        T record = JsonConvert.DeserializeObject<T>(reader.ReadLine().Trim(trimChars));

                        if (record != null)
                        {
                            yield return record;
                        }
                    }
                }
                else
                {
                    foreach (T record in JsonConvert.DeserializeObject<List<T>>(reader.ReadToEnd()))
                    {
                        yield return record;
                    };
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
            _memoryStream = null;
            Open();
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

            using (StreamReader reader = new StreamReader(_memoryStream, Encoding.UTF8, false, StreamBufferSize, LeaveStreamOpen))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        private void ResetPosition()
        {
            if (_memoryStream != null)
            {
                _memoryStream.Position = 0;
            }
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
            if (_memoryStream != stream)
            {
                _memoryStream = stream;
            }

            SetFileObjectLength();
        }

        public void Set(byte[] byteArray)
        {
            Open(true);
            _memoryStream.Write(byteArray, 0, byteArray.Length);
            SetFileObjectLength();
        }

        public MemoryStream Write<T>(T record)
        {
            return Write<T>(new List<T>(){record});
        }
        
        public MemoryStream Write<T>(IList<T> records)
        {
            Log.Debug($"enter: record length: {records.Count}");

            if (LeaveStreamOpen)
            {
                Open();

                if (_memoryStream.Position > 0)
                {
                    byte[] lastByte = new byte[1];
                    _memoryStream.Position--;
                    _memoryStream.Read(lastByte, 0, 1);

                    string lastChar = Encoding.UTF8.GetString(lastByte);

                    Log.Info($"last character:{lastChar}");

                    if (lastChar.Equals("]"))
                    {
                        _memoryStream.Position--;
                    }
                }
            }
            else
            {
                _memoryStream = null;
                Open();
                _fileObject.RecordCount = 0;
            }

            _fileObject.RecordCount += records.Count;

            using (StreamWriter writer = new StreamWriter(_memoryStream, Encoding.UTF8, StreamBufferSize, LeaveStreamOpen))
            {
                if (_memoryStream.Position == 0)
                {
                    writer.Write("[");
                }

                if (LeaveStreamOpen)
                {
                    foreach (T record in records)
                    {
                        writer.Write($"{JsonConvert.SerializeObject(record)},{Environment.NewLine}");
                    }
                }
                else
                {
                    writer.Write($"{JsonConvert.SerializeObject(records)}{Environment.NewLine}");
                }

                writer.Write("]");
            }

            SetFileObjectLength();
            return _memoryStream;
        }

        private MemoryStream Open(bool resetPosition = false)
        {
            if (_memoryStream == null)
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
                    ResetPosition();
                }
            }

            SetFileObjectLength();
            return _memoryStream;
        }

        private void SetFileObjectLength()
        {
            _fileObject.Length = _memoryStream.Length;
        }
    }
}