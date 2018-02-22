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
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Net;

namespace Google.PowerShell.Tests.Compute
{
    /// <summary>
    /// Tests for GceManagedInstance cmdlets.
    /// </summary>
    public class ManagedGceInstanceCmdletTests : GceCmdletTestBase
    {
        private InstanceGroupManager FirstTestGroup = new InstanceGroupManager()
        {
            Name = "One",
            Region = $@"{ComputeHttpsLink}\region\us-central1"
        };
        private InstanceGroupManager SecondTestGroup = new InstanceGroupManager()
        {
            Name = "Two"
        };

        private ManagedInstance FirstTestInstance = new ManagedInstance()
        {
            Id = 0,
            CurrentAction = "NONE"
        };

        private ManagedInstance SecondTestInstance = new ManagedInstance()
        {
            Id = 1,
            CurrentAction = "NONE"
        };

        /// <summary>
        /// Tests that Get-GceManagedInstanceGroup works with -Region option.
        /// </summary>
        [Test]
        public void TestGetRegionGceManagedInstanceGroup()
        {
            var listResponse = new RegionInstanceGroupManagerList()
            {
                Items = new[]
                {
                    FirstTestGroup,
                    SecondTestGroup
                }
            };

            Mock<RegionInstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.RegionInstanceGroupManagers);
            instances.SetupRequest(
                  item => item.List(FakeProjectId, FakeRegionName), listResponse);

            Pipeline.Commands.AddScript(
                $"Get-GceManagedInstanceGroup -Region {FakeRegionName}");
            Collection<PSObject> results = Pipeline.Invoke();
            Assert.AreEqual(results.Count, 2);
            InstanceGroupManager firstGroup = results[0]?.BaseObject as InstanceGroupManager;
            InstanceGroupManager secondGroup = results[1]?.BaseObject as InstanceGroupManager;
            Assert.IsNotNull(firstGroup);
            Assert.AreEqual(firstGroup.Name, FirstTestGroup.Name);
            Assert.IsNotNull(secondGroup);
            Assert.AreEqual(secondGroup.Name, SecondTestGroup.Name);
        }

        /// <summary>
        /// Tests that Get-GceManagedInstanceGroup works with -Name and -Region option.
        /// </summary>
        [Test]
        public void TestGetGceManagedInstanceGroupByNameAndRegion()
        {
            string instanceGroupName = FirstTestGroup.Name;
            Mock<RegionInstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.RegionInstanceGroupManagers);
            instances.SetupRequest(
                  item => item.Get(FakeProjectId, FakeRegionName, instanceGroupName), FirstTestGroup);

            Pipeline.Commands.AddScript(
                $"Get-GceManagedInstanceGroup -Name {instanceGroupName} -Region {FakeRegionName}");
            Collection<PSObject> results = Pipeline.Invoke();

