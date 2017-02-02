// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Adds a Google Compute Engine health check.
    /// </para>
    /// <para type="descritpion">
    /// Adds a Google Compute Engine health check. Use this cmdlet to create both HTTP and HTTPS health checks.
    /// https://cloud.google.com/compute/docs/load-balancing/health-checks
    /// </para>
    /// <example>
    ///   <code>PS C:\> Add-GceHealthCheck "my-health-check" -Project "my-project" -Http</code>
    ///   <para>Adds an HTTP health check to the project named "my-project".</para>
    /// </example>
    /// <example>
    ///   <code> PS C:\> Add-GceHealthCheck "my-health-check" -Https </code>
    ///   <para>Adds an HTTPS health check to the project in the Cloud SDK config.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Add-GceHealthCheck "my-health-check" -Http -Description "Description of my health check." `
    ///      -HostHeader "mydomain.com" -Port 50 -RequestPath "/some/path" -CheckInterval "0:0:2" `
    ///      -Timeout "0:0:2" -HealthyThreshold 3 -UnhealthyThreshold 3
    ///   </code>
    ///   <para>Adds an HTTP health check with non-default values.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/httpHealthChecks#resource)">
    /// [HealthCheck resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceHealthCheck", DefaultParameterSetName = ParameterSetNames.ByValues)]
    [OutputType(typeof(HttpHealthCheck), typeof(HttpsHealthCheck))]
    public class AddGceHealthCheckCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByValues = "ByValues";
            public const string ByHttpObject = "ByHttpObject";
            public const string ByHttpsObject = "ByHttpsObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project to add the health check to. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the health check to add.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// Human readable description of the health check.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The value of the host header in the health check request. If left empty, the public IP on behalf
        /// of which this health check is performed will be used.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public string HostHeader { get; set; }

        /// <summary>
        /// <para type="description">
        /// The TCP port number for the health check request. Defaults to 80 for HTTP and 443 for HTTPS.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public int? Port { get; set; }

        /// <summary>
        /// <para type="description">
        /// The request path for the health check request. Defaults to "/".
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public string RequestPath { get; set; }

        /// <summary>
        /// <para type="description">
        /// How often to send a health check request. Defaults to 5 seconds.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public TimeSpan? CheckInterval { get; set; }

        /// <summary>
        /// <para type="description">
        /// How long to wait before claiming failure. Defaults to 5 seconds.
        /// May not be greater than CheckInterval.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// <para type="description">
        /// Number of consecutive success required to mark an unhealthy instance healthy. Defaults to 2.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public int? HealthyThreshold { get; set; }

        /// <summary>
        /// <para type="description">
        /// Number of consecutive failures required to mark a healthy instance unhealthy. Defaults to 2.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public int? UnhealthyThreshold { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will create an HTTPS health check. If not set, will create an HTTP health check.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public SwitchParameter Https { get; set; }

        /// <summary>
        /// <para type="description">
        /// Object describing a new HTTPS health check.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByHttpsObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public HttpsHealthCheck HttpsObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// Object describing a new http health check.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByHttpObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public HttpHealthCheck HttpObject { get; set; }

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
                    CheckIntervalSec = (int?)CheckInterval?.TotalSeconds,
                    TimeoutSec = (int?)Timeout?.TotalSeconds,
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
                    CheckIntervalSec = (int?)CheckInterval?.TotalSeconds,
                    TimeoutSec = (int?)Timeout?.TotalSeconds,
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

    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Compute Engine health checks.
    /// </para>
    /// <para type="description">
    /// Gets Google Compute Engine health checks.
    /// This cmdlet can be used to retrieve both HTTP and HTTPS health checks. It can list all health checks
    /// of a project, or get a health check by name.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GceHealthCheck -Project "my-project"</code>
    ///   <para>Gets all health checks of project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceHealthCheck -Https</code>
    ///   <para>Gets all HTTPS health checks of the project in the Cloud SDK config.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\>; Get-GceHealthCheck "my-health-check" -Http</code>
    ///   <para>
    ///     Gets the HTTP health check named "my-health-check" in the project of the Cloud SDK config.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/httpHealthChecks#resource)">
    /// [HealthCheck resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceHealthCheck", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(HttpHealthCheck), typeof(HttpsHealthCheck))]
    public class GetGceHealthCheckCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// The name of the project to get the health checks of.
        /// Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the health check to get.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will get health checks that use HTTP.
        /// If neither -Http nor -Https are set, Get-GceHealthCheck will retrieve both.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter Http { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will get health checks that use HTTPS.
        /// If neither -Http nor -Https are set, Get-GceHealthCheck will retrieve both.
        /// </para>
        /// </summary>
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
                        $"The health check named {Name} in project {Project} could not be found", exceptions);
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

    /// <summary>
    /// <para type="synopsis">
    /// Removes a Google Compute Engine health check.
    /// </para>
    /// <para type="description">
    /// Removes a Google Compute Engine Health check. Use this cmdlet to remove both HTTP and HTTPS health
    /// checks.
    /// </para>
    /// <example>
    ///   <code> PS C:\> Remove-GceHealthCheck "my-health-check" -Project "my-project" -Http </code>
    ///   <para>Remove HTTP health check "my-health-check" from project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code> PS C:\> Remove-GceHealthCheck "my-health-check" -Https </code>
    ///   <para>Remove HTTPS health check "my-health-check" from the project in the Cloud SDK config.</para>
    /// </example>
    /// <example>
    ///   <code> PS C:\> Get-GceHealthCheck -Project "my-project | Remove-GceHealthCheck</code>
    ///   <para>Remove all health checks from project "my-project".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/httpHealthChecks#resource)">
    /// [HealthCheck resource definition]
    /// </para>
    /// </summary>
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

        /// <summary>
        /// <para type="description">
        /// The name of the project to remove the health check from.
        /// Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameHttp)]
        [Parameter(ParameterSetName = ParameterSetNames.ByNameHttps)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the health check to remove.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameHttp, Mandatory = true, Position = 0)]
        [Parameter(ParameterSetName = ParameterSetNames.ByNameHttps, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will remove a health check that uses HTTP.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameHttp, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByHttpObject)]
        public SwitchParameter Http { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will remove a health check that uses HTTPS.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameHttps, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByHttpsObject)]
        public SwitchParameter Https { get; set; }

        /// <summary>
        /// <para type="description">
        /// An object defining the HTTP health check to remove.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByHttpObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public HttpHealthCheck HttpObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// An object defining the HTTPS health check to remove.
        /// </para>
        /// </summary>
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
            if (ShouldProcess($"{project}/{name}", "Remove HTTPS Health Check"))
            {
                HttpsHealthChecksResource.DeleteRequest request = Service.HttpsHealthChecks.Delete(project, name);
                Operation operation = request.Execute();
                AddGlobalOperation(project, operation);
            }
        }

        private void RemoveHttpHealthCheck(string project, string name)
        {
            if (ShouldProcess($"{project}/{name}", "Remove HTTP Health Check"))
            {
                HttpHealthChecksResource.DeleteRequest request = Service.HttpHealthChecks.Delete(project, name);
                Operation operation = request.Execute();
                AddGlobalOperation(project, operation);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Sets the data of a Google Compute Engine health check.
    /// </para>
    /// <para type="description">
    /// Sets the data of a Google Compute Engine health check. First get the health check object with
    /// Get-GceHealthCheck. Then change the data in the object you received. Finally send that object to
    /// Set-GceHealthCheck.
    /// </para>
    /// <example>
    ///   <code>
    ///     PS C:\> $healthCheck = Get-GceHealthCheck "my-health-check" -Project "my-project"
    ///     PS C:\> $healthCheck.CheckIntervalSec = 30
    ///     PS C:\> $healthCheck | Set-GceHealthCheck
    ///   </code>
    ///   <para>Changes the  HTTP health check "my-health-check" from project "my-project".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/httpHealthChecks#resource)">
    /// [HealthCheck resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GceHealthCheck", SupportsShouldProcess = true)]
    [OutputType(typeof(HttpHealthCheck), typeof(HttpsHealthCheck))]
    public class SetGceHealthCheckCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByHttpObject = "ByHttpObject";
            public const string ByHttpsObject = "ByHttpsObject";
        }

        /// <summary>
        /// <para type="description">
        /// The object describing a health check using HTTP.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByHttpObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public HttpHealthCheck HttpObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// The object describing a health check using HTTPS.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByHttpsObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public HttpsHealthCheck HttpsObject { get; set; }

        protected override void ProcessRecord()
        {
            string project;
            string name;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByHttpObject:
                    project = GetProjectNameFromUri(HttpObject.SelfLink);
                    name = HttpObject.Name;
                    if (ShouldProcess($"{project}/{name}", "Set HTTP Health Check"))
                    {
                        HttpHealthChecksResource.UpdateRequest request =
                            Service.HttpHealthChecks.Update(HttpObject, project, name);
                        Operation operation = request.Execute();
                        AddGlobalOperation(project, operation, () =>
                        {
                            WriteObject(Service.HttpHealthChecks.Get(project, name).Execute());
                        });
                    }
                    break;
                case ParameterSetNames.ByHttpsObject:
                    project = GetProjectNameFromUri(HttpsObject.SelfLink);
                    name = HttpsObject.Name;
                    if (ShouldProcess($"{project}/{name}", "Set HTTPS Health Check"))
                    {
                        HttpsHealthChecksResource.UpdateRequest request =
                            Service.HttpsHealthChecks.Update(HttpsObject, project, name);
                        Operation operation = request.Execute();
                        AddGlobalOperation(project, operation, () =>
                        {
                            WriteObject(Service.HttpsHealthChecks.Get(project, name).Execute());
                        });
                    }
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }
    }
}
