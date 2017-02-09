// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Compute Engine backend services.
    /// </para>
    /// <para type="description">
    /// Lists backend services of a project, or gets a specific one.
    /// </para>
    /// <example>
    /// <code>PS C:\> Get-GceBackendService</code>
    /// <para>This command lists all backend services for the default project.</para>
    /// </example>
    /// <example>
    /// <code>PS C:\> Get-GceBackendService "my-backendservice"</code>
    /// <para>This command gets the backend service named "my-backendservice".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/backendServices#resource-representations)">
    /// [Backend resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceBackendService", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(BackendService))]
    public class GetGceBackendServiceCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// The project the backend services belong to. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfProject)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the backend service to get.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    WriteObject(Service.BackendServices.Get(Project, Name).Execute());
                    break;
                case ParameterSetNames.OfProject:
                    WriteObject(GetAllProjectBackendServices(Project), true);
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private IEnumerable<BackendService> GetAllProjectBackendServices(string project)
        {
            BackendServicesResource.ListRequest request = Service.BackendServices.List(project);
            do
            {
                BackendServiceList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (BackendService backendService in response.Items)
                    {
                        yield return backendService;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }
    }
}
