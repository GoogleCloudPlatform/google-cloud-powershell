// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using NUnit.Framework;
using Google.Apis.Util;
using Google.PowerShell.Common;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
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

        /// <summary>
        /// Test that GetAccessToken returns a valid token.
        /// </summary>
        [Test]
        public void TestGetAccessToken()
        {
            ActiveUserToken token = GCloudWrapper.GetAccessToken(new System.Threading.CancellationToken()).Result;
            Assert.IsNotNull(token);
            Assert.IsNotNullOrEmpty(token.AccessToken, "Token returned by GetAccessToken should have an access token.");
            Assert.IsNotNull(token.ExpiredTime, "Token returned by GetAccessToken should have an Issued DateTime.");
            Assert.IsFalse(token.IsExpired, "Token returned by GetAccessToken should be valid.");
        }
    }
}
