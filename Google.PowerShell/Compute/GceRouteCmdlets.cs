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
    /// </para>
    /// <para type="description">
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

        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.FromObject, Mandatory = true,
            ValueFromPipeline = true)]
        public Route Object { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.FromValues, Mandatory = true)]
        public string Name { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.FromValues, Mandatory = true)]
        public string DestinationIpRange { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.FromValues, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "SelfLink", TypeToTransform = typeof(Network))]
        public string Network { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.FromValues, Mandatory = true)]
        public long? Priority { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        public string Description { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        public string[] Tags { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        [PropertyByTypeTransformation(Property = "SelfLink", TypeToTransform = typeof(Network))]
        public string NextHopNetwork { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        [PropertyByTypeTransformation(Property = "SelfLink", TypeToTransform = typeof(Instance))]
        public string NextHopInstance { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        public string NextHopIp { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.FromValues)]
        public string NextHopVpnTunnel { get; set; }

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
                        Tags = Tags.ToList(),
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
    /// </para>
    /// <para type="description">
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceRoute")]
    public class GetGceRouteCmdlet : GceCmdlet
    {
        public string Project { get; set; }

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
