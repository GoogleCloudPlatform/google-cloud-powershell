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

using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.PowerShell.Tests.Common;
using Moq;
using NUnit.Framework;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Net;

namespace Google.PowerShell.Tests.Storage
{
    public class GetGcsObjectCmdletTests : GcsCmdletTestBase
    {
        [Test]
        public void TestGetGcsObjectNamed()
        {
            const string bucketName = "mock-bucket";
            const string objectName = "mock-object";
            var response = new Object { Bucket = bucketName, Name = objectName };
            Mock<ObjectsResource> objects = ServiceMock.Resource(s => s.Objects);
            objects.SetupRequest(o => o.Get(bucketName, objectName), response);

            Pipeline.Commands.AddScript($"Get-GcsObject -Bucket {bucketName} -ObjectName {objectName}");
            Collection<PSObject> results = Pipeline.Invoke();

            var result = results.Single().BaseObject as Object;
            Assert.IsNotNull(result);
            Assert.AreEqual(bucketName, result.Bucket);
            Assert.AreEqual(objectName, result.Name);
        }

        [Test]
        public void TestGetGcsObjectListBucket()
        {
            const string bucketName = "mock-bucket";
            const string objectName = "mock-object";
            var response = new Object { Bucket = bucketName, Name = objectName };
            Mock<ObjectsResource> objects = ServiceMock.Resource(s => s.Objects);
            objects.SetupRequest(o => o.List(bucketName), new Objects { Items = new[] { response } });

            Pipeline.Commands.AddScript($"Get-GcsObject -Bucket {bucketName}");
            Collection<PSObject> results = Pipeline.Invoke();

            var result = results.Single().BaseObject as Object;
            Assert.IsNotNull(result);
            Assert.AreEqual(bucketName, result.Bucket);
            Assert.AreEqual(objectName, result.Name);
        }

        [Test]
        public void TestGetGcsObjectMissingError()
        {
            const string bucketName = "mock-bucket";
            const string objectName = "mock-object";
            Mock<ObjectsResource> objects = ServiceMock.Resource(s => s.Objects);
            objects.SetupRequestError<ObjectsResource, ObjectsResource.GetRequest, Object>(
                o => o.Get(bucketName, objectName),
                new GoogleApiException("service-name", "error-message") { HttpStatusCode = HttpStatusCode.NotFound });

            Pipeline.Commands.AddScript($"Get-GcsObject -Bucket {bucketName} -ObjectName {objectName}");
            Pipeline.Invoke();

            TestErrorRecord(ErrorCategory.ResourceUnavailable);
        }
    }
}
