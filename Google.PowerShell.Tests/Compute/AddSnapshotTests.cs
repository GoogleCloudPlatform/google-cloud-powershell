using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Compute;
using Google.PowerShell.Tests.Common;
using Moq;
using NUnit.Framework;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;

namespace Google.PowerShell.Tests.Compute
{
    public class TestableAddGceSnapshotCmdlet : AddGceSnapshotCmdlet
    {
        /// <inheritdoc />
        public override ComputeService Service { get; } = GceCmdletTestBase.ServiceMock.Object;
    }

    public class AddSnapshotTests : GceCmdletTestBase
    {
        [OneTimeSetUp]
        public void BeforeAll()
        {
            Config.Cmdlets.Append(
                new CmdletConfigurationEntry("Add-GceSnapshot", typeof(TestableAddGceSnapshotCmdlet), ""));
        }

        [Test]
        public void TestDiskNameParameterSet()
        {
            const string diskName = "diskname";
            const string projectName = "someProject";
            const string zoneName = "someZone";
            Mock<DisksResource> diskResourceMock = ServiceMock.Resource(s => s.Disks);
            Mock<DisksResource.CreateSnapshotRequest> requestMock = diskResourceMock.SetupRequest(
                d => d.CreateSnapshot(It.Is<Snapshot>(s => s.Name.StartsWith(diskName)), projectName, zoneName, diskName),
                Task.FromResult(DoneOperation));
            var snapshotResult = new Snapshot();
            ServiceMock.Resource(s => s.Snapshots).SetupRequest(
                s => s.Get(It.IsAny<string>(), It.IsAny<string>()), Task.FromResult(snapshotResult));

            Pipeline.Commands.AddScript($"Add-GceSnapShot -DiskName {diskName} -Zone {zoneName} -Project {projectName}");
            Collection<PSObject> results = Pipeline.Invoke();

            CollectionAssert.AreEqual(results, new[] { snapshotResult });
            diskResourceMock.VerifyAll();
            requestMock.VerifySet(r => r.GuestFlush = It.IsAny<bool>(), Times.Never);
        }

        [Test]
        public void TestVss()
        {
            Mock<DisksResource.CreateSnapshotRequest> requestMock = ServiceMock.Resource(s => s.Disks).SetupRequest(
                d => d.CreateSnapshot(It.IsAny<Snapshot>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Task.FromResult(DoneOperation));
            var snapshotResult = new Snapshot();
            ServiceMock.Resource(s => s.Snapshots).SetupRequest(
                s => s.Get(It.IsAny<string>(), It.IsAny<string>()), Task.FromResult(snapshotResult));

            Pipeline.Commands.AddScript("Add-GceSnapShot -DiskName diskname -Zone someZone -Project someProject -VSS");
            Collection<PSObject> results = Pipeline.Invoke();

            CollectionAssert.AreEqual(results, new[] { snapshotResult });
            requestMock.VerifySet(r => r.GuestFlush = true, Times.Once);
        }
    }
}
