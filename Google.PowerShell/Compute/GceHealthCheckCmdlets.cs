using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    [Cmdlet(VerbsCommon.Add, "GceHealthCheck", DefaultParameterSetName = ParameterSetNames.ByValues)]
    public class AddGceHealthCheckCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByValues = "ByValues";
            public const string ByHttpObject = "ByHttpObject";
            public const string ByHttpsObject = "ByHttpsObject";
        }

        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; private set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByValues, Mandatory = true, Position = 0)]
        public string Name { get; private set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public string Description { get; private set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public string HostHeader { get; private set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public int? Port { get; private set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public string RequestPath { get; private set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public int? CheckIntervalSeconds { get; private set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public int? TimeoutSeconds { get; private set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public int? HealthyThreshold { get; private set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public int? UnhealthyThreshold { get; private set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public SwitchParameter Https { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByHttpsObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public HttpsHealthCheck HttpsObject { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByHttpObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public HttpHealthCheck HttpObject { get; private set; }

        protected override void ProcessRecord()
        {
            if (Https || HttpsObject != null)
            {
                HttpsHealthCheck body = HttpsObject ?? new HttpsHealthCheck
                {
                    Name = Name,
                    Description = Description,
                    Host = HostHeader,
                    Port = Port,
                    RequestPath = RequestPath,
                    CheckIntervalSec = CheckIntervalSeconds,
                    TimeoutSec = TimeoutSeconds,
                    HealthyThreshold = HealthyThreshold,
                    UnhealthyThreshold = UnhealthyThreshold
                };
                HttpsHealthChecksResource.InsertRequest request =
                    Service.HttpsHealthChecks.Insert(body, Project);
                Operation operation = request.Execute();
                AddGlobalOperation(Project, operation, () =>
                {
                    WriteObject(Service.HttpsHealthChecks.Get(Project, body.Name).Execute());
                });
            }
            else
            {
                HttpHealthCheck body = HttpObject ?? new HttpHealthCheck
                {
                    Name = Name,
                    Description = Description,
                    Host = HostHeader,
                    Port = Port,
                    RequestPath = RequestPath,
                    CheckIntervalSec = CheckIntervalSeconds,
                    TimeoutSec = TimeoutSeconds,
                    HealthyThreshold = HealthyThreshold,
                    UnhealthyThreshold = UnhealthyThreshold
                };
                HttpHealthChecksResource.InsertRequest request =
                    Service.HttpHealthChecks.Insert(body, Project);
                Operation operation = request.Execute();
                AddGlobalOperation(Project, operation, () =>
                {
                    WriteObject(Service.HttpHealthChecks.Get(Project, body.Name).Execute());
                });
            }
        }
    }

    [Cmdlet(VerbsCommon.Get, "GceHealthCheck")]
    public class GetGceHealthCheckCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string ByName = "ByName";
        }

        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        [Parameter]
        public SwitchParameter Http { get; set; }

        [Parameter]
        public SwitchParameter Https { get; set; }


        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.OfProject:
                    WriteObject(GetAllProjectHealthChecks(), true);
                    break;
                case ParameterSetNames.ByName:
                    WriteObject(GetHealthCheckByName(), true);
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            Service.HttpHealthChecks.List(Project);
        }

        private IEnumerable<object> GetHealthCheckByName()
        {
            var exceptions = new List<Exception>();
            var healthChecks = new List<object>();
            if (Http || !Https)
            {
                HttpHealthChecksResource.GetRequest request = Service.HttpHealthChecks.Get(Project, Name);
                try
                {
                    healthChecks.Add(request.Execute());
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            if (Https || !Http)
            {
                HttpsHealthChecksResource.GetRequest request = Service.HttpsHealthChecks.Get(Project, Name);
                try
                {
                    healthChecks.Add(request.Execute());
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            if (healthChecks.Count == 0)
            {
                if (exceptions.Count == 1)
                {
                    throw exceptions[0];
                }
                else
                {
                    throw new AggregateException(
                        $"Can not find health check named {Name} in project {Project}", exceptions);
                }
            }
            else
            {
                return healthChecks;
            }
        }

        private IEnumerable<object> GetAllProjectHealthChecks()
        {
            if (Http || !Https)
            {
                HttpHealthChecksResource.ListRequest request = Service.HttpHealthChecks.List(Project);
                do
                {
                    HttpHealthCheckList response = request.Execute();
                    if (response.Items != null)
                    {
                        foreach (HttpHealthCheck healthCheck in response.Items)
                        {
                            yield return healthCheck;
                        }
                    }
                    request.PageToken = response.NextPageToken;
                } while (!Stopping && request.PageToken != null);
            }

            if (Https || !Http)
            {
                HttpsHealthChecksResource.ListRequest request = Service.HttpsHealthChecks.List(Project);
                do
                {
                    HttpsHealthCheckList response = request.Execute();
                    if (response.Items != null)
                    {
                        foreach (HttpsHealthCheck healthCheck in response.Items)
                        {
                            yield return healthCheck;
                        }
                    }
                    request.PageToken = response.NextPageToken;
                } while (!Stopping && request.PageToken != null);
            }
        }
    }

    [Cmdlet(VerbsCommon.Remove, "GceHealthCheck", SupportsShouldProcess = true)]
    public class RemoveGceHealthCheckCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByNameHttp = "ByNameHttp";
            public const string ByNameHttps = "ByNameHttps";
            public const string ByHttpObject = "ByHttpObject";
            public const string ByHttpsObject = "ByHttpsObject";
        }
        [Parameter(ParameterSetName = ParameterSetNames.ByNameHttp)]
        [Parameter(ParameterSetName = ParameterSetNames.ByNameHttps)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByNameHttp, Mandatory = true, Position = 0)]
        [Parameter(ParameterSetName = ParameterSetNames.ByNameHttps, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByNameHttp, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByHttpObject)]
        public SwitchParameter Http { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByNameHttps, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByHttpsObject)]
        public SwitchParameter Https { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByHttpObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public HttpHealthCheck HttpObject { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByHttpsObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public HttpsHealthCheck HttpsObject { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByNameHttp:
                    RemoveHttpHealthCheck(Project, Name);
                    break;
                case ParameterSetNames.ByNameHttps:
                    RemoveHttpsHealthCheck(Project, Name);
                    break;
                case ParameterSetNames.ByHttpObject:
                    RemoveHttpHealthCheck(GetProjectNameFromUri(HttpObject.SelfLink), HttpObject.Name);
                    break;
                case ParameterSetNames.ByHttpsObject:
                    RemoveHttpsHealthCheck(GetProjectNameFromUri(HttpsObject.SelfLink), HttpsObject.Name);
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private void RemoveHttpsHealthCheck(string project, string name)
        {
            if (ShouldProcess($"{project}/{name}", "Remove Https Health Check"))
            {
                HttpsHealthChecksResource.DeleteRequest request = Service.HttpsHealthChecks.Delete(project, name);
                Operation operation = request.Execute();
                AddGlobalOperation(project, operation);
            }
        }

        private void RemoveHttpHealthCheck(string project, string name)
        {
            if (ShouldProcess($"{project}/{name}", "Remove Http Health Check"))
            {
                HttpHealthChecksResource.DeleteRequest request = Service.HttpHealthChecks.Delete(project, name);
                Operation operation = request.Execute();
                AddGlobalOperation(project, operation);
            }
        }
    }

    [Cmdlet(VerbsCommon.Set, "GceHealthCheck", SupportsShouldProcess = true)]
    public class SetGceHealthCheckCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByHttpObject = "ByHttpObject";
            public const string ByHttpsObject = "ByHttpsObject";
        }

        [Parameter(ParameterSetName = ParameterSetNames.ByHttpObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public HttpHealthCheck HttpObject { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByHttpsObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public HttpsHealthCheck HttpsObject { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByHttpObject)]
        public SwitchParameter Http { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByHttpsObject)]
        public SwitchParameter Https { get; set; }

        protected override void ProcessRecord()
        {
            string project;
            string name;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByHttpObject:
                    project = GetProjectNameFromUri(HttpObject.SelfLink);
                    name = HttpObject.Name;
                    if (ShouldProcess($"{project}/{name}", "Set Http Health Check"))
                    {
                        HttpHealthChecksResource.UpdateRequest request =
                            Service.HttpHealthChecks.Update(HttpObject, project, name);
                        Operation operation = request.Execute();
                        AddGlobalOperation(project, operation, () =>
                        {
                            WriteObject(Service.HttpHealthChecks.Get(project, name));

                        });
                    }
                    break;
                case ParameterSetNames.ByHttpsObject:
                    project = GetProjectNameFromUri(HttpsObject.SelfLink);
                    name = HttpsObject.Name;
                    if (ShouldProcess($"{project}/{name}", "Set Https Health Check"))
                    {
                        HttpsHealthChecksResource.UpdateRequest request =
                            Service.HttpsHealthChecks.Update(HttpsObject, project, name);
                        Operation operation = request.Execute();
                        AddGlobalOperation(project, operation, () =>
                        {
                            WriteObject(Service.HttpsHealthChecks.Get(project, name));

                        });
                    }
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }
    }
}
