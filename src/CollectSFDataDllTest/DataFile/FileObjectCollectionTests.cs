using NUnit.Framework;
using CollectSFData.DataFile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace CollectSFData.DataFile.Tests
{
    [TestFixture()]
    public class FileObjectCollectionTests
    {
        [Test()]
        public void AddRangeTest()
        {
            FileObjectCollection foc = new FileObjectCollection();

            foc.AddRange(new FileObject[] { });
            Assert.IsTrue(foc.Count() == 0);
            foc.AddRange(new FileObject[1] { new FileObject() });
            Assert.IsTrue(foc.Count() == 1);
            foc.AddRange(new FileObject[2] { new FileObject(), new FileObject() });
            Assert.IsTrue(foc.Count() == 3);
        }

        [Test()]
        public void AddTest()
        {
            FileObjectCollection foc = new FileObjectCollection();
            foc.Add(new FileObject());
            Assert.IsTrue(foc.Count() == 1);
        }

        [Test()]
        public void AnyTest()
        {
            FileObjectCollection foc = new FileObjectCollection();
            Assert.IsFalse(foc.Any());

            foc.Add(new FileObject());
            Assert.IsTrue(foc.Any());
        }

        [Test()]
        public void AnyTestAll()
        {
            FileStatus fsAllOptions = FileStatus.uploading | FileStatus.downloading | FileStatus.enumerated | FileStatus.existing | FileStatus.failed | FileStatus.formatting | FileStatus.queued | FileStatus.succeeded;
            FileObjectCollection foc = new FileObjectCollection();
            Assert.IsFalse(foc.Any(fsAllOptions));

            foc.Add(new FileObject());
            Assert.IsFalse(foc.Any(fsAllOptions));

            foc.Add(new FileObject() { Status = FileStatus.unknown });
            Assert.IsFalse(foc.Any(fsAllOptions));

            foc.Add(new FileObject() { Status = FileStatus.enumerated });
            Assert.IsTrue(foc.Any(fsAllOptions));

            foc.Clear();
            Assert.IsFalse(foc.Any());
            foc.AddRange(new FileObject[4]
            {
                new FileObject() { Status = FileStatus.uploading },
                new FileObject() { Status = FileStatus.downloading },
                new FileObject() { Status = FileStatus.downloading },
                new FileObject() { Status = FileStatus.failed },
            });
            Assert.IsTrue(foc.Any(fsAllOptions));

            Assert.IsTrue(foc.Count(fsAllOptions) == 4, $"expected:4 got:{foc.Count(fsAllOptions)}");
        }

        [Test()]
        public void AnyTestMultiple()
        {
            FileStatus fsSingleOption = FileStatus.succeeded;
            FileStatus fsMultipleOption = FileStatus.failed | FileStatus.succeeded;
            FileObjectCollection foc = new FileObjectCollection();
            Assert.IsFalse(foc.Any(fsMultipleOption));

            foc.Add(new FileObject());
            Assert.IsFalse(foc.Any(fsMultipleOption));

            foc.Add(new FileObject() { Status = FileStatus.unknown });
            Assert.IsFalse(foc.Any(fsMultipleOption));

            foc.Add(new FileObject() { Status = fsSingleOption });
            Assert.IsTrue(foc.Any(fsMultipleOption));

            foc.Clear();
            Assert.IsFalse(foc.Any());
            foc.AddRange(new FileObject[4]
            {
                new FileObject() { Status = FileStatus.uploading },
                new FileObject() { Status = FileStatus.downloading },
                new FileObject() { Status = FileStatus.downloading },
                new FileObject() { Status = FileStatus.enumerated },
            });
            Assert.IsFalse(foc.Any(fsMultipleOption));

            foc.Clear();
            Assert.IsFalse(foc.Any());
            foc.AddRange(new FileObject[4]
            {
                new FileObject() { Status = FileStatus.uploading },
                new FileObject() { Status = fsSingleOption },
                new FileObject() { Status = FileStatus.failed },
                new FileObject() { Status = FileStatus.downloading },
            });
            Assert.IsTrue(foc.Any(fsMultipleOption));

            Assert.IsTrue(foc.Count(fsMultipleOption) == 2, $"expected:2 got:{foc.Count(fsMultipleOption)}");

            foc.Clear();
            fsMultipleOption = FileStatus.existing | FileStatus.succeeded;
            Assert.IsFalse(foc.Any());
            foc.AddRange(new FileObject[10]
            {
                new FileObject() { Status = FileStatus.uploading },
                new FileObject() { Status = FileStatus.existing },
                new FileObject() { Status = FileStatus.succeeded },
                new FileObject() { Status = FileStatus.succeeded },
                new FileObject() { Status = FileStatus.succeeded },
                new FileObject() { Status = FileStatus.existing },
                new FileObject() { Status = FileStatus.failed },
                new FileObject() { Status = FileStatus.succeeded },
                new FileObject() { Status = FileStatus.succeeded },
                new FileObject() { Status = FileStatus.downloading },
            });
            Assert.IsTrue(foc.Any(fsMultipleOption));

            Assert.IsTrue(foc.Count(fsMultipleOption) == 7, $"expected:7 got:{foc.Count(fsMultipleOption)}");
        }

        [Test()]
        public void AnyTestSingle()
        {
            FileStatus fsSingleOption = FileStatus.failed;
            FileObjectCollection foc = new FileObjectCollection();
            Assert.IsFalse(foc.Any(fsSingleOption));

            foc.Add(new FileObject());
            Assert.IsFalse(foc.Any(fsSingleOption));

            foc.Add(new FileObject() { Status = FileStatus.unknown });
            Assert.IsFalse(foc.Any(fsSingleOption));

            foc.Add(new FileObject() { Status = fsSingleOption });
            Assert.IsTrue(foc.Any(fsSingleOption));

            foc.Clear();
            Assert.IsFalse(foc.Any());
            foc.AddRange(new FileObject[4]
            {
                new FileObject() { Status = FileStatus.uploading },
                new FileObject() { Status = FileStatus.downloading },
                new FileObject() { Status = FileStatus.downloading },
                new FileObject() { Status = FileStatus.enumerated },
            });
            Assert.IsFalse(foc.Any(fsSingleOption));

            foc.Clear();
            Assert.IsFalse(foc.Any());
            foc.AddRange(new FileObject[4]
            {
                new FileObject() { Status = FileStatus.uploading },
                new FileObject() { Status = fsSingleOption },
                new FileObject() { Status = fsSingleOption },
                new FileObject() { Status = FileStatus.downloading },
            });
            Assert.IsTrue(foc.Any(fsSingleOption));

            Assert.IsTrue(foc.Count(fsSingleOption) == 2, $"expected:2 got:{foc.Count(fsSingleOption)}");
        }

        [Test()]
        public void AnyTestStatusAll()
        {
            FileObjectCollection foc = new FileObjectCollection();
            Assert.IsFalse(foc.Any(FileStatus.all));

            foc.Add(new FileObject());
            Assert.IsTrue(foc.Any(FileStatus.all));

            foc.Clear();
            Assert.IsFalse(foc.Any());
            foc.Add(new FileObject() { Status = FileStatus.downloading });
            Assert.IsTrue(foc.Any(FileStatus.all));

            foc.Clear();
            Assert.IsFalse(foc.Any());
            foc.AddRange(new FileObject[2] { new FileObject() { Status = FileStatus.downloading }, new FileObject() { Status = FileStatus.downloading } });
            Assert.IsTrue(foc.Count(FileStatus.all) == 2);

            foc.Clear();
            Assert.IsFalse(foc.Any());
            foc.AddRange(new FileObject[2] { new FileObject() { Status = FileStatus.downloading }, new FileObject() { Status = FileStatus.unknown } });
            Assert.IsTrue(foc.Count(FileStatus.all) == 2);
        }

        [Test()]
        public void FileObjectCollectionTest()
        {
            FileObjectCollection foc = new FileObjectCollection();
            Assert.NotNull(foc);
        }

        [Test()]
        public void FindAllMultipleTest()
        {
            FileStatus fsSingleOption = FileStatus.failed;
            FileStatus fsMultipleOption = FileStatus.failed | FileStatus.succeeded;
            FileObjectCollection foc = new FileObjectCollection();
            Assert.IsFalse(foc.FindAll(fsMultipleOption).Count > 0);

            foc.Add(new FileObject());
            Assert.IsFalse(foc.FindAll(fsMultipleOption).Count > 0);

            foc.Add(new FileObject() { Status = FileStatus.unknown });
            Assert.IsFalse(foc.FindAll(fsMultipleOption).Count > 0);

            foc.Add(new FileObject() { Status = fsSingleOption });
            Assert.IsTrue(foc.FindAll(fsMultipleOption).Count > 0);

            foc.Clear();
            Assert.IsFalse(foc.Any());
            foc.AddRange(new FileObject[4]
            {
                new FileObject() { Status = FileStatus.uploading },
                new FileObject() { Status = FileStatus.downloading },
                new FileObject() { Status = FileStatus.downloading },
                new FileObject() { Status = FileStatus.enumerated },
            });
            Assert.IsFalse(foc.FindAll(fsSingleOption).Count > 0);

            foc.Clear();
            Assert.IsFalse(foc.Any());
            foc.AddRange(new FileObject[4]
            {
                new FileObject() { Status = FileStatus.uploading },
                new FileObject() { Status = fsSingleOption },
                new FileObject() { Status = fsSingleOption },
                new FileObject() { Status = FileStatus.succeeded },
            });
            Assert.IsTrue(foc.Count(fsMultipleOption) == 3, $"expected:3 got:{foc.Count(fsMultipleOption)}");

            Assert.IsTrue(foc.FindAll(fsMultipleOption).Count(x => fsMultipleOption.HasFlag(x.Status)) == 3, $"expected:3 got:{foc.Count(fsMultipleOption)}");
        }

        [Test()]
        public void FindAllSingleTest()
        {
            FileStatus fsSingleOption = FileStatus.failed;
            FileObjectCollection foc = new FileObjectCollection();
            Assert.IsFalse(foc.FindAll(fsSingleOption).Count > 0);

            foc.Add(new FileObject());
            Assert.IsFalse(foc.FindAll(fsSingleOption).Count > 0);

            foc.Add(new FileObject() { Status = FileStatus.unknown });
            Assert.IsFalse(foc.FindAll(fsSingleOption).Count > 0);

            foc.Add(new FileObject() { Status = fsSingleOption });
            Assert.IsTrue(foc.FindAll(fsSingleOption).Count > 0);

            foc.Clear();
            Assert.IsFalse(foc.Any());
            foc.AddRange(new FileObject[4]
            {
                new FileObject() { Status = FileStatus.uploading },
                new FileObject() { Status = FileStatus.downloading },
                new FileObject() { Status = FileStatus.downloading },
                new FileObject() { Status = FileStatus.enumerated },
            });
            Assert.IsFalse(foc.FindAll(fsSingleOption).Count > 0);

            foc.Clear();
            Assert.IsFalse(foc.Any());
            foc.AddRange(new FileObject[4]
            {
                new FileObject() { Status = FileStatus.uploading },
                new FileObject() { Status = fsSingleOption },
                new FileObject() { Status = fsSingleOption },
                new FileObject() { Status = FileStatus.downloading },
            });
            Assert.IsTrue(foc.Count(fsSingleOption) == 2, $"expected:2 got:{foc.Count(fsSingleOption)}");

            Assert.IsTrue(foc.FindAll(fsSingleOption).Count(x => x.Status == fsSingleOption) == 2, $"expected:2 got:{foc.Count(fsSingleOption)}");
        }

        [Test()]
        public void FindByMessageIdTest()
        {
            string testMessageId = Guid.NewGuid().ToString();
            string testFileUri = $"c:/temp/{Path.GetRandomFileName()}";
            FileObjectCollection foc = new FileObjectCollection();
            foc.AddRange(new FileObject[4]
            {
                new FileObject() { Status = FileStatus.uploading, MessageId = null },
                new FileObject() { Status = FileStatus.downloading, MessageId = testMessageId, FileUri = testFileUri },
                new FileObject() { Status = FileStatus.downloading, MessageId = Guid.NewGuid().ToString(), FileUri = $"c:/temp/{Path.GetRandomFileName()}" },
                new FileObject() { Status = FileStatus.failed, MessageId = Guid.NewGuid().ToString(), FileUri = $"c:/temp/{Path.GetRandomFileName()}" },
            });
            Assert.IsTrue(foc.FindByMessageId(testMessageId).MessageId == testMessageId & foc.FindByMessageId(testMessageId).FileUri == testFileUri);
        }

        [Test()]
        public void FindByUriFirstOrDefaultTest()
        {
            string testMessageId = Guid.NewGuid().ToString();
            string testFileUri = $"c:/temp/{Path.GetRandomFileName()}";
            FileObjectCollection foc = new FileObjectCollection();
            foc.AddRange(new FileObject[4]
            {
                new FileObject() { Status = FileStatus.uploading, MessageId = null },
                new FileObject() { Status = FileStatus.downloading, MessageId = testMessageId, FileUri = testFileUri },
                new FileObject() { Status = FileStatus.downloading, MessageId = Guid.NewGuid().ToString(), FileUri = $"c:/temp/{Path.GetRandomFileName()}" },
                new FileObject() { Status = FileStatus.failed, MessageId = Guid.NewGuid().ToString(), FileUri = $"c:/temp/{Path.GetRandomFileName()}" },
            });
            Assert.IsTrue(foc.FindByUriFirstOrDefault(testFileUri).MessageId == testMessageId & foc.FindByUriFirstOrDefault(testFileUri).FileUri == testFileUri);
        }

        [Test()]
        public void HasFileUriTest()
        {
            string testMessageId = Guid.NewGuid().ToString();
            string testFileUri = $"c:/temp/{Path.GetRandomFileName()}";
            FileObjectCollection foc = new FileObjectCollection();
            foc.AddRange(new FileObject[4]
            {
                new FileObject() { Status = FileStatus.uploading, MessageId = null },
                new FileObject() { Status = FileStatus.downloading, MessageId = testMessageId, FileUri = testFileUri },
                new FileObject() { Status = FileStatus.downloading, MessageId = Guid.NewGuid().ToString(), FileUri = $"c:/temp/{Path.GetRandomFileName()}" },
                new FileObject() { Status = FileStatus.failed, MessageId = Guid.NewGuid().ToString(), FileUri = $"c:/temp/{Path.GetRandomFileName()}" },
            });
            Assert.IsTrue(foc.HasFileUri(testFileUri));
        }

        [Test()]
        public void StatusStringTest()
        {
            FileObjectCollection foc = new FileObjectCollection();
            foc.AddRange(new FileObject[9]
            {
                new FileObject() { Status = FileStatus.downloading },
                new FileObject() { Status = FileStatus.enumerated },
                new FileObject() { Status = FileStatus.existing },
                new FileObject() { Status = FileStatus.failed },
                new FileObject() { Status = FileStatus.formatting },
                new FileObject() { Status = FileStatus.queued },
                new FileObject() { Status = FileStatus.succeeded },
                new FileObject() { Status = FileStatus.uploading },
                new FileObject() { Status = FileStatus.unknown },
            });
            string statusString = foc.StatusString();
            Assert.IsTrue(statusString == "FileObjects:status:unknown:1 enumerated:1 existing:1 queued:1 downloading:1 formatting:1 uploading:1 failed:1 succeeded:1 all:9 ");
        }
    }
}