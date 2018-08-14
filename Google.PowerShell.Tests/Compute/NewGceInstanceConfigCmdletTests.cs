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

using Google.Apis.Compute.v1.Data;
using NUnit.Framework;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;

namespace Google.PowerShell.Tests.Compute
{
    public class NewGceInstanceConfigCmdletTests : GceCmdletTestBase
    {
        [Test]
        public void TestAcceptingCustomMemoryCustomCpu()
        {
            Pipeline.Commands.AddScript(
                $"New-GceInstanceConfig -Region us-central1-a -Name instance-name -CustomCpu 2 -CustomMemory 2048");
            Collection<PSObject> results = Pipeline.Invoke();

            var instance = (Instance) results.Single().BaseObject;
            Assert.AreEqual("custom-2-2048", instance.MachineType);
        }

        [Test]
        public void TestErrorMissingCustomMemoryWithCustomCpu()
        {
            Pipeline.Commands.AddScript(
                $"New-GceInstanceConfig -Region us-central1-a -Name instance-name -CustomCpu 2");
            var e = Assert.Throws<ParameterBindingException>(() => Pipeline.Invoke());

            Assert.AreEqual("CustomMemory", e.ParameterName.Trim());
        }

        [Test]
        public void TestErrorMissingCustomCpuWithCustomMemory()
        {
            Pipeline.Commands.AddScript(
                $"New-GceInstanceConfig -Region us-central1-a -Name instance-name -CustomMemory 2048");
            var e = Assert.Throws<ParameterBindingException>(() => Pipeline.Invoke());

            Assert.AreEqual("CustomCpu", e.ParameterName.Trim());
        }

        [Test]
        public void TestMachineTypeInvalidWithCustom()
        {
            Pipeline.Commands.AddScript(
                $"New-GceInstanceConfig -Region us-central1-a -Name instance-name -CustomCpu 2 -CustomMemory 2048 -MachineType some-type");
            var e = Assert.Throws<ParameterBindingException>(() => Pipeline.Invoke());

            Assert.AreEqual("Parameter set cannot be resolved using the specified named parameters.", e.Message);
            Assert.IsNull(e.ParameterName);
        }

        [Test]
        public void TestLabels()
        {
            Pipeline.Commands.AddScript(
                $"New-GceInstanceConfig -Region us-central1-a -Name instance-name -Label @{{'key' = 'value'}}");
            Collection<PSObject> results = Pipeline.Invoke();

            var instance = (Instance)results.Single().BaseObject;
            Assert.AreEqual(instance.Labels["key"], "value");
        }
    }
}
