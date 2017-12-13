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

using Google.Cloud.BigQuery.V2;
using Moq;
using NUnit.Framework;
using System;

namespace Google.PowerShell.Tests.BigQuery
{
    public class StartBqJobTests : BqCmdletTestBase
    {
        /// <summary>
        /// Tests that Start-BqJob -Query works with -WriteMode.
        /// </summary>
        [Test]
        public void TestCreateQueryWithWriteDisposition()
        {
            string queryString = "QueryString";
            WriteDisposition disposition = WriteDisposition.WriteTruncate;

            // Checks the argument passed in to CreateQueryJob.
            ClientMock.Setup(client => client.CreateQueryJob(
                It.Is<string>(arg =>
                    arg.Equals(queryString, StringComparison.OrdinalIgnoreCase)),
                null,
                It.Is<QueryOptions>(option =>
                    option.WriteDisposition == disposition))).Returns<BigQueryJob>(null);

            Pipeline.Commands.AddScript($"Start-BqJob -Query -QueryString {queryString} -WriteMode {disposition}");
            Pipeline.Invoke();

            ClientMock.VerifyAll();
        }
    }
}
