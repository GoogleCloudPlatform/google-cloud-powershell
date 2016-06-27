﻿using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using Google.PowerShell.ComputeEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;

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
    public class GetManagedInstanceGroupCmdlet : GceCmdlet
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
                string zone = GetZoneNameFromUri(manager.Zone);
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
            string zone = GetZoneNameFromUri(Object.Zone);
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
                IEnumerable<InstanceGroupManager> allManagers = response.Items.SelectMany(
                    kvp => kvp.Value?.InstanceGroupManagers ?? new InstanceGroupManager[0]);
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
        [PropertyByTypeTransformation(Property = "SelfLink", TypeToTransform = typeof(InstanceTemplate))]
        public string InstanceTemplate { get; set; }

        /// <summary>
        /// <para type="description">
        /// The target number of instances for this instance group to have.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByProperies, Mandatory = true, Position = 2)]
        public int TargetSize { get; set; }

        /// <summary>
        /// <para type="description">
        /// The base instance name for this group. Instances will take this name and append a hypen and a
        /// random four character string. Defaults to the group name.
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
                        BaseInstanceName = BaseInstanceName ?? Name,
                        Description = Description,
                        TargetPools = TargetPool,
                        NamedPorts = BuildNamedPorts(),
                        TargetSize = TargetSize

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

    /// <summary>
    /// <para type="synopsis">
    /// Removes a Google Compute Engine instance group manager.
    /// </para>
    /// <para type="description"> 
    /// Removes a Google Compute Engine instance group manager.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceManagedInstanceGroup", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.ByName)]
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
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
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
            string zone = GetZoneNameFromUri(Object.Zone);
            string name = Object.Name;
            if (ShouldProcess($"{project}/{zone}/{name}", "Remove Instance Group Manager"))
            {
                AddOperation(project, zone, Service.InstanceGroupManagers.Delete(project, zone, name).Execute());
            }
        }

        private void DeleteByName()
        {
            if (ShouldProcess($"{Project}/{Zone}/{Name}", "Remove Instance Group Manager"))
            {
                AddOperation(Project, Zone, Service.InstanceGroupManagers.Delete(Project, Zone, Name).Execute());
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Changes the data of a Google Compute Engine instance group manager.
    /// </para>
    /// <para type="description"> 
    /// Changes the data of a Google Compute Engine instance group manager. As a whole, the group can be
    /// resized, have its template set, and have its target pools set. Member instances can be abandoned,
    /// deleted and recreated.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GceManagedInstanceGroup", SupportsShouldProcess = true)]
    public class SetGceManagedInstanceGroupCmdelt : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string AbandonUri = "AbandonUri";
            public const string AbandonObject = "AbandonObject";
            public const string DeleteUri = "DeleteUri";
            public const string DeleteObject = "DeleteObject";
            public const string RecreateUri = "RecreateUri";
            public const string RecreateObject = "RecreateObject";
            public const string Resize = "Resize";
            public const string SetTemplate = "SetTemplate";
            public const string SetTargetPools = "SetTargetPools";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the managed instance group.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone the managed instance group is in.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the managed instance group to change.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will abandon the instance specified by InstanceUri or InstanceObject.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AbandonUri, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.AbandonObject, Mandatory = true)]
        public SwitchParameter Abandon { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will delete the instance specified by InstanceUri or InstanceObject.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.DeleteUri, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.DeleteObject, Mandatory = true)]
        public SwitchParameter Delete { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will recreate the instance specified by InstanceUri or InstanceObject.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.RecreateUri, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.RecreateObject, Mandatory = true)]
        public SwitchParameter Recreate { get; set; }

        /// <summary>
        /// <para type="description">
        /// The uri of the instance to Abandon, Delete or Recreate.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AbandonUri, Mandatory = true,
            ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.DeleteUri, Mandatory = true,
            ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.RecreateUri, Mandatory = true,
            ValueFromPipeline = true)]
        public string[] InstanceUri { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Instance object to Abandon, Delete or Recreate.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AbandonObject, Mandatory = true,
            ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.DeleteObject, Mandatory = true,
            ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.RecreateObject, Mandatory = true,
            ValueFromPipeline = true)]
        public Instance[] InstanceObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// The new target size of the instance group.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Resize, Mandatory = true)]
        public int Size { get; set; }

        /// <summary>
        /// <para type="description">
        /// Uri to the new template of the instance group.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.SetTemplate, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "SelfLink", TypeToTransform = typeof(InstanceTemplate))]
        public string Template { get; set; }

        /// <summary>
        /// <para type="description">
        /// The uris of the new set of target pools.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.SetTargetPools, Mandatory = true)]
        public string[] TargetPoolUri { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.AbandonUri:
                    AbandonUri();
                    break;
                case ParameterSetNames.AbandonObject:
                    AbandonObject();
                    break;
                case ParameterSetNames.DeleteUri:
                    DeleteUri();
                    break;
                case ParameterSetNames.DeleteObject:
                    DeleteObject();
                    break;
                case ParameterSetNames.RecreateUri:
                    RecreateUri();
                    break;
                case ParameterSetNames.RecreateObject:
                    RecreateObject();
                    break;
                case ParameterSetNames.Resize:
                    Resize();
                    break;
                case ParameterSetNames.SetTemplate:
                    SetTemplate();
                    break;
                case ParameterSetNames.SetTargetPools:
                    SetTargetPools();
                    break;
                default:
                    throw new PSInvalidOperationException($"{ParameterSetName} is not a valid parameter set");
            }
        }

        private void SetTemplate()
        {
            InstanceGroupManagersSetInstanceTemplateRequest body =
                new InstanceGroupManagersSetInstanceTemplateRequest
                {
                    InstanceTemplate = Template
                };
            if (ShouldProcess($"{Project}/{Zone}/{Name}", "Set Template"))
            {
                InstanceGroupManagersResource.SetInstanceTemplateRequest request =
                    Service.InstanceGroupManagers.SetInstanceTemplate(body, Project, Zone, Name);
                AddOperation(Project, Zone, request.Execute());
            }
        }


        private void SetTargetPools()
        {
            var body = new InstanceGroupManagersSetTargetPoolsRequest()
            {
                TargetPools = TargetPoolUri
            };
            if (ShouldProcess($"{Project}/{Zone}/{Name}", "Set Target Pools"))
            {
                InstanceGroupManagersResource.SetTargetPoolsRequest request =
                    Service.InstanceGroupManagers.SetTargetPools(body, Project, Zone, Name);
                AddOperation(Project, Zone, request.Execute());
            }
        }

        private void Resize()
        {
            if (ShouldProcess($"{Project}/{Zone}/{Name}", "Resize"))
            {
                Operation operation = Service.InstanceGroupManagers.Resize(Project, Zone, Name, Size).Execute();
                AddOperation(Project, Zone, operation);
            }
        }

        private void RecreateObject()
        {
            var body = new InstanceGroupManagersRecreateInstancesRequest
            {
                Instances = InstanceObject.Select(i => i.SelfLink).ToList()
            };
            if (ShouldProcess("Recreating instances: \n" + string.Join("\n", body.Instances), null,
                $"Managed Instance Group {Project}/{Zone}/{Name}"))
            {
                InstanceGroupManagersResource.RecreateInstancesRequest request =
                    Service.InstanceGroupManagers.RecreateInstances(body, Project, Zone, Name);
                AddOperation(Project, Zone, request.Execute());
            }
        }

        private void RecreateUri()
        {
            var body = new InstanceGroupManagersRecreateInstancesRequest
            {
                Instances = InstanceUri
            };
            if (ShouldProcess("Recreating instances: \n" + string.Join("\n", body.Instances), null,
                $"Managed Instance Group {Project}/{Zone}/{Name}"))
            {
                InstanceGroupManagersResource.RecreateInstancesRequest request =
                    Service.InstanceGroupManagers.RecreateInstances(body, Project, Zone, Name);
                AddOperation(Project, Zone, request.Execute());
            }
        }

        private void DeleteObject()
        {
            var body = new InstanceGroupManagersDeleteInstancesRequest
            {
                Instances = InstanceObject.Select(i => i.SelfLink).ToList()
            };
            if (ShouldProcess("Deleting instances: \n" + string.Join("\n", body.Instances), null,
                $"Managed Instance Group {Project}/{Zone}/{Name}"))
            {
                InstanceGroupManagersResource.DeleteInstancesRequest request =
                    Service.InstanceGroupManagers.DeleteInstances(body, Project, Zone, Name);
                AddOperation(Project, Zone, request.Execute());
            }
        }

        private void DeleteUri()
        {
            var body = new InstanceGroupManagersDeleteInstancesRequest
            {
                Instances = InstanceUri
            };
            if (ShouldProcess("Deleting instances: \n" + string.Join("\n", body.Instances), null,
                $"Managed Instance Group {Project}/{Zone}/{Name}"))
            {
                InstanceGroupManagersResource.DeleteInstancesRequest request =
                    Service.InstanceGroupManagers.DeleteInstances(body, Project, Zone, Name);
                AddOperation(Project, Zone, request.Execute());
            }
        }

        private void AbandonObject()
        {
            var body = new InstanceGroupManagersAbandonInstancesRequest
            {
                Instances = InstanceObject.Select(i => i.SelfLink).ToList()
            };
            if (ShouldProcess("Abandoning instances: \n" + string.Join("\n", body.Instances), null,
                $"Managed Instance Group {Project}/{Zone}/{Name}"))
            {
                InstanceGroupManagersResource.AbandonInstancesRequest request =
                    Service.InstanceGroupManagers.AbandonInstances(body, Project, Zone, Name);
                AddOperation(Project, Zone, request.Execute());
            }
        }

        private void AbandonUri()
        {
            var body = new InstanceGroupManagersAbandonInstancesRequest
            {
                Instances = InstanceUri
            };
            if (ShouldProcess("Abandoning instances: \n" + string.Join("\n", body.Instances), null,
                $"Managed Instance Group {Project}/{Zone}/{Name}"))
            {
                InstanceGroupManagersResource.AbandonInstancesRequest request =
                    Service.InstanceGroupManagers.AbandonInstances(body, Project, Zone, Name);
                AddOperation(Project, Zone, request.Execute());
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Waits for a Google Compute Engine managed instance group to be stable.
    /// </para>
    /// <para type="description"> 
    /// Waits for all of the instances of a managed instance group to reach normal running state.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Wait, "GceManagedInstanceGroup")]
    public class WaitGceManagedInstanceGroupCmdlet : GceCmdlet
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
        /// The name of the managed instance group to change.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public InstanceGroupManager Object { get; set; }

        protected override void ProcessRecord()
        {
            string project;
            string zone;
            string name;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    project = Project;
                    zone = Zone;
                    name = Name;
                    break;
                case ParameterSetNames.ByObject:
                    project = GetProjectNameFromUri(Object.SelfLink);
                    zone = GetZoneNameFromUri(Object.Zone);
                    name = Object.Name;
                    break;
                default:
                    throw new PSInvalidOperationException($"{ParameterSetName} is not a valid parameter set");
            }

            IList<ManagedInstance> instances;
            do
            {
                Thread.Sleep(150);
                InstanceGroupManagersListManagedInstancesResponse response =
                    Service.InstanceGroupManagers.ListManagedInstances(project, zone, name).Execute();
                instances = response.ManagedInstances;
            } while (instances.Any(i => i.CurrentAction != "NONE") && !Stopping);
        }
    }
}
