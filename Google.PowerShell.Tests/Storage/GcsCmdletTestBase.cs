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

using Google.Apis.Storage.v1;
using Google.PowerShell.CloudStorage;
using Google.PowerShell.Tests.Common;
using Moq;
using NUnit.Framework;

namespace Google.PowerShell.Tests.Storage
{
    /// <summary>
    /// Abstract base class for running unit tests on GceCmdlets.
    /// </summary>
    [TestFixture]
    public abstract class GcsCmdletTestBase : PowerShellTestBase
    {
        /// <summary>
        /// The mock of the compute service. Reset before every test.
        /// </summary>
        protected static Mock<StorageService> ServiceMock { get; } = new Mock<StorageService>();

        [OneTimeSetUp]
        public void BeforeAll()
        {
            GcsCmdlet.DefaultStorageService = ServiceMock.Object;
        }

        [OneTimeTearDown]
        public void AfterAll()
        {
            GcsCmdlet.DefaultStorageService = null;
        }

        [SetUp]
        public new void BeforeEach()
        {
            ServiceMock.Reset();
        }
    }
}