            Assert.AreEqual(results.Count, 1);
            InstanceGroupManager retrievedInstance = results[0].BaseObject as InstanceGroupManager;
            Assert.AreEqual(retrievedInstance?.Name, instanceGroupName);
        }

        /// <summary>
        /// Tests that Get-GceManagedInstanceGroup throws error if -Name
        /// is used with both -Region and -Zone.
        /// </summary>
        [Test]
        public void TestGetGceManagedInstanceGroupByNameError()
        {
            Pipeline.Commands.AddScript(
                $"Get-GceManagedInstanceGroup -Name instance -Region {FakeRegionName} -Zone {FakeZoneName}");
            var error = Assert.Throws<CmdletInvocationException>(() => Pipeline.Invoke());

            Assert.AreEqual("Parameters -Region and -Zone cannot be used together with -Name.",
                error.Message);
        }

        /// <summary>
        /// Tests that Remove-GceManagedInstanceGroup works with -Region option.
        /// </summary>
        [Test]
        public void TestRemoveGceManagedInstanceGroupByRegion()
        {
            string instanceGroupName = "instance-group";
            Mock<RegionInstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.RegionInstanceGroupManagers);
            instances.SetupRequest(
                  item => item.Delete(FakeProjectId, FakeRegionName, instanceGroupName), DoneOperation);

            Pipeline.Commands.AddScript(
                $"Remove-GceManagedInstanceGroup -Name {instanceGroupName} -Region {FakeRegionName}");
            Pipeline.Invoke();

            instances.VerifyAll();
        }

        /// <summary>
        /// Creates a InstanceGroupManager with a region and self-link.
        /// </summary>
        private InstanceGroupManager CreateRegionalInstanceGroup(
            string groupName, string projectName, string regionName)
        {
            string regionLink = $"{ComputeHttpsLink}/projects/{projectName}/regions/{regionName}";
            return new InstanceGroupManager()
            {
                Name = groupName,
                Region = regionLink,
                SelfLink = $"{regionLink}/instanceGroupManagers/{groupName}"
            };
        }

        /// <summary>
        /// Tests that Remove-GceManagedInstanceGroup works when pipelining regional instance group.
        /// </summary>
        [Test]
        public void TestRemoveGceManagedInstanceGroupPipelineRegional()
        {
            string instanceGroupName = "RegionalInstanceGroup";
            InstanceGroupManager regionalInstanceGroup =
                CreateRegionalInstanceGroup(instanceGroupName, FakeProjectId, FakeRegionName);

            string managedRegionVar = "managedRegion";
            Pipeline.Runspace.SessionStateProxy.SetVariable(managedRegionVar, regionalInstanceGroup);

            Mock<RegionInstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.RegionInstanceGroupManagers);
            instances.SetupRequest(
                  item => item.Delete(FakeProjectId, FakeRegionName, instanceGroupName), DoneOperation);

            Pipeline.Commands.AddScript(
                $"${managedRegionVar} | Remove-GceManagedInstanceGroup");
            Pipeline.Invoke();

            instances.VerifyAll();
        }

        /// <summary>
        /// Tests that Remove-GceManagedInstanceGroup works when pipelining zonal instance group.
        /// </summary>
        [Test]
        public void TestRemoveGceManagedInstanceGroupPipelineZonal()
        {
            string instanceGroupName = "RegionalInstanceGroup";
            string zoneLink = $"{ComputeHttpsLink}/projects/{FakeProjectId}/zones/{FakeZoneName}";
            InstanceGroupManager regionalInstanceGroup = new InstanceGroupManager()
            {
                Name = instanceGroupName,
                Zone = zoneLink,
                SelfLink = $"{zoneLink}/instanceGroupManagers/{instanceGroupName}"
            };

            string managedRegionVar = "managedRegion";
            Pipeline.Runspace.SessionStateProxy.SetVariable(managedRegionVar, regionalInstanceGroup);

            Mock<InstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.InstanceGroupManagers);
            instances.SetupRequest(
                  item => item.Delete(FakeProjectId, FakeZoneName, instanceGroupName), DoneOperation);

            Pipeline.Commands.AddScript(
                $"${managedRegionVar} | Remove-GceManagedInstanceGroup");
            Pipeline.Invoke();

            instances.VerifyAll();
        }

        /// <summary>
        /// Tests that Add-GceManagedInstanceGroup works with -Region option.
        /// </summary>
        [Test]
        public void TestAddGceManagedInstanceGroupByRegion()
        {
            string instanceGroupName = "instance-group";
            string templateName = "instance-template";
            Mock<RegionInstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.RegionInstanceGroupManagers);
            instances.SetupRequest(
                  item => item.Insert(
                      It.Is<InstanceGroupManager>(manager => manager.Name == instanceGroupName),
                      FakeProjectId, FakeRegionName),
                  DoneOperation);
            instances.SetupRequest(
                i => i.Get(FakeProjectId, FakeRegionName, instanceGroupName),
                new InstanceGroupManager { Name = instanceGroupName });

            Pipeline.Commands.AddScript($"Add-GceManagedInstanceGroup -Name {instanceGroupName} " +
                $"-InstanceTemplate {templateName} -TargetSize 1 -Region {FakeRegionName}");
            Collection<PSObject> results = Pipeline.Invoke();

            instances.Verify(
                resource => resource.Insert(
                    It.Is<InstanceGroupManager>(manager => manager.Name == instanceGroupName),
                    FakeProjectId, FakeRegionName),
                Times.Once);

            Assert.AreEqual(results.Count, 1);
            InstanceGroupManager createdInstance = results[0].BaseObject as InstanceGroupManager;
            Assert.AreEqual(createdInstance?.Name, instanceGroupName);
        }

        /// <summary>
        /// Tests that Add-GceManagedInstanceGroup works with -Region and -Object.
        /// </summary>
        [Test]
        public void TestAddGceManagedInstanceGroupByObjectWithRegionParam()
        {
            string managedRegionObject = "managedRegionObj";
            string instanceGroupName = FirstTestGroup.Name;
            Pipeline.Runspace.SessionStateProxy.SetVariable(managedRegionObject, FirstTestGroup);

            Mock<RegionInstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.RegionInstanceGroupManagers);
            instances.SetupRequest(
                  item => item.Insert(
                      It.Is<InstanceGroupManager>(manager => manager.Name == instanceGroupName),
                      FakeProjectId, FakeRegionName),
                  DoneOperation);
            instances.SetupRequest(
                i => i.Get(FakeProjectId, FakeRegionName, FirstTestGroup.Name),
                FirstTestGroup);

            Pipeline.Commands.AddScript(
                $"${managedRegionObject} | Add-GceManagedInstanceGroup -Region {FakeRegionName}");
            Collection<PSObject> results = Pipeline.Invoke();

            instances.Verify(
                resource => resource.Insert(
                    It.Is<InstanceGroupManager>(manager => manager.Name == instanceGroupName),
                    FakeProjectId, FakeRegionName),
                Times.Once);

            Assert.AreEqual(results.Count, 1);
            InstanceGroupManager createdInstance = results[0].BaseObject as InstanceGroupManager;
            Assert.AreEqual(createdInstance?.Name, instanceGroupName);
        }

        /// <summary>
        /// Tests that Add-GceManagedInstanceGroup works with -Object when
        /// the object has a region.
        /// </summary>
        [Test]
        public void TestAddGceManagedInstanceGroupByObjectWithRegionSet()
        {
            string instanceGroupName = "RegionalInstanceGroup";
            string instanceRegionName = "MyRegion";
            InstanceGroupManager regionalInstanceGroup =
                CreateRegionalInstanceGroup(instanceGroupName, FakeProjectId, instanceRegionName);

            string managedRegionObject = "managedRegionObj";
            Pipeline.Runspace.SessionStateProxy.SetVariable(managedRegionObject, regionalInstanceGroup);

            Mock<RegionInstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.RegionInstanceGroupManagers);
            instances.SetupRequest(
                  item => item.Insert(
                      It.Is<InstanceGroupManager>(manager => manager.Name == instanceGroupName),
                      FakeProjectId, instanceRegionName),
                  DoneOperation);
            instances.SetupRequest(
                i => i.Get(FakeProjectId, instanceRegionName, instanceGroupName),
                regionalInstanceGroup);

            Pipeline.Commands.AddScript(
                $"${managedRegionObject} | Add-GceManagedInstanceGroup -Region {instanceRegionName}");
            Collection<PSObject> results = Pipeline.Invoke();

            instances.Verify(
                resource => resource.Insert(
                    It.Is<InstanceGroupManager>(manager => manager.Name == instanceGroupName),
                    FakeProjectId, instanceRegionName),
                Times.Once);

            Assert.AreEqual(results.Count, 1);
            InstanceGroupManager createdInstance = results[0].BaseObject as InstanceGroupManager;
            Assert.AreEqual(createdInstance?.Name, instanceGroupName);
        }

        /// <summary>
        /// Tests that Add-GceManagedInstanceGroup throws error if -Object
        /// is used with both -Region and -Zone.
        /// </summary>
        [Test]
        public void TestAddGceManagedInstanceGroupByObjectError()
        {
            string managedRegionObject = "managedRegionObj";
            string instanceGroupName = FirstTestGroup.Name;
            Pipeline.Runspace.SessionStateProxy.SetVariable(managedRegionObject, FirstTestGroup);

            Pipeline.Commands.AddScript(
                $"${managedRegionObject} | Add-GceManagedInstanceGroup -Region " +
                $"{FakeRegionName} -Zone {FakeZoneName}");
            var error = Assert.Throws<CmdletInvocationException>(() => Pipeline.Invoke());

            Assert.AreEqual("Parameters -Region and -Zone cannot be used together with -Object.",
                error.Message);
        }

        /// <summary>
        /// Tests that Add-GceManagedInstanceGroup uses -Zone option by default.
        /// </summary>
        [Test]
        public void TestAddGceManagedInstanceGroupDefault()
        {
            string instanceGroupName = FirstTestGroup.Name;
            Mock<InstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.InstanceGroupManagers);
            instances.SetupRequest(
                  item => item.Insert(
                      It.Is<InstanceGroupManager>(manager => manager.Name == instanceGroupName),
                      FakeProjectId, FakeZoneName),
                  DoneOperation);
            instances.SetupRequest(
                i => i.Get(FakeProjectId, FakeZoneName, instanceGroupName),
                FirstTestGroup);

            Pipeline.Commands.AddScript(
                $"Add-GceManagedInstanceGroup -Name {instanceGroupName} " +
                $"-InstanceTemplate template -TargetSize 1");
            Collection<PSObject> results = Pipeline.Invoke();

            instances.Verify(
                resource => resource.Insert(
                    It.Is<InstanceGroupManager>(manager => manager.Name == instanceGroupName),
                    FakeProjectId, FakeZoneName),
                Times.Once);

            Assert.AreEqual(results.Count, 1);
            InstanceGroupManager createdInstance = results[0].BaseObject as InstanceGroupManager;
            Assert.AreEqual(createdInstance?.Name, instanceGroupName);
        }

        /// <summary>
        /// Tests that Set-GceManagedInstanceGroup works with -Region and -Abandon option.
        /// </summary>
        [Test]
        public void TestSetGceManagedInstanceGroupAbandonInstanceByRegion()
        {
            string instanceGroupName = FirstTestGroup.Name;
            string instanceUri = "uri";
            Mock<RegionInstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.RegionInstanceGroupManagers);
            instances.SetupRequest(
                  item => item.AbandonInstances(It.IsAny<RegionInstanceGroupManagersAbandonInstancesRequest>(),
                  FakeProjectId, FakeRegionName, instanceGroupName), DoneOperation);
            instances.SetupRequest(
                i => i.Get(FakeProjectId, FakeRegionName, instanceGroupName),
                FirstTestGroup);

            Pipeline.Commands.AddScript(
                $"Set-GceManagedInstanceGroup -Name {instanceGroupName} -Region {FakeRegionName}" +
                $" -Abandon -InstanceUri {instanceUri}");
            Collection<PSObject> results = Pipeline.Invoke();

            instances.Verify(
                resource => resource.AbandonInstances(
                    It.Is<RegionInstanceGroupManagersAbandonInstancesRequest>(request =>
                        request.Instances.Count == 1 && request.Instances.Contains(instanceUri)),
                    FakeProjectId, FakeRegionName, instanceGroupName),
                Times.Once);

            Assert.AreEqual(results.Count, 1);
            InstanceGroupManager createdInstance = results[0].BaseObject as InstanceGroupManager;
            Assert.AreEqual(createdInstance?.Name, instanceGroupName);
        }

        /// <summary>
        /// Tests that Set-GceManagedInstanceGroup works with -Region and -Size option.
        /// </summary>
        [Test]
        public void TestSetGceManagedInstanceGroupResizeByRegion()
        {
            string instanceGroupName = FirstTestGroup.Name;
            int newSize = 5;
            Mock<RegionInstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.RegionInstanceGroupManagers);
            instances.SetupRequest(
                item => item.Resize(FakeProjectId, FakeRegionName, instanceGroupName, newSize),
                DoneOperation);
            instances.SetupRequest(
                i => i.Get(FakeProjectId, FakeRegionName, instanceGroupName),
                FirstTestGroup);

            Pipeline.Commands.AddScript(
                $"Set-GceManagedInstanceGroup -Name {instanceGroupName} -Region {FakeRegionName}" +
                $" -Size {newSize}");
            Collection<PSObject> results = Pipeline.Invoke();

            instances.Verify(
                resource => resource.Resize(
                    FakeProjectId, FakeRegionName, instanceGroupName, newSize),
                Times.Once);

            Assert.AreEqual(results.Count, 1);
            InstanceGroupManager createdInstance = results[0].BaseObject as InstanceGroupManager;
            Assert.AreEqual(createdInstance?.Name, instanceGroupName);
        }

        /// <summary>
        /// Tests that Set-GceManagedInstanceGroup throws error if -Region and -Zone
        /// are used together.
        /// </summary>
        [Test]
        public void TestSetGceManagedInstanceGroupError()
        {
            Pipeline.Commands.AddScript(
                $"Set-GceManagedInstanceGroup -Name instance-group -Region {FakeRegionName}" +
                $" -Zone {FakeZoneName} -Size 5");
            var error = Assert.Throws<CmdletInvocationException>(() => Pipeline.Invoke());

            Assert.AreEqual("Parameters -Region and -Zone cannot be used together.",
                error.Message);
        }

        /// <summary>
        /// Tests that Remove-GceManagedInstanceGroup works with -Region option.
        /// </summary>
        [Test]
        public void TestWaitGceManagedInstanceGroupByRegion()
        {
            var listResponse = new RegionInstanceGroupManagersListInstancesResponse()
            {
                ManagedInstances = new[]
                {
                    FirstTestInstance,
                    SecondTestInstance
                }
            };

            string instanceGroupName = "instance-group";
            Mock<RegionInstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.RegionInstanceGroupManagers);
            instances.SetupRequest(
                  item => item.ListManagedInstances(FakeProjectId, FakeRegionName, instanceGroupName),
                  listResponse);

            Pipeline.Commands.AddScript(
                $"Wait-GceManagedInstanceGroup -Name {instanceGroupName} -Region {FakeRegionName}");
            Pipeline.Invoke();

            instances.VerifyAll();
        }

        /// <summary>
        /// Tests that Remove-GceManagedInstanceGroup works when pipelining regional instance group.
        /// </summary>
        [Test]
        public void TestWaitGceManagedInstanceGroupPipelineRegional()
        {
            var listResponse = new RegionInstanceGroupManagersListInstancesResponse()
            {
                ManagedInstances = new[]
                {
                    FirstTestInstance,
                    SecondTestInstance
                }
            };

            string instanceGroupName = "RegionalInstanceGroup";
            InstanceGroupManager regionalInstanceGroup =
                CreateRegionalInstanceGroup(instanceGroupName, FakeProjectId, FakeRegionName);

            string managedRegionVar = "managedRegion";
            Pipeline.Runspace.SessionStateProxy.SetVariable(managedRegionVar, regionalInstanceGroup);

            Mock<RegionInstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.RegionInstanceGroupManagers);
            instances.SetupRequest(
                  item => item.ListManagedInstances(FakeProjectId, FakeRegionName, instanceGroupName),
                  listResponse);

            Pipeline.Commands.AddScript(
                $"${managedRegionVar} | Wait-GceManagedInstanceGroup");
            Pipeline.Invoke();

            instances.VerifyAll();
        }

        /// <summary>
        /// Tests that Wait-GceManagedInstanceGroup works when pipelining zonal instance group.
        /// </summary>
        [Test]
        public void TestWaitGceManagedInstanceGroupPipelineZonal()
        {
            var listResponse = new InstanceGroupManagersListManagedInstancesResponse()
            {
                ManagedInstances = new[]
                {
                    FirstTestInstance,
                    SecondTestInstance
                }
            };

            string instanceGroupName = "RegionalInstanceGroup";
            string zoneLink = $"{ComputeHttpsLink}/projects/{FakeProjectId}/zones/{FakeZoneName}";
            InstanceGroupManager regionalInstanceGroup = new InstanceGroupManager()
            {
                Name = instanceGroupName,
                Zone = zoneLink,
                SelfLink = $"{zoneLink}/instanceGroupManagers/{instanceGroupName}"
            };

            string managedRegionVar = "managedRegion";
            Pipeline.Runspace.SessionStateProxy.SetVariable(managedRegionVar, regionalInstanceGroup);

            Mock<InstanceGroupManagersResource> instances =
                  ServiceMock.Resource(s => s.InstanceGroupManagers);
            instances.SetupRequest(
                  item => item.ListManagedInstances(FakeProjectId, FakeZoneName, instanceGroupName),
                  listResponse);

            Pipeline.Commands.AddScript(
                $"${managedRegionVar} | Wait-GceManagedInstanceGroup");
            Pipeline.Invoke();

            instances.VerifyAll();
        }
    }
}
