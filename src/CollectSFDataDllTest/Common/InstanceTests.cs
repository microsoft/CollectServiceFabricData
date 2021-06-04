using NUnit.Framework;
using CollectSFData.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectSFData.Common.Tests
{
    [TestFixture()]
    public class InstanceTests
    {
        [Test()]
        public void CloseTest()
        {
            Instance instance = new Instance();
            instance.Close();
            Assert.IsFalse(CustomTaskManager.IsRunning);
        }

        [Test()]
        public void InitializeTest()
        {
            DateTime startTime = DateTime.Now;
            Instance instance = new Instance();
            Assert.IsNotNull(instance);
            Assert.IsTrue(CustomTaskManager.IsRunning);
            Assert.IsTrue(instance.StartTime > startTime);
        }

        [Test()]
        public void InstanceTest()
        {
            Instance instance = new Instance();
            Assert.IsNotNull(instance);
        }
    }
}