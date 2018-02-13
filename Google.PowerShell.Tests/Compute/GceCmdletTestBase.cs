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
using Google.PowerShell.ComputeEngine;
using Google.PowerShell.Tests.Common;
using Moq;
using NUnit.Framework;

namespace Google.PowerShell.Tests.Compute
{
    /// <summary>
    /// Abstract base class for running unit tests on GceCmdlets.
    /// </summary>
    [TestFixture]
    public abstract class GceCmdletTestBase : PowerShellTestBase
    {
        /// <summary>
        /// Represents the start of a compute https link.
        /// </summary>
        protected const string ComputeHttpsLink = "https://www.googleapis.com/compute/v1";

        /// <summary>
        /// A completed operation.
        /// </summary>
        protected Operation DoneOperation { get; } = new Operation
        {
            Name = "doneOperation",
            Status = "DONE",
            Id = 0,
            Description = "mock operation"
        };

        /// <summary>
        /// The mock of the compute service. Reset before every test.
        /// </summary>
        protected static Mock<ComputeService> ServiceMock { get; } = new Mock<ComputeService>();

        [OneTimeSetUp]
        public void BeforeAll()
        {
            GceCmdlet.OptionalComputeService = ServiceMock.Object;
        }

        [OneTimeTearDown]
        public void AfterAll()
        {
            GceCmdlet.OptionalComputeService = null;
        }

        [SetUp]
        public new void BeforeEach()
        {
            ServiceMock.Reset();
        }
    }
}