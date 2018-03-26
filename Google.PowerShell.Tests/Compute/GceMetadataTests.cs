// Copyright 2018 Google Inc. All Rights Reserved.
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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using Google.PowerShell.ComputeEngine;

namespace Google.PowerShell.Tests.Compute
{
    public class GetGceMetadataTest : GceCmdletTestBase
    {
        /// <summary>
        /// Tests that the HttpClient used by GceMetadata
        /// has the correct headers for the first call.
        /// </summary>
        [Test]
        public void TestFirstCallClientHeader()
        {
            Assert.IsFalse(GetGceMetadataCmdlet.Client.DefaultRequestHeaders.Contains(
                GetGceMetadataCmdlet.MetadataFlavorHeader));
            Pipeline.Commands.AddScript(
                $"Get-GceMetadata");
            try
            {
                Collection<PSObject> results = Pipeline.Invoke();
            }
            // Exception thrown because we are not running from a VM.
            catch { }

            IEnumerable<string> headers = GetGceMetadataCmdlet.Client.DefaultRequestHeaders.GetValues(
                GetGceMetadataCmdlet.MetadataFlavorHeader);
            Assert.AreEqual(headers.Count(), 1);
            Assert.AreEqual(headers.First(), "Google");
        }

        /// <summary>
        /// Tests that the HttpClient used by GceMetadata
        /// has the correct headers for the second call.
        /// </summary>
        [Test]
        public void TestSecondCallClientHeader()
        {
            Assert.IsFalse(GetGceMetadataCmdlet.Client.DefaultRequestHeaders.Contains(
                GetGceMetadataCmdlet.MetadataFlavorHeader));
            Pipeline.Commands.AddScript(
                $"Get-GceMetadata; Get-GceMetadata");
            try
            {
                Collection<PSObject> results = Pipeline.Invoke();
            }
            // Exception thrown because we are not running from a VM.
            catch { }

            IEnumerable<string> headers = GetGceMetadataCmdlet.Client.DefaultRequestHeaders.GetValues(
                GetGceMetadataCmdlet.MetadataFlavorHeader);
            Assert.AreEqual(headers.Count(), 1);
            Assert.AreEqual(headers.First(), "Google");
        }
    }
}
