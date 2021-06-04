// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace CollectSFData.DataFile
{
    public class FileObjectCollection : SynchronizedList<FileObject>
    {
        private object _collectionLock = new object();

        public FileObjectCollection() : base(new List<FileObject>())
        {
        }

        public new bool Add(FileObject fileObject)
        {
            return AddRange(new FileObject[] { fileObject });
        }

        public bool AddRange(FileObject[] fileObjects)
        {
            int exists = 0;

            lock (_collectionLock)
            {
                foreach (FileObject fileObject in fileObjects)
                {
                    FileObject existingFileObject = FindByUriFirstOrDefault(fileObject.RelativeUri);
                    if (!existingFileObject.IsPopulated)
                    {
                        base.Add(fileObject);
                        continue;
                    }
                    else if (existingFileObject.Status != fileObject.Status)
                    {
                        existingFileObject.Status = fileObject.Status;
                    }

                    exists++;
                }
            }

            if (exists > 0)
            {
                Log.Warning($"{exists} fileobject(s) already exist");
            }
            else
            {
                Log.Debug($"{fileObjects.Length} fileobject(s) added");
            }

            return exists == 0;
        }

        public bool Any(FileStatus fileObjectStatus = FileStatus.all)
        {
            bool retval = this.Any(x => !x.Status.Equals(FileStatus.unknown) && fileObjectStatus.HasFlag(x.Status));
            Log.Debug($"returning:{retval}");
            return retval;
        }

        public new int Count(FileStatus fileObjectStatus)
        {
            int count = this.Count(x => !x.Equals(FileStatus.unknown) && fileObjectStatus.HasFlag(x.Status));
            Log.Debug($"returning:count:{count}", fileObjectStatus);
            return count;
        }

        public List<FileObject> FindAll(FileStatus fileObjectStatus = FileStatus.all)
        {
            List<FileObject> results = new List<FileObject>();
            results.AddRange(this.FindAll(x => !x.Status.Equals(FileStatus.unknown) && fileObjectStatus.HasFlag(x.Status)));
            return results;
        }

        public FileObject FindByMessageId(string searchItem)
        {
            foreach (FileObject queuedObject in this.Take())
            {
                if (queuedObject.HasKey(searchItem))
                {
                    Log.Debug($"returning fileObject with fileUri:{queuedObject.FileUri}");
                    return queuedObject;
                }
            }

            Log.Error($"unable to find fileObject by key searchItem:{searchItem}");
            return new FileObject();
        }

        public FileObject FindByUriFirstOrDefault(string searchItem)
        {
            foreach (FileObject queuedObject in this.Take())
            {
                if (queuedObject.HasKey(searchItem))
                {
                    Log.Debug($"returning fileObject with fileUri:{queuedObject.FileUri}");
                    return queuedObject;
                }
            }

            Log.Debug($"unable to find fileObject by key searchItem:{searchItem}");
            return new FileObject();
        }

        public bool HasFileUri(string searchItem)
        {
            return FindByUriFirstOrDefault(searchItem).IsPopulated;
        }

        public int Pending()
        {
            return (Count() - Count(FileStatus.succeeded | FileStatus.existing));
        }

        public string StatusString()
        {
            StringBuilder display = new StringBuilder();
            display.Append("FileObjects:status:");

            foreach (FileStatus status in Enum.GetValues(typeof(FileStatus)))
            {
                // since all is not a real status, populate with total count
                int statusCount = 0;
                if (status == FileStatus.all)
                {
                    statusCount = this.Count();
                }
                else if (status == FileStatus.unknown)
                {
                    statusCount = this.Count(x => x.Status.Equals(status));
                }
                else
                {
                    statusCount = this.Count(x => !x.Status.Equals(FileStatus.unknown) && status.HasFlag(x.Status));
                }

                display.Append($"{status}:{statusCount} ");
            }

            Log.Debug(display.ToString());
            return display.ToString();
        }
    }
}