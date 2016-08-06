﻿using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using Google.PowerShell.ComputeEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// <example>
    /// <code>PS C:\> Get-GceManagedInstanceGroup</code>
    /// <para>Lists all managed instance groups for the default project.</para>
    /// </example>
    /// <example>
    /// <code>PS C:\> Get-GceManagedInstanceGroup -Zone "us-central1-a"</code>
    /// <para>Lists all managed instance groups for the default project in the given zone.</para>
    /// </example>
    /// <example>
    /// <code>PS C:\> Get-GceManagedInstanceGroup "my-instance-group" -InstanceStatus</code>
    /// <para>Lists the status of all members of the instance group named "my-instance-group" in the default
    /// project and zone.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceManagedInstanceGroup", DefaultParameterSetName = ParameterSetNames.ListProject)]
    [OutputType(typeof(InstanceGroupManager), typeof(ManagedInstance))]
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
                    throw UnknownParameterSetException;
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
    /// <example>
    /// <code>
    /// PS C:\> $template = Get-GceInstanceTemplate "my-template"
    /// PS C:\> Add-GceManagedInstanceGroup "my-instance-group" $template 4
    /// </code>
    /// <para>Creates a new managed instance group named "my-instance-group". The instance of the group will
    /// be created from template "my-template" and the group will create four instances.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceManagedInstanceGroup")]
    [OutputType(typeof(InstanceGroupManager))]
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
        /// The instance template to use when creating instances. Can be a string URL to a template, or an
        /// InstanceTemplate object from Get-GceInstanceTemplate.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByProperies, Mandatory = true, Position = 1)]
        [PropertyByTypeTransformation(Property = nameof(Apis.Compute.v1.Data.InstanceTemplate.SelfLink),
            TypeToTransform = typeof(InstanceTemplate))]
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
                    throw UnknownParameterSetException;
            }
            InstanceGroupManagersResource.InsertRequest request =
                Service.InstanceGroupManagers.Insert(manager, Project, Zone);
            Operation operation = request.Execute();
            AddZoneOperation(Project, Zone, operation, () =>
            {
                WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, manager.Name));
            });
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
    /// <example>
    /// <code>PS C:\> Remove-GceManagedInstanceGroup "my-instance-group"</code>
    /// <para>Removes the instance group named "my-instance-group" in the default project and zone.</para>
    /// </example>
    /// <example>
    /// <code>PS C:\> Get-GceManagedInstanceGroup -Zone "us-central1-a" | Remove-GceManagedInstanceGroup</code>
    /// <para>Removes all managed instance groups of the default project in zone "us-central1-a".</para>
    /// </example>
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
                    throw UnknownParameterSetException;
            }
        }

        private void DeleteByObject()
        {
            string project = GetProjectNameFromUri(Object.SelfLink);
            string zone = GetZoneNameFromUri(Object.Zone);
            string name = Object.Name;
            if (ShouldProcess($"{project}/{zone}/{name}", "Remove Instance Group Manager"))
            {
                Operation operation = Service.InstanceGroupManagers.Delete(project, zone, name).Execute();
                AddZoneOperation(project, zone, operation);
            }
        }

        private void DeleteByName()
        {
            if (ShouldProcess($"{Project}/{Zone}/{Name}", "Remove Instance Group Manager"))
            {
                Operation operation = Service.InstanceGroupManagers.Delete(Project, Zone, Name).Execute();
                AddZoneOperation(Project, Zone, operation);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Changes the data of a Google Compute Engine instance group manager.
    /// </para>
    /// <para type="description"> 
    /// Changes the data of a Google Compute Engine instance group manager. As a whole, the group can be
    /// resized, have its template set, or have its target pools set. Member instances can be abandoned,
    /// deleted, or recreated.
    /// </para>
    /// <example>
    /// <code>PS C:\> Get-GceInstance "my-instance-1" | Set-ManagedInstanceGroup "my-group" -Abandon</code>
    /// <para> Abandons the instance named "my-instance-1". The instance will still exist, but will no longer 
    /// be a member of the instance group "my-group". The size of the instance group will decrease to match.</para>
    /// </example>
    /// <example>
    /// <code>
    /// PS C:\> $instanceUri = (Get-GceInstance "my-instance-2").SelfLink
    /// PS C:\> Set-ManagedInstanceGroup "my-group" -Delete -InstanceUri $instanceUri
    /// </code>
    /// <para> Deletes the instance "my-instance-2". The size of the instance group will decrease to match.</para>
    /// </example>
    /// <example>
    /// <code>PS C:\> Set-GceManagedInstanceGroup "my-group" -Size 5</code>
    /// <para>Changes the target size of managed instance group "my-group" to be 5.</para>
    /// </example>
    /// <example>
    /// <code>
    /// PS C:\> $template = Get-GceInstanceTemplate "new-template"
    /// PS C:\> Set-GceManagedInstanceGroup "my-group" -Template $template
    /// </code>
    /// <para>The tempalte "new-template" becomes the template for all new instances created by managed
    /// instance group "my-group"</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GceManagedInstanceGroup", SupportsShouldProcess = true)]
    [OutputType(typeof(InstanceGroupManager))]
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
                    throw UnknownParameterSetException;
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
                Operation operation = request.Execute();
                AddZoneOperation(Project, Zone, operation, () =>
                {
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name));
                });
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
                Operation operation = request.Execute();
                AddZoneOperation(Project, Zone, operation, () =>
                {
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name));
                });
            }
        }

        private void Resize()
        {
            if (ShouldProcess($"{Project}/{Zone}/{Name}", "Resize"))
            {
                InstanceGroupManagersResource.ResizeRequest request =
                    Service.InstanceGroupManagers.Resize(Project, Zone, Name, Size);
                Operation operation = request.Execute();
                AddZoneOperation(Project, Zone, operation, () =>
                {
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name));
                });
            }
        }

        private void RecreateObject()
        {
            var body = new InstanceGroupManagersRecreateInstancesRequest
            {
                Instances = InstanceObject.Select(i => i.SelfLink).ToList()
            };
            if (ShouldProcess("Recreating instances: " + JoinInstanceNames(body.Instances), null,
                $"Managed Instance Group {Project}/{Zone}/{Name}"))
            {
                InstanceGroupManagersResource.RecreateInstancesRequest request =
                    Service.InstanceGroupManagers.RecreateInstances(body, Project, Zone, Name);
                Operation operation = request.Execute();
                AddZoneOperation(Project, Zone, operation, () =>
                {
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name));
                });
            }
        }

        private void RecreateUri()
        {
            var body = new InstanceGroupManagersRecreateInstancesRequest
            {
                Instances = InstanceUri
            };
            if (ShouldProcess("Recreating instances: " + JoinInstanceNames(body.Instances), null,
                $"Managed Instance Group {Project}/{Zone}/{Name}"))
            {
                InstanceGroupManagersResource.RecreateInstancesRequest request =
                    Service.InstanceGroupManagers.RecreateInstances(body, Project, Zone, Name);
                Operation operation = request.Execute();
                AddZoneOperation(Project, Zone, operation, () =>
                {
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name));
                });
            }
        }

        private void DeleteObject()
        {
            var body = new InstanceGroupManagersDeleteInstancesRequest
            {
                Instances = InstanceObject.Select(i => i.SelfLink).ToList()
            };
            if (ShouldProcess("Deleting instances: " + JoinInstanceNames(body.Instances), null,
                $"Managed Instance Group {Project}/{Zone}/{Name}"))
            {
                InstanceGroupManagersResource.DeleteInstancesRequest request =
                    Service.InstanceGroupManagers.DeleteInstances(body, Project, Zone, Name);
                Operation operation = request.Execute();
                AddZoneOperation(Project, Zone, operation, () =>
                {
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name));
                });
            }
        }

        private void DeleteUri()
        {
            var body = new InstanceGroupManagersDeleteInstancesRequest
            {
                Instances = InstanceUri
            };
            if (ShouldProcess("Deleting instances: " + JoinInstanceNames(body.Instances), null,
                $"Managed Instance Group {Project}/{Zone}/{Name}"))
            {
                InstanceGroupManagersResource.DeleteInstancesRequest request =
                    Service.InstanceGroupManagers.DeleteInstances(body, Project, Zone, Name);
                Operation operation = request.Execute();
                AddZoneOperation(Project, Zone, operation, () =>
                {
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name));
                });
            }
        }

        private void AbandonObject()
        {
            var body = new InstanceGroupManagersAbandonInstancesRequest
            {
                Instances = InstanceObject.Select(i => i.SelfLink).ToList()
            };
            if (ShouldProcess("Abandoning instances: " + JoinInstanceNames(body.Instances), null,
                $"Managed Instance Group {Project}/{Zone}/{Name}"))
            {
                InstanceGroupManagersResource.AbandonInstancesRequest request =
                    Service.InstanceGroupManagers.AbandonInstances(body, Project, Zone, Name);
                Operation operation = request.Execute();
                AddZoneOperation(Project, Zone, operation, () =>
                {
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name));
                });
            }
        }

        private void AbandonUri()
        {
            var body = new InstanceGroupManagersAbandonInstancesRequest
            {
                Instances = InstanceUri
            };
            if (ShouldProcess("Abandoning instances: " + JoinInstanceNames(body.Instances), null,
                $"Managed Instance Group {Project}/{Zone}/{Name}"))
            {
                InstanceGroupManagersResource.AbandonInstancesRequest request =
                    Service.InstanceGroupManagers.AbandonInstances(body, Project, Zone, Name);
                Operation operation = request.Execute();
                AddZoneOperation(Project, Zone, operation, () =>
                {
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name));
                });
            }
        }

        private static string JoinInstanceNames(IEnumerable<string> instanceUris)
        {
            return string.Join(", ", instanceUris.Select(uri => GetUriPart("instances", uri)));
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Waits for a Google Compute Engine managed instance group to be stable.
    /// </para>
    /// <para type="description"> 
    /// Waits for all of the instances of a managed instance group to reach normal running state.
    /// </para>
    /// <example>
    /// <code>PS C:\> Wait-GceManagedInstanceGroup "my-group" -Timeout 30</code>
    /// <para>Waits for the managed instance group "my-group" to reach a noraml running state for up to 30
    /// seconds.</para>
    /// </example>
    /// <example>
    /// <code>PS C:\> Get-GceManagedInstanceGroup -Zone "us-central1-a" | Wait-GceManagedInstanceGroup</code>
    /// <para>Waits for all maanged instance groups in zone us-central1-a to reach a normal running state.</para>
    /// </example>
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
        /// The name of the managed instance group to wait on.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The mananged instance group object to wait on.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public InstanceGroupManager Object { get; set; }

        /// <summary>
        /// <para type="description">
        /// The maximum number of seconds to wait for each managed instance group. -1, the default, waits until
        /// all instances have no current action, no matter how long it takes. If the timeout expires, the wait
        /// will end with a warning.
        /// </para>
        /// </summary>
        [Parameter(Position = 1)]
        public int Timeout { get; set; } = -1;

        protected override void ProcessRecord()
        {
            InstanceGroupManagersResource.ListManagedInstancesRequest request;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    request = Service.InstanceGroupManagers.ListManagedInstances(Project, Zone, Name);
                    break;
                case ParameterSetNames.ByObject:
                    string project = GetProjectNameFromUri(Object.SelfLink);
                    string zone = GetZoneNameFromUri(Object.Zone);
                    string name = Object.Name;
                    request = Service.InstanceGroupManagers.ListManagedInstances(project, zone, name);
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            IList<ManagedInstance> instances = request.Execute().ManagedInstances;
            while (instances != null && instances.Any(i => i.CurrentAction != "NONE") && !Stopping)
            {
                if (Timeout >= 0 && stopwatch.Elapsed.Seconds > Timeout)
                {
                    WriteWarning("Wait-GceManagedInstanceGroup timed out for " +
                                 $"{request.Project}/{request.Zone}/{request.InstanceGroupManager}");
                    break;
                }
                Thread.Sleep(150);
                instances = request.Execute().ManagedInstances;
            }
        }
    }
}
