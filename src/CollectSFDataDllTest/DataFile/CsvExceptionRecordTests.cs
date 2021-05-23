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
    public class CsvExceptionRecordTests
    {
        private string _fileType = "FabricHost";
        private string _fileUri = "https://sflogsxxxxxxxxxx.blob.core.windows.net/fabriccrashdumps-12345678-1234-12234-1234-123456789012/_nt0_0/FabricHost.1196.dmp";
        private string _fileUriNoPid = "https://sflogsxxxxxxxxxx.blob.core.windows.net/fabriccrashdumps-12345678-1234-12234-1234-123456789012/_nt0_0/FabricHost.dmp";
        private DateTime _lastModified = DateTime.Now.ToUniversalTime();
        private int _pid = 1196;

        [Test()]
        public void CsvExceptionRecordTest()
        {
            CsvExceptionRecord csvExceptionRecord = new CsvExceptionRecord();
            Assert.IsNotNull(csvExceptionRecord);
        }

        [Test()]
        public void CsvExceptionRecordTest1()
        {
            FileObject fileObject = new FileObject(_fileUri);
            CsvExceptionRecord csvExceptionRecord = new CsvExceptionRecord(fileObject.FileUri, fileObject);
            Assert.IsNotNull(csvExceptionRecord);
        }

        [Test()]
        public void PopulateTest()
        {
            FileObject fileObject = new FileObject(_fileUri);
            CsvExceptionRecord csvExceptionRecord = new CsvExceptionRecord();
            Assert.IsNotNull(csvExceptionRecord);

            csvExceptionRecord.Populate(fileObject, _fileUri);
            Assert.IsTrue(csvExceptionRecord.PID.Equals(_pid));

            csvExceptionRecord.Populate(fileObject, Path.GetFileName(_fileUri));
            Assert.IsTrue(csvExceptionRecord.Type.Equals(_fileType));
            Assert.IsTrue(csvExceptionRecord.PID.Equals(_pid));

            csvExceptionRecord.Populate(fileObject, _fileUriNoPid);
            Assert.IsTrue(csvExceptionRecord.Type.Equals(_fileType));
            Assert.IsTrue(csvExceptionRecord.PID.Equals(0));
        }

        [Test()]
        public void ToStringTest()
        {
            FileObject fileObject = new FileObject(_fileUri)
            {
                LastModified = _lastModified
            };

            CsvExceptionRecord csvExceptionRecord = new CsvExceptionRecord(fileObject.FileUri, fileObject);
            Assert.IsNotNull(csvExceptionRecord);
            string output = csvExceptionRecord.ToString();
            Assert.IsTrue(output.StartsWith($"{_lastModified:o},{_pid},{_fileType},{_fileUri}"), $"output: {output}");
        }
    }
}