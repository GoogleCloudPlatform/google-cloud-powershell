using System.Management.Automation.Runspaces;
using Google.PowerShell.Tests.Compute;
using Moq;
using NUnit.Framework;

namespace Google.PowerShell.Tests.Common
{
    public class PsCmdletsTests
    {
        protected readonly RunspaceConfiguration Config = RunspaceConfiguration.Create();
        protected Pipeline Pipeline;

        [SetUp]
        public void BeforeEach()
        {
            TestableAddGceSnapshotCmdlet.ComputeServiceMock.Reset();
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