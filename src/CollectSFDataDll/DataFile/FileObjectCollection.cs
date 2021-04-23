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
        public FileObjectCollection() : base(new List<FileObject>())
        {
        }

        public bool HasFileUri(string searchItem)
        {
            return FindByUri(searchItem) == null;
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

        public FileObject FindByUri(string searchItem)
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

        public List<FileObject> FindAll(FileStatus fileObjectStatus = FileStatus.all)
        {
            List<FileObject> results = new List<FileObject>();
            StringBuilder display = new StringBuilder();

            foreach (FileStatus status in Enum.GetValues(typeof(FileStatus)))
            {
                if (CompareStatus(status, fileObjectStatus))
                {
                    results.AddRange(this.FindAll(x => x.Status == status));
                }
            }

            return results;
        }

        public bool Any(FileStatus fileObjectStatus = FileStatus.all)
        {
            foreach (FileStatus status in Enum.GetValues(typeof(FileStatus)))
            {
                if (CompareStatus(status, fileObjectStatus))
                {
                    if (this.Any(x => x.Status == status))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool CompareStatus(FileStatus current, FileStatus filter)
        {
            bool retval = false;

            if (filter == FileStatus.all | (current & filter) == filter)
            {
                retval = true;
            }

            Log.Debug($"returning:{retval} = {current} == {filter}");
            return retval;
        }

        public string StatusString(FileStatus fileObjectStatus = FileStatus.all)
        {
            StringBuilder display = new StringBuilder();

            foreach (FileStatus status in Enum.GetValues(typeof(FileStatus)))
            {
                if (CompareStatus(status, fileObjectStatus))
                {
                    // since all is not a real status, populate with total count
                    int statusCount = 0;
                    if(status == FileStatus.all)
                    {
                        statusCount = this.Count();
                    }
                    else
                    {
                        statusCount = this.Count(x => x.Status == status);
                    }

                    display.Append($"{status}:{statusCount} ");
                }
            }

            Log.Debug(display.ToString());
            return display.ToString();
        }

        public new int Count(FileStatus fileObjectStatus)
        {
            int count = 0;
            foreach (FileStatus status in Enum.GetValues(typeof(FileStatus)))
            {
                if (CompareStatus(status, fileObjectStatus))
                {
                    int statusCount = this.Count(x => x.Status == status);
                    count += statusCount;
                }
            }

            Log.Debug($"returning:count:{count}", fileObjectStatus);
            return count;
        }
    }
}