using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;
using Google.PowerShell.Common;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class MeasurementProtocolServiceTests
    {
        FakeMeasurementProtocolService fakeService_;

        [TestFixtureSetUp]
        public void SetUpFixture()
        {
            fakeService_ = new FakeMeasurementProtocolService();
        }

        [SetUp]
        public void SetUpTest()
        {
            fakeService_.Reset();
        }

        [Test]
        public void MeasurementProtocolServiceTests_TheFakeWorks()
        {
            // Test the basic case: publish events and that they can be validated.
            for (int i = 0; i < 50; i++)
            {
                // TODO(chrsmith): Is C#'s new "label-${i}" syntax supported yet?
                string label = String.Format("label-{0}", i);
                Assert.IsFalse(fakeService_.ContainsEvent("category", "action", label));
                fakeService_.PublishEvent("category", "action", label);
                Assert.IsTrue(fakeService_.ContainsEvent("category", "action", label));
            }
        }

        [Test]
        public void MeasurementProtocolServiceTests_TheFakeDiscardsOldData()
        {
            fakeService_.PublishEvent("category", "action", "alpha");
            Assert.IsTrue(fakeService_.ContainsEvent("category", "action", "alpha"));

            // Confirm that old data is discarded.
            for (int i = 0; i < 50; i++)
            {
                fakeService_.PublishEvent("category", "action", "beta");
            }
            Assert.IsFalse(fakeService_.ContainsEvent("category", "action", "alpha"));
        }
    }
}
