using NUnit.Framework;
using System.Collections;
using System.Linq;
using Google.Apis.Storage.v1.Data;
using Google.PowerShell.CloudStorage;
using Google.Apis.Storage.v1;
using System;

namespace Google.PowerShell.Tests.Storage
{
    [TestFixture]
    public class GcsCmdletTest
    {
        [Test]
        public void TestParseGcsDefaultObject()
        {
            // Working case: string maps to enum value.
            Assert.AreEqual(
                GcsCmdlet.ParseGcsDefaultObject<BucketsResource.InsertRequest.PredefinedAclEnum>("PublicRead"),
                BucketsResource.InsertRequest.PredefinedAclEnum.PublicRead);
            Assert.AreEqual(
                GcsCmdlet.ParseGcsDefaultObject<BucketsResource.InsertRequest.PredefinedAclEnum>("PUBLICREADWRITE"),
                BucketsResource.InsertRequest.PredefinedAclEnum.PublicReadWrite);
            // Private__ does the right thing.
            Assert.AreEqual(
                GcsCmdlet.ParseGcsDefaultObject<BucketsResource.InsertRequest.PredefinedAclEnum>("private"),
                BucketsResource.InsertRequest.PredefinedAclEnum.Private__);

            // Null / empty.
            Assert.AreEqual(
                GcsCmdlet.ParseGcsDefaultObject<BucketsResource.InsertRequest.PredefinedAclEnum>(""),
                null);
            Assert.AreEqual(
                GcsCmdlet.ParseGcsDefaultObject<BucketsResource.InsertRequest.PredefinedAclEnum>(null),
                null);

            // Unknown value.
            Assert.Throws<ArgumentException>(() =>
                GcsCmdlet.ParseGcsDefaultObject<BucketsResource.InsertRequest.PredefinedAclEnum>("randomACL"));

            // Different type.
            Assert.AreEqual(
                GcsCmdlet.ParseGcsDefaultObject<BucketsResource.InsertRequest.PredefinedDefaultObjectAclEnum>("BucketOwnerFullControl"),
                BucketsResource.InsertRequest.PredefinedDefaultObjectAclEnum.BucketOwnerFullControl);
        }
    }
}
