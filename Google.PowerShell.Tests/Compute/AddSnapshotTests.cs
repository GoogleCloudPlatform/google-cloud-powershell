// Copyright 2017 Google Inc. All Rights Reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Tests.Common;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.ObjectModel;
using System.Management.Automation;

namespace Google.PowerShell.Tests.Compute
{

    /// <summary>
    /// Unit tests for the Add-GceSnapshot cmdlet.
    /// </summary>
    public class AddSnapshotTests : GceCmdletTestBase
    {

        [Test]
        public void TestDiskNameParameterSet()
        {
            const string diskName = "diskname";
            Mock<DisksResource> diskResourceMock = ServiceMock.Resource(s => s.Disks);
            Mock<DisksResource.CreateSnapshotRequest> requestMock = diskResourceMock.SetupRequest(
                d => d.CreateSnapshot(
                    It.Is<Snapshot>(s => s.Name.StartsWith(diskName, StringComparison.Ordinal)), FakeProjectId, FakeZoneName,
                    diskName),
                DoneOperation);
            var snapshotResult = new Snapshot();
            ServiceMock.Resource(s => s.Snapshots).SetupRequest(
                s => s.Get(It.IsAny<string>(), It.IsAny<string>()), snapshotResult);

            Pipeline.Commands.AddScript($"Add-GceSnapShot -DiskName {diskName}");
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
                DoneOperation);
            var snapshotResult = new Snapshot();
            ServiceMock.Resource(s => s.Snapshots).SetupRequest(
                s => s.Get(It.IsAny<string>(), It.IsAny<string>()), snapshotResult);

            Pipeline.Commands.AddScript("Add-GceSnapShot -DiskName diskname -VSS");
            Collection<PSObject> results = Pipeline.Invoke();

            CollectionAssert.AreEqual(results, new[] { snapshotResult });
            requestMock.VerifySet(r => r.GuestFlush = true, Times.Once);
        }

        [Test]
        public void TestLabels()
        {
            Mock<DisksResource.CreateSnapshotRequest> requestMock = ServiceMock.Resource(s => s.Disks).SetupRequest(
                d => d.CreateSnapshot(It.Is<Snapshot>(snapshot => snapshot.Labels["key"] == "value"),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                DoneOperation);
            var snapshotResult = new Snapshot();
            ServiceMock.Resource(s => s.Snapshots).SetupRequest(
                s => s.Get(It.IsAny<string>(), It.IsAny<string>()), snapshotResult);

            Pipeline.Commands.AddScript("Add-GceSnapShot -DiskName diskname -Label @{'key' = 'value'}");
            Collection<PSObject> results = Pipeline.Invoke();

            CollectionAssert.AreEqual(results, new[] { snapshotResult });
        }
    }
}
