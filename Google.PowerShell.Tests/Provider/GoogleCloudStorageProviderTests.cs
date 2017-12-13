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

using Google.Apis.CloudResourceManager.v1;
using Google.Apis.CloudResourceManager.v1.Data;
using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.PowerShell.Provider;
using Google.PowerShell.Tests.Common;
using Moq;
using NUnit.Framework;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Google.PowerShell.Tests.Provider
{
    public class GoogleCloudStorageProviderTests : PowerShellTestBase
    {
        /// <summary>
        /// The mock of the storage service. Reset before every test.
        /// </summary>
        private readonly Mock<StorageService> _serviceMock = new Mock<StorageService>();
        private readonly Mock<CloudResourceManagerService> _resourceServiceMock =
            new Mock<CloudResourceManagerService>();

        [OneTimeSetUp]
        public void BeforeAll()
        {
            Config.Providers.Append(
                new ProviderConfigurationEntry(
                    GoogleCloudStorageProvider.ProviderName, typeof(GoogleCloudStorageProvider), null));

            GoogleCloudStorageProvider.OptionalStorageService = _serviceMock.Object;
            GoogleCloudStorageProvider.OptionalResourceService = _resourceServiceMock.Object;
        }

        [OneTimeTearDown]
        public void AfterAll()
        {
            GoogleCloudStorageProvider.OptionalStorageService = null;
            GoogleCloudStorageProvider.OptionalResourceService = null;
        }

        [SetUp]
        public new void BeforeEach()
        {
            _serviceMock.Reset();
            _resourceServiceMock.Reset();
            _resourceServiceMock.Resource(s => s.Projects).SetupRequest(
                p => p.List(),
                new ListProjectsResponse
                {
                    Projects = new[]
                    {
                        new Project {Name = FakeProjectId, ProjectId = FakeProjectId, LifecycleState = "ACTIVE"}
                    }
                });
        }

        /// <summary>
        /// Test the fix for an issue where Get-Item would occasionally return nothing.
        /// </summary>
        /// <seealso href="https://github.com/GoogleCloudPlatform/google-cloud-powershell/issues/580"/>
        [Test]
        public void TestIssue580()
        {
            const string bucketName = "bucket-name";
            Mock<BucketsResource> bucketsMock = _serviceMock.Resource(s => s.Buckets);
            bucketsMock.SetupRequest(b => b.List(FakeProjectId),
                new Buckets { Items = new[] { new Bucket { Id = bucketName, Name = bucketName } } });
            Pipeline.Commands.AddScript("cd gs:");
            Pipeline.Commands.AddScript("ls");

            Pipeline.Commands.AddScript($"Get-Item {bucketName}");
            Collection<PSObject> results = Pipeline.Invoke();

            Assert.AreEqual(0, Pipeline.Error.Count);
            Assert.AreEqual(1, results.Count);
            var returnedBucket = results[0].BaseObject as Bucket;
            Assert.IsNotNull(returnedBucket);
            Assert.AreEqual(bucketName, returnedBucket.Id);
        }
    }
}
