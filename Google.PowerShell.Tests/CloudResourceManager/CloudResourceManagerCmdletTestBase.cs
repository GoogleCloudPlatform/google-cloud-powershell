// Copyright 2018 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.CloudResourceManager.v1;
using Google.PowerShell.CloudResourceManager;
using Google.PowerShell.Tests.Common;
using Moq;
using NUnit.Framework;

namespace Google.PowerShell.Tests.CloudResourceManager
{
    public class CloudResourceManagerCmdletTestBase : PowerShellTestBase
    {
        /// <summary>
        /// The mock of the cloud resource manager service. Reset before every test.
        /// </summary>
        protected static Mock<CloudResourceManagerService> ServiceMock { get; } = new Mock<CloudResourceManagerService>();

        [OneTimeSetUp]
        public void BeforeAll()
        {
            CloudResourceManagerCmdlet.ServiceOverride = ServiceMock.Object;
        }

        [OneTimeTearDown]
        public void AfterAll()
        {
            CloudResourceManagerCmdlet.ServiceOverride = null;
        }

        [SetUp]
        public new void BeforeEach()
        {
            ServiceMock.Reset();
        }

    }
}
