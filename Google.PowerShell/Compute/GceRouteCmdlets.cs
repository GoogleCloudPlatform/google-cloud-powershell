using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using Google.PowerShell.ComputeEngine;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace Google.PowerShell.Compute
{
    /// <summary>
    /// <para type="synopsis">
    /// Adds a new networking route.
    /// </para>
    /// <para type="description">
    /// Adds a new networking route.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceRoute")]
    public class AddGceRouteCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string FromValues = "FromValues";
            public const string FromObject = "FromObject";
        }

        /// <summary>
        /// <para type = "description">
        /// The project to add the route to. Defaults to the gcloud config project.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type = "description">
        /// An object describing a route to add.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromObject, Mandatory = true,
            ValueFromPipeline = true)]
        public Route Object { get; set; }

        /// <summary>
        /// <para type = "description">
        /// The name of the route to add.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromValues, Mandatory = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type = "description">
        /// The destination range of outgoing packets that this route applies to.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromValues, Mandatory = true)]
        public string DestinationIpRange { get; set; }

        /// <summary>
        /// <para type = "description">
        /// The network this route applies to. Can be either a url, or a network object from Get-GceNetwork.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromValues, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "SelfLink", TypeToTransform = typeof(Network))]
        public string Network { get; set; }

        /// <summary>
        /// <para type = "description">
        /// The priority of this route. Priority is used to break ties in cases where there
        /// is more than one matching route of equal prefix length. In the case of two routes
        /// with equal prefix length, the one with the lowest-numbered priority value wins.
        /// Default value is 1000. Valid range is 0 through 65535.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        public long? Priority { get; set; }

        /// <summary>
        /// <para type = "description">
        /// Human readable description of this route.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        public string Description { get; set; }

        /// <summary>
        /// <para type = "description">
        /// Instance tag(s) this route applies to.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        public string[] Tag { get; set; }

        /// <summary>
        /// <para type = "description">
        /// The local network that should handle matching packets. Can be either a url or a network object from
        /// Get-GceNetwork.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        [PropertyByTypeTransformation(Property = "SelfLink", TypeToTransform = typeof(Network))]
        public string NextHopNetwork { get; set; }

        /// <summary>
        /// <para type = "description">
        /// The instance that should handle matching packets. Can be either a url, or an instance from
        /// Get-GceInstance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        [PropertyByTypeTransformation(Property = "SelfLink", TypeToTransform = typeof(Instance))]
        public string NextHopInstance { get; set; }

        /// <summary>
        /// <para type = "description">
        /// The IP Address of an instance that should handle matching packets.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        public string NextHopIp { get; set; }

        /// <summary>
        /// <para type = "description">
        /// The url of a VpnTunnel that should handle matching packets.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        public string NextHopVpnTunnel { get; set; }

        /// <summary>
        /// <para type = "description">
        /// The url to a gateway that should handle matching packets.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        public string NextHopGateway { get; set; }

        protected override void ProcessRecord()
        {
            string project = Project;
            Route route;
            switch (ParameterSetName)
            {
                case ParameterSetNames.FromObject:
                    route = Object;
                    break;
                case ParameterSetNames.FromValues:
                    route = new Route
                    {
                        Name = Name,
                        Description = Description,
                        DestRange = DestinationIpRange,
                        Network = Network,
                        Priority = Priority,
                        Tags = Tag.ToList(),
                        NextHopNetwork = NextHopNetwork,
                        NextHopInstance = NextHopInstance,
                        NextHopIp = NextHopIp,
                        NextHopGateway = NextHopGateway,
                        NextHopVpnTunnel = NextHopVpnTunnel
                    };
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            RoutesResource.InsertRequest request = Service.Routes.Insert(route, project);
            Operation operation = request.Execute();

            string name = route.Name;
            AddGlobalOperation(project, operation, () =>
            {
                WriteObject(Service.Routes.Get(project, name).Execute());
            });
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets or lists networking routes.
    /// </para>
    /// <para type="description">
    /// Lists all the networking routes for a project, or gets a specific one by project and name.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceRoute")]
    public class GetGceRouteCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type = "description">
        /// The project of the route to get. Defaults to the gcloud config project.
        /// </para>
        /// </summary>
        public string Project { get; set; }

        /// <summary>
        /// <para type = "description">
        /// 
        /// </para>
        /// </summary>
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            if (string.IsNullOrEmpty(Name))
            {
                WriteObject(GetAllProjectRoutes(), true);
            }
            else
            {
                WriteObject(Service.Routes.Get(Project, Name).Execute());
            }
        }

        private IEnumerable<Route> GetAllProjectRoutes()
        {
            RoutesResource.ListRequest request = Service.Routes.List(Project);
            do
            {
                RouteList respone = request.Execute();
                if (respone.Items != null)
                {
                    foreach (Route route in respone.Items)
                    {
                        yield return route;
                    }
                }
                request.PageToken = respone.NextPageToken;
            }
            while (!Stopping && request.PageToken != null);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// </para>
    /// <para type="description">
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceRoute", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.ByName)]
    public class RemoveGceRouteCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        public string Project { get; set; }
        public string Name { get; set; }
        public Route Object { get; set; }
        protected override void ProcessRecord()
        {
            string project;
            string name;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    project = Project;
                    name = Name;
                    break;
                case ParameterSetNames.ByObject:
                    project = GetProjectNameFromUri(Object.SelfLink);
                    name = Object.Name;
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            if (ShouldProcess($"{project}/{name}", "Remove GceRoute"))
            {
                Operation operation = Service.Routes.Delete(project, name).Execute();
                AddGlobalOperation(project, operation);
            }
        }
    }
}
