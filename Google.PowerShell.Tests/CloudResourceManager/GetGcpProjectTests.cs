// Copyright 2018 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.CloudResourceManager.v1;
using Google.Apis.CloudResourceManager.v1.Data;
using Google.PowerShell.Tests.Common;
using Moq;
using NUnit.Framework;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Google.PowerShell.Tests.CloudResourceManager
{
    public class GetGcpProjectTests : CloudResourceManagerCmdletTestBase
    {
        [Test]
        public void TestPipelinesCorrectly()
        {
            Mock<ProjectsResource> projects = ServiceMock.Resource(s => s.Projects);
            Project firstResult = new Project { Name = "FirstProject", ProjectId = "first-project" };
            projects.SetupRequest(
                p => p.List(),
                Task.FromResult(new ListProjectsResponse()
                {
                    Projects = new[] {
                        firstResult,
                        new Project { Name = "SecondProject", ProjectId = "second-project" }
                    }
                }));

            Pipeline.Commands.AddScript("Get-GcpProject | Select -First 1");
            Collection<PSObject> results = Pipeline.Invoke();

            Assert.AreEqual(firstResult, results.Single().BaseObject);
        }
    }
}
