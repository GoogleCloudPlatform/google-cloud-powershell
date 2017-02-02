// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using Google.PowerShell.ComputeEngine;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.Compute
{
    /// <summary>
    /// <para type="synopsis">
    /// Get data about the networks a project has.
    /// </para>
    /// <para type="description">
    /// Get data about the networks a project has. This includes its name, id, and subnetworks.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GceNetwork</code>
    ///   <para>Lists all networks in set up for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceNetwork "default"</code>
    ///   <para>Gets the default network for the default project.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/networks#resource)">
    /// [Network resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceNetwork")]
    [OutputType(typeof(Network))]
    public class GetGceNetworkCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type = "description">
        /// The project to get the networks of. Defaults to the gcloud config project.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type = "description">
        /// The name of the network to get.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            if (Name == null)
            {
                WriteObject(GetAllProjectNetworks(), true);
            }
            else
            {
                WriteObject(Service.Networks.Get(Project, Name).Execute());
            }
        }

        private IEnumerable<Network> GetAllProjectNetworks()
        {
            NetworksResource.ListRequest request = Service.Networks.List(Project);
            do
            {
                NetworkList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (Network network in response.Items)
                    {
                        yield return network;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }
    }
}
