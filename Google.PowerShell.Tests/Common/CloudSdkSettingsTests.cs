// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

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
    /// 
    /// IMPORTANT TODO(chrsmith): We'll want to automate testing these routines against
    /// different installs of the Cloud SDK:
    /// - { per-user, per-system }
    /// - { default install location, non-standard install location }
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
        public void TestGetPythonConfigurationFilePath()
        {
            var cloudSettings = new CloudSdkSettings();
            string pythonConfigFilePath = cloudSettings.GetPythonConfigurationFilePath();

            Assert.IsNotNull(pythonConfigFilePath);
            Assert.IsTrue(File.Exists(pythonConfigFilePath));
        }

        [Test]
        public void TestGetDefaultProject()
        {
            var cloudSettings = new CloudSdkSettings();
            string defaultProject = cloudSettings.GetDefaultProject();
            // This requires that gcloud init was run and the default project was set.
            Assert.IsNotNull(defaultProject);
        }

        [Test]
        public void TestGetOptInSetting()
        {
            var cloudSettings = new CloudSdkSettings();
            // Just assert this doesn't throw, depending on the install the
            // value could be true or false.
            cloudSettings.GetOptIntoReportingSetting();
            // Same with above. If the user opted into settings (and has ran
            // the Python bits at least once) the value will be stable.
            // Otherwise it will be different each time.
            cloudSettings.GetAnoymousClientID();
        }
    }
}
