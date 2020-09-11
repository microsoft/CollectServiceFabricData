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
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace CollectSFData.DataFile
{
    public class StreamManager : Constants
    {
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

            Open();
            _memoryStream.Position = 0;

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
                fileObject.Length = _memoryStream.Length;
                fileObject.Stream.Set(uncompressedStream);

                Log.Debug($"decompressing memoryStream complete. size: {uncompressedStream.Length} position: {uncompressedStream.Position}");
                return uncompressedStream;
            }
        }

        public MemoryStream Get()
        {
            Open();
            _memoryStream.Position = 0;
            return _memoryStream;
        }

        public IList<T> Read<T>()
        {
            Open();
            Log.Debug($"enter: memoryStream length: {_memoryStream.Length}");
            _memoryStream.Position = 0;
            return new BinaryFormatter().Deserialize(_memoryStream) as IList<T>;
        }

        public IList<string> Read()
        {
            return Read<string>();
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
            Open();
            Log.Debug($"enter: memoryStream length: {_memoryStream.Length}");
            _memoryStream.Position = 0;

            using (StreamReader reader = new StreamReader(_memoryStream, Encoding.UTF8))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        public void SaveToFile(string fileUri = null)
        {
            fileUri = fileUri ?? _fileObject.FileUri;
            Open();
            _memoryStream.Position = 0;

            Directory.CreateDirectory(Path.GetDirectoryName(fileUri));
            Log.Info($"writing memoryStream ({_memoryStream.Length} bytes) to file: {fileUri}", ConsoleColor.Green);

            using (FileStream fileStream = File.OpenWrite(fileUri))
            {
                _memoryStream.CopyTo(fileStream);
            }

            _memoryStream.Position = 0;
        }

        public void Set(MemoryStream stream)
        {
            if (_memoryStream != stream)
            {
                _memoryStream = stream;
            }
        }

        public void Set(byte[] byteArray)
        {
            if (_memoryStream.CanWrite && byteArray.Length <= _memoryStream.Length)
            {
                _memoryStream.SetLength(byteArray.Length);
                _memoryStream.Position = 0;
                _memoryStream.Write(byteArray, 0, byteArray.Length);
            }
            else
            {
                _memoryStream = new MemoryStream(byteArray);
            }
        }

        public MemoryStream Write<T>(IList<T> records)
        {
            Log.Debug($"enter: record length: {records.Count}");
            _memoryStream = null;
            Open();
            new BinaryFormatter().Serialize(_memoryStream, records);
            return _memoryStream;
        }

        private MemoryStream Open()
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
            }

            return _memoryStream;
        }
    }
}