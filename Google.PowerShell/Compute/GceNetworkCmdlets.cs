// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using Google.PowerShell.ComputeEngine;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net;

namespace Google.PowerShell.Compute
{
    /// <summary>
    /// <para type="synopsis">
    /// Creates a new Google Compute Engine network.
    /// </para>
    /// <para type="description">
    /// Creates a new Google Compute Engine network. The cmdlet will create the network
    /// in the default project if -Project is not used. By default, the network is
    /// created in without any subnets. To create a network with subnets automatically
    /// created, use -AutoSubnet switch. To create a network in legacy mode,
    /// which has a range and cannot have subnets, use the -IPv4Range parameter. Please
    /// note that using legacy network is not recommended as many newer Google Cloud Platform
    /// features are not supported on legacy networks and legacy networks may not be
    /// supported in the future.
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GceNetwork -Name "my-network"</code>
    ///   <para>Creates network "my-network" in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GceNetwork -Name "my-network" -Project "my-project" -AutoSubnet</code>
    ///   <para>
    ///   Creates a network "my-network" with auto subnet created in the project "my-project".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GceNetwork -Name "my-network" -IPv4Range 192.168.0.0/16</code>
    ///   <para>Creates a network "my-network" IPv4 range of 192.168.0.0/16.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/vpc/)">
    /// [Networks]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/vpc/legacy)">
    /// [Legacy Networks]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GceNetwork")]
    [OutputType(typeof(Network))]
    public class NewGceNetworkCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type = "description">
        /// The project to create the network in. Defaults to the gcloud config project.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type = "description">
        /// The name of the network to be created.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, Mandatory = true)]
        [ValidatePattern("[a-z]([-a-z0-9]*[a-z0-9])?")]
        public string Name { get; set; }

        /// <summary>
        /// <para type = "description">
        /// The description of the network to be created.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty()]
        public string Description { get; set; }

        /// <summary>
        /// <para type = "description">
        /// The IPv4 range of the network. Please note that using this parameter
        /// will create a legacy network that has range and cannot have subnets.
        /// This is not recommended as many Google Cloud features are not available
        /// on legacy networks.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty()]
        public string IPv4Range { get; set; }

        /// <summary>
        /// <para type = "description">
        /// If set to true, the network created will its subnets created automatically.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter AutoSubnet { get; set; }

        protected override void ProcessRecord()
        {
            try
            {
                NetworksResource.InsertRequest createRequest = Service.Networks.Insert(
                    CreateNetwork(), Project);
                Operation createNetwork = createRequest.Execute();
                WaitForGlobalOperation(Project, createNetwork, $"Creating network {Name}");

                NetworksResource.GetRequest getRequest = Service.Networks.Get(Project, Name);
                WriteObject(getRequest.Execute());
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"A network with the name '{Name}' already exists in project '{Project}'.",
                    ErrorCategory.InvalidArgument,
                    Name));
            }
        }

        private Network CreateNetwork()
        {
            if (AutoSubnet.IsPresent && IPv4Range != null)
            {
                throw new PSArgumentException(
                    "-IPv4Range parameter is used to create old style network that cannot have subnets.");
            }

            Network network = new Network()
            {
                Name = Name,
                Description = Description,
            };

            if (AutoSubnet.IsPresent)
            {
                network.AutoCreateSubnetworks = AutoSubnet;
            }
            else if (IPv4Range != null)
            {
                network.IPv4Range = IPv4Range;
            }

            return network;
        }
    }

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
