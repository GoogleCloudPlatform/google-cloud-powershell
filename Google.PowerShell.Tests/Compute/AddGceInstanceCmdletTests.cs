﻿// Copyright 2017 Google Inc. All Rights Reserved.
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

namespace Google.PowerShell.Tests.Compute
{
    public class AddGceInstanceConfigCmdletTests : GceCmdletTestBase
    {
        [Test]
        public void TestAcceptingCustomMemoryCustomCpu()
        {
            const string psDiskVar = "attachedDisk";
            const string mockedResultName = "mocked-result-name";
            Pipeline.Runspace.SessionStateProxy.SetVariable(psDiskVar, new AttachedDisk());
            Mock<InstancesResource> instances = ServiceMock.Resource(s => s.Instances);
            instances.SetupRequest(
                i => i.Insert(It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>()),
                DoneOperation);
            instances.SetupRequest(
                i => i.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                new Instance { Name = mockedResultName });

            Pipeline.Commands.AddScript(
                $"Add-GceInstance -Name instance-name -Disk ${psDiskVar} -CustomCpu 2 -CustomMemory 2048");
            Collection<PSObject> results = Pipeline.Invoke();

            instances.Verify(
                resource => resource.Insert(
                    It.Is<Instance>(i => i.MachineType == $"zones/{FakeZoneName}/machineTypes/custom-2-2048"),
                    FakeProjectId, FakeZoneName),
                Times.Once);
            var instance = (Instance) results.Single().BaseObject;
            Assert.AreEqual(mockedResultName, instance.Name);
        }

        [Test]
        public void TestErrorMissingCustomMemoryWithCustomCpu()
        {
            const string psDiskVar = "attachedDisk";
            Pipeline.Runspace.SessionStateProxy.SetVariable(psDiskVar, new AttachedDisk());

            Pipeline.Commands.AddScript(
                $"Add-GceInstance -Name instance-name -Disk ${psDiskVar} -CustomCpu 2");
            var e = Assert.Throws<ParameterBindingException>(() => Pipeline.Invoke());

            Assert.AreEqual("CustomMemory", e.ParameterName.Trim());
        }

        [Test]
        public void TestErrorMissingCustomCpuWithCustomMemory()
        {
            const string psDiskVar = "attachedDisk";
            Pipeline.Runspace.SessionStateProxy.SetVariable(psDiskVar, new AttachedDisk());

            Pipeline.Commands.AddScript(
                $"Add-GceInstance -Name instance-name -Disk ${psDiskVar} -CustomMemory 2048");
            var e = Assert.Throws<ParameterBindingException>(() => Pipeline.Invoke());

            Assert.AreEqual("CustomCpu", e.ParameterName.Trim());
        }

        [Test]
        public void TestMachineTypeInvalidWithCustom()
        {
            const string psDiskVar = "attachedDisk";
            Pipeline.Runspace.SessionStateProxy.SetVariable(psDiskVar, new AttachedDisk());

            Pipeline.Commands.AddScript(
                $"Add-GceInstance -Name instance-name -Disk ${psDiskVar} -CustomCpu 2 -CustomMemory 2048 -MachineType some-type");
            var e = Assert.Throws<ParameterBindingException>(() => Pipeline.Invoke());

            Assert.AreEqual("Parameter set cannot be resolved using the specified named parameters.", e.Message);
            Assert.IsNull(e.ParameterName);
        }
    }
}
