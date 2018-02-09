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
    }
}
