using NUnit.Framework;
using Google.PowerShell.Common;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    /// <summary>
    /// Assumes that the Cloud SDK is installed on the local machine and initialized.
    /// 
    /// </summary>
    public class TestGCloudWrapper
    {
        /// <summary>
        /// Test that GetInstallationPropertiesPath returns a correct path.
        /// </summary>
        [Test]
        public void TestGetInstallationPropertiesPath()
        {
            string installedPath = GCloudWrapper.GetInstallationPropertiesPath().Result;
            Assert.IsNotNullOrEmpty(installedPath);

            Assert.IsTrue(System.IO.File.Exists(installedPath), "Installation Path should points to a file");
        }
    }
}
