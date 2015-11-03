// Copyright 2015 Google Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using NUnit.Framework;
using Google.PowerShell.Common;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    /// <summary>
    /// Assumes that the Cloud SDK is installed on the local machine and initialized.
    /// 
    /// Ideally we would script installing and configuring the Cloud SDK, but rather than
    /// deal with test account credentials, etc. We just verify that the local developer's
    /// creds and settings are present.
    /// </summary>
    internal class CloudSdkSettingsTests
    {
        [Test]
        public void TestGetConfigurationFilePath()
        {
            var cloudSettings = new CloudSdkSettings();
            string configFilePath = cloudSettings.GetConfigurationFilePath();

            Assert.IsNotNull(configFilePath);
            Assert.IsTrue(File.Exists(configFilePath));
        }

        [Test]
        public void TestGetDefaultProject()
        {
            var cloudSettings = new CloudSdkSettings();
            string defaultProject = cloudSettings.GetDefaultProject();

            Assert.IsNotNull(defaultProject);
        }
    }
}
