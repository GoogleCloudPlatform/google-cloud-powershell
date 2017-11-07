using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Compute;
using Google.PowerShell.Tests.Common;
using Moq;
using NUnit.Framework;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;

namespace Google.PowerShell.Tests.Compute
{
    public class TestableAddGceSnapshotCmdlet : AddGceSnapshotCmdlet
    {
        public static Mock<ComputeService> ComputeServiceMock { get; } = new Mock<ComputeService>();

        /// <inheritdoc />
        public override ComputeService Service { get; } = ComputeServiceMock.Object;
    }

    public class AddSnapshotTests : PsCmdletsTests
    {
        private readonly Operation _doneOperation = new Operation
        {
            Name = "doneOperation",
            Status = "DONE",
            Id = 0,
            Description = "mock operation"
        };

        [OneTimeSetUp]
        public void BeforeAll()
        {
            Config.Cmdlets.Append(
                new CmdletConfigurationEntry("Add-GceSnapshot", typeof(TestableAddGceSnapshotCmdlet), ""));
        }

        [Test]
        public void TestCmdlet()
        {
            Mock<ComputeService> serviceMock = TestableAddGceSnapshotCmdlet.ComputeServiceMock;
            Mock<DisksResource.CreateSnapshotRequest> requestMock = serviceMock.Resource(s => s.Disks).SetupRequest(
                d => d.CreateSnapshot(It.IsAny<Snapshot>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Task.FromResult(_doneOperation));
            var snapshotResult = new Snapshot();
            serviceMock.Resource(s => s.Snapshots).SetupRequest(
                s => s.Get(It.IsAny<string>(), It.IsAny<string>()), Task.FromResult(snapshotResult));

            Pipeline.Commands.AddScript("Add-GceSnapShot -DiskName diskname -Zone someZone -Project someProject");
            Collection<PSObject> results = Pipeline.Invoke();

            Assert.AreEqual(results.Single(), snapshotResult);
            requestMock.VerifySet(r => r.GuestFlush = It.IsAny<bool>(), Times.Never);
        }

        [Test]
        public void TestVss()
        {
            Mock<ComputeService> serviceMock = TestableAddGceSnapshotCmdlet.ComputeServiceMock;
            Mock<DisksResource.CreateSnapshotRequest> requestMock = serviceMock.Resource(s => s.Disks).SetupRequest(
                d => d.CreateSnapshot(It.IsAny<Snapshot>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Task.FromResult(_doneOperation));
            var snapshotResult = new Snapshot();
            serviceMock.Resource(s => s.Snapshots).SetupRequest(
                s => s.Get(It.IsAny<string>(), It.IsAny<string>()), Task.FromResult(snapshotResult));

            Pipeline.Commands.AddScript("Add-GceSnapShot -DiskName diskname -Zone someZone -Project someProject -VSS");
            Collection<PSObject> results = Pipeline.Invoke();

            Assert.AreEqual(results.Single(), snapshotResult);
            requestMock.VerifySet(r => r.GuestFlush = true, Times.Once);
        }
    }
}
