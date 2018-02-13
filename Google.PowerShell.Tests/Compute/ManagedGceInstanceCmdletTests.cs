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
            Name = "One"
        };
        private InstanceGroupManager SecondTestGroup = new InstanceGroupManager()
        {
            Name = "Two"
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
        /// Tests that Remove-GceManagedInstanceGroup works when pipelining regional instance group.
        /// </summary>
        [Test]
        public void TestRemoveGceManagedInstanceGroupPipelineRegional()
        {
            string instanceGroupName = "RegionalInstanceGroup";
            string regionLink = $"{ComputeHttpsLink}/projects/{FakeProjectId}/regions/{FakeRegionName}";
            InstanceGroupManager regionalInstanceGroup = new InstanceGroupManager()
            {
                Name = instanceGroupName,
                Region = regionLink,
                SelfLink = $"{regionLink}/instanceGroupManagers/{instanceGroupName}"
            };

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
        /// Runs PowerShell script script to create a GceManagedInstanceGroup.
        /// Checks that the group created has name instanceGroupName.
        /// </summary>
        /// <param name="script"></param>
        private void TestAddGceManagedInstanceGroupHelper(string script, string instanceGroupName)
        {
            Pipeline.Commands.AddScript(script);
            Collection<PSObject> results = Pipeline.Invoke();

            Assert.AreEqual(results.Count, 1);
            InstanceGroupManager createdInstance = results[0].BaseObject as InstanceGroupManager;
            Assert.AreEqual(createdInstance?.Name, instanceGroupName);
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

            TestAddGceManagedInstanceGroupHelper(
                $"Add-GceManagedInstanceGroup -Name {instanceGroupName} " +
                $"-InstanceTemplate {templateName} -TargetSize 1 -Region {FakeRegionName}",
                instanceGroupName);
            instances.VerifyAll();
        }

        /// <summary>
        /// Tests that Add-GceManagedInstanceGroup works with -Region and -Object.
        /// </summary>
        [Test]
        public void TestAddGceManagedInstanceGroupByRegionObject()
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

            TestAddGceManagedInstanceGroupHelper(
                $"${managedRegionObject} | Add-GceManagedInstanceGroup -Region {FakeRegionName}",
                instanceGroupName);
            instances.VerifyAll();
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

            TestAddGceManagedInstanceGroupHelper(
                $"Add-GceManagedInstanceGroup -Name {instanceGroupName} " +
                $"-InstanceTemplate template -TargetSize 1",
                instanceGroupName);
            instances.VerifyAll();
        }
    }
}
