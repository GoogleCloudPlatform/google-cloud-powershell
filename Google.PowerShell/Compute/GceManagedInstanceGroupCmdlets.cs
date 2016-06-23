using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using Google.PowerShell.ComputeEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Google.PowerShell.Compute
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Compute Engine instance group managers.
    /// </para>
    /// <para type="description"> 
    /// Gets Google Compute Engine instance group managers.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceManagedInstanceGroup", DefaultParameterSetName = ParameterSetNames.ListProject)]
    class GetManagedInstanceGroupCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string ListProject = "ListProject";
            public const string ListZone = "ListZone";
            public const string ByName = "ByName";
            public const string ByUri = "ByUri";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the instance group.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ListProject)]
        [Parameter(ParameterSetName = ParameterSetNames.ListZone)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone the instance group is in.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ListZone, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance group to get.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The full uri of the managed instance group
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByUri, Mandatory = true)]
        public string Uri { get; set; }

        /// <summary>
        /// <para type="description">
        /// The InstanceGroupManager object to get.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true, ValueFromPipeline = true)]
        public InstanceGroupManager Object { get; set; }


        /// <summary>
        /// <para type="description">
        /// If set, will return ManagedInstance objects describing the state of the instances of this group,
        /// including whether they exist yet or not.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter InstanceStatus { get; set; }

        protected override void ProcessRecord()
        {
            IEnumerable<InstanceGroupManager> managers;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ListProject:
                    managers = GetProjectGroups();
                    break;
                case ParameterSetNames.ListZone:
                    managers = GetZoneGroups();
                    break;
                case ParameterSetNames.ByName:
                    managers = new[] { GetGroupByName() };
                    break;
                case ParameterSetNames.ByUri:
                    managers = new[] { GetGroupByUri() };
                    break;
                case ParameterSetNames.ByObject:
                    managers = new[] { GetGroupByObject() };
                    break;
                default:
                    throw new PSInvalidOperationException($"{ParameterSetName} is not a valid parameter set");
            }

            if (InstanceStatus)
            {
                WriteObject(GetInstances(managers), true);
            }
            else
            {
                WriteObject(managers, true);
            }
        }

        /// <summary>
        /// Gets the status objects for the managed instances.
        /// </summary>
        private IEnumerable<ManagedInstance> GetInstances(IEnumerable<InstanceGroupManager> managers)
        {
            foreach (InstanceGroupManager manager in managers)
            {
                string project = GetProjectNameFromUri(manager.SelfLink);
                string zone = manager.Zone;
                string name = manager.Name;
                InstanceGroupManagersResource.ListManagedInstancesRequest request =
                    Service.InstanceGroupManagers.ListManagedInstances(project, zone, name);
                InstanceGroupManagersListManagedInstancesResponse response = request.Execute();
                if (response.ManagedInstances != null)
                {
                    foreach (ManagedInstance instance in response.ManagedInstances)
                    {
                        yield return instance;
                    }
                }
            }
        }

        private InstanceGroupManager GetGroupByObject()
        {
            string project = GetProjectNameFromUri(Object.SelfLink);
            string zone = Object.Zone;
            string name = Object.Name;
            return Service.InstanceGroupManagers.Get(project, zone, name).Execute();
        }

        private InstanceGroupManager GetGroupByUri()
        {
            string project = GetProjectNameFromUri(Uri);
            string zone = GetZoneNameFromUri(Uri);
            string name = GetUriPart("instanceGroupManagers", Uri);
            return Service.InstanceGroupManagers.Get(project, zone, name).Execute();
        }

        private InstanceGroupManager GetGroupByName()
        {
            return Service.InstanceGroupManagers.Get(Project, Zone, Name).Execute();
        }

        private IEnumerable<InstanceGroupManager> GetZoneGroups()
        {
            InstanceGroupManagersResource.ListRequest request =
                Service.InstanceGroupManagers.List(Project, Zone);
            do
            {
                InstanceGroupManagerList response = request.Execute();
                foreach (InstanceGroupManager manager in response.Items)
                {
                    yield return manager;
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);

        }

        private IEnumerable<InstanceGroupManager> GetProjectGroups()
        {
            InstanceGroupManagersResource.AggregatedListRequest request =
                Service.InstanceGroupManagers.AggregatedList(Project);
            do
            {
                InstanceGroupManagerAggregatedList response = request.Execute();
                IEnumerable<InstanceGroupManager> allManagers =
                    response.Items.SelectMany(kvp => kvp.Value.InstanceGroupManagers);
                foreach (InstanceGroupManager manager in allManagers)
                {
                    yield return manager;
                }
                request.PageToken = response.NextPageToken;
            } while (request.PageToken != null && !Stopping);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a new Google Compute Engine instance group manager.
    /// </para>
    /// <para type="description"> 
    /// Creates a new Google Compute Engine instance group manager.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceManagedInstanceGroup")]
    public class AddManagedInstanceGroupCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByProperies = "ByProperties";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that will own the instance group.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone where the instance gorup will live.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance group.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByProperies, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance template to use when creating instances.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByProperies, Mandatory = true, Position = 1)]
        public string InstanceTemplate { get; set; }

        /// <summary>
        /// <para type="description">
        /// The base instance name for this group. Instances will take this name and append a hypen and a
        /// random four character string.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByProperies)]
        public string BaseInstanceName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The human readable description of this instance group.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByProperies)]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The URLs for all TargetPool resources to which instances in the instanceGroup field are added.
        /// The target pools automatically apply to all of the instances in the managed instance group.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByProperies)]
        public string[] TargetPool { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name you want to give to a port. Must have the same number of elements as PortNumber.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByProperies)]
        public string[] PortName { get; set; } = { };

        /// <summary>
        /// <para type="description">
        /// The number of the port you want to give a name. Must have the same number of elements as PortName.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByProperies)]
        public int[] PortNumber { get; set; } = { };

        /// <summary>
        /// <para type="description">
        /// A NamedPort object you want to include in the list of named ports.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByProperies)]
        public NamedPort[] NamedPort { get; set; }

        /// <summary>
        /// <para type="description">
        /// An InstanceGroupManager object used to create a new managed instance group.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public InstanceGroupManager Object { get; set; }

        protected override void ProcessRecord()
        {
            InstanceGroupManager manager;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByProperies:
                    manager = new InstanceGroupManager
                    {
                        Name = Name,
                        InstanceTemplate = InstanceTemplate,
                        Zone = Zone,
                        BaseInstanceName = BaseInstanceName,
                        Description = Description,
                        TargetPools = TargetPool,
                        NamedPorts = BuildNamedPorts()

                    };
                    break;
                case ParameterSetNames.ByObject:
                    manager = Object;
                    break;
                default:
                    throw new PSInvalidOperationException($"{ParameterSetName} is not a valid parameter set");
            }
            InstanceGroupManagersResource.InsertRequest request =
                Service.InstanceGroupManagers.Insert(manager, Project, Zone);
            Operation response = request.Execute();
            AddOperation(Project, Zone, response);
        }

        private List<NamedPort> BuildNamedPorts()
        {
            var ports = new List<NamedPort>();
            if (NamedPort != null)
            {
                ports.AddRange(NamedPort);
            }
            if (PortName.Length != PortNumber.Length)
            {
                throw new PSInvalidOperationException(
                    "PortName and PortNumber must have the same number of arguments.");
            }
            Func<string, int, NamedPort> buildNamedPort =
                (name, number) => new NamedPort { Name = name, Port = number };
            ports.AddRange(PortName.Zip(PortNumber, buildNamedPort));
            return ports;
        }
    }

    [Cmdlet(VerbsCommon.Remove, "GceManagedInstanceGroup")]
    public class RemoveManagedInstanceGroupCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the managed instance group.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone the managed instance group is in.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the managed instance group to delete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The managed instance group object to delete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public InstanceGroupManager Object { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    DeleteByName();
                    break;
                case ParameterSetNames.ByObject:
                    DeleteByObject();
                    break;
                default:
                    throw new PSInvalidOperationException($"{ParameterSetName} is not a valid parameter set");
            }
        }

        private void DeleteByObject()
        {
            string project = GetProjectNameFromUri(Object.SelfLink);
            string zone = Object.Zone;
            string name = Object.Name;
            AddOperation(project, zone, Service.InstanceGroupManagers.Delete(project, zone, name).Execute());
        }

        private void DeleteByName()
        {
            AddOperation(Project, Zone, Service.InstanceGroupManagers.Delete(Project, Zone, Name).Execute());
        }
    }
}
