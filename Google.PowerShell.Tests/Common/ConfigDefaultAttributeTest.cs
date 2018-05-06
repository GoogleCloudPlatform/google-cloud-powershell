// Copyright 2015-2018 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.PowerShell.Common;
using NUnit.Framework;
using System.Management.Automation;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class ConfigDefaultAttributeTest
    {
        internal class TestGCloudCmdlet : GCloudCmdlet
        {
            [ConfigPropertyName("project")]
            [Parameter]
            public override string Project { get; set; }

            public void TestBeginProcessing()
            {
                BeginProcessing();
            }
        }

        internal class TestFailGCloudCmdlet : GCloudCmdlet
        {
            [ConfigPropertyName("not_a_gcloud_config_parameter")]
            [Parameter]
            public override string Project { get; set; }

            public void TestBeginProcessing()
            {
                BeginProcessing();
            }
        }

        [Test]
        public void TestConfigDefaultWorks()
        {
            var cmdlet = new TestGCloudCmdlet();
            cmdlet.TestBeginProcessing();
            Assert.AreEqual(CloudSdkSettings.GetDefaultProject(), cmdlet.Project);
        }

        [Test]
        public void TestConfigDefaultThrowNotFound()
        {
            var cmdlet = new TestFailGCloudCmdlet();
            Assert.Throws<PSInvalidOperationException>(cmdlet.TestBeginProcessing);
        }
    }
}