// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <para type="synopsis">
    /// Gets Google Compute Engine url maps.
    /// </para>
    /// <para type="description">
    /// Lists url maps of a project, or gets a specific one.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GceUrlMap</code>
    ///   <para>This command lists all url maps for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceUrlMap "my-url-map"</code>
    ///   <para>This command gets the url map named "my-url-map"</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/urlMaps#resource)">
    /// [Url Map resource definition]
    /// </para>
    [Cmdlet(VerbsCommon.Get, "GceUrlMap", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(UrlMap))]
    public class GceGceUrlMapCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// The project the url maps belong to. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfProject)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the url map to get.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.OfProject:
                    WriteObject(GetAllProjectUrlMaps(Project), true);
                    break;
                case ParameterSetNames.ByName:
                    WriteObject(Service.UrlMaps.Get(Project, Name).Execute());
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private IEnumerable<UrlMap> GetAllProjectUrlMaps(string project)
        {
            UrlMapsResource.ListRequest request = Service.UrlMaps.List(project);
            do
            {
                UrlMapList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (UrlMap urlMap in response.Items)
                    {
                        yield return urlMap;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }
    }
}
