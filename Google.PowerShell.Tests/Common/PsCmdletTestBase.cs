﻿using NUnit.Framework;
using System.Management.Automation.Runspaces;

namespace Google.PowerShell.Tests.Common
{
    /// <summary>
    /// Abstract base class for running unit tests on PSCmdlets.
    /// </summary>
    [TestFixture]
    public abstract class PsCmdletTestBase
    {
        protected readonly RunspaceConfiguration Config = RunspaceConfiguration.Create();
        protected Pipeline Pipeline;

        [SetUp]
        public void BeforeEach()
        {
            Runspace rs = RunspaceFactory.CreateRunspace(Config);
            rs.Open();
            Pipeline = rs.CreatePipeline();
        }

        [TearDown]
        public void AfterEach()
        {
            Pipeline.Dispose();
            Pipeline.Runspace.Dispose();
        }
    }
}