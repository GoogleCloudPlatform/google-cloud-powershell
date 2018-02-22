// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using Google.PowerShell.ComputeEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Net;
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
    ///   <code>PS C:\> Get-GceManagedInstanceGroup</code>
    ///   <para>Lists all managed instance groups for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceManagedInstanceGroup -Zone "us-central1-a"</code>
    ///   <para>Lists all managed instance groups for the default project in the given zone.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceManagedInstanceGroup "my-instance-group" -InstanceStatus</code>
    ///   <para>
    ///   Lists the status of all members of the instance group named "my-instance-group" in the default
    ///   project and zone.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instanceGroupManagers#resource)">
    /// [Managed Instance Group resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceManagedInstanceGroup", DefaultParameterSetName = ParameterSetNames.ListProject)]
    [OutputType(typeof(InstanceGroupManager), typeof(ManagedInstance))]
    public class GetManagedInstanceGroupCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string ListProject = "ListProject";
            public const string ListRegion = "ListRegion";
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
        [Parameter(ParameterSetName = ParameterSetNames.ListRegion)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

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
        /// The zone the instance group is in.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ListRegion, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

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
                case ParameterSetNames.ListRegion:
                    managers = GetRegionGroups();
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
            string region = GetRegionNameFromUri(Uri);
            string name = GetUriPart("instanceGroupManagers", Uri);

            if (!string.IsNullOrWhiteSpace(region))
            {
                return Service.RegionInstanceGroupManagers.Get(project, region, name).Execute();
            }

            string zone = GetZoneNameFromUri(Uri);
            return Service.InstanceGroupManagers.Get(project, zone, name).Execute();
        }

        /// <summary>
        /// If -Region and -Zone are specified together, returns an error.
        /// If -Region is specified, finds a regional managed instance group
        /// with name Name.
        /// If -Zone is specified, finds a zonal managed instance group with
        /// name Name.
        /// If neither -Region or -Zone is specified, tries to find a zonal
        /// managed instance group with name Name in the default zone.
        /// If that fails, tries to find a regional managed instance group
        /// with name Name in the default region.
        /// </summary>
        private InstanceGroupManager GetGroupByName()
        {
            bool regionSpecified = MyInvocation.BoundParameters.ContainsKey(nameof(Region));
            bool zoneSpecified = MyInvocation.BoundParameters.ContainsKey(nameof(Zone));
            if (regionSpecified && zoneSpecified)
            {
                throw new PSInvalidOperationException(
                    "Parameters -Region and -Zone cannot be used together with -Name.");
            }

            if (regionSpecified)
            {
                return Service.RegionInstanceGroupManagers.Get(Project, Region, Name).Execute();
            }

            if (zoneSpecified)
            {
                return Service.InstanceGroupManagers.Get(Project, Zone, Name).Execute();
            }

            InstanceGroupManager manager;
            try
            {
                manager = Service.InstanceGroupManagers.Get(Project, Zone, Name).Execute();
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                manager = Service.RegionInstanceGroupManagers.Get(Project, Region, Name).Execute();
            }

            return manager;
        }

        private IEnumerable<InstanceGroupManager> GetZoneGroups()
        {
            InstanceGroupManagersResource.ListRequest request =
                Service.InstanceGroupManagers.List(Project, Zone);
            do
            {
                InstanceGroupManagerList response = request.Execute();
                var managers = response.Items ?? Enumerable.Empty<InstanceGroupManager>();
                foreach (InstanceGroupManager manager in managers)
                {
                    yield return manager;
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }


        private IEnumerable<InstanceGroupManager> GetRegionGroups()
        {
            RegionInstanceGroupManagersResource.ListRequest request =
                Service.RegionInstanceGroupManagers.List(Project, Region);
            do
            {
                RegionInstanceGroupManagerList response = request.Execute();
                var managers = response.Items ?? Enumerable.Empty<InstanceGroupManager>();
                foreach (InstanceGroupManager manager in managers)
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
    ///   <code>
    ///   PS C:\> $template = Get-GceInstanceTemplate "my-template"
    ///   PS C:\> Add-GceManagedInstanceGroup "my-instance-group" $template 4
    ///   </code>
    ///   <para>
    ///   Creates a new managed instance group named "my-instance-group". The instance of the group will
    ///   be created from template "my-template" and the group will create four instances.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instanceGroupManagers#resource)">
    /// [Managed Instance Group resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceManagedInstanceGroup", DefaultParameterSetName =
        ParameterSetNames.ByZoneProperties)]
    [OutputType(typeof(InstanceGroupManager))]
    public class AddManagedInstanceGroupCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByZoneProperties = "ByZoneProperties";
            public const string ByRegionProperties = "ByRegionProperties";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that will own the instance group.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone where the instance gorup will live.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByZoneProperties)]
        [Parameter(ParameterSetName = ParameterSetNames.ByObject)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The region where the instance gorup will live.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByRegionProperties)]
        [Parameter(ParameterSetName = ParameterSetNames.ByObject)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance group.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByZoneProperties, Mandatory = true, Position = 0)]
        [Parameter(ParameterSetName = ParameterSetNames.ByRegionProperties, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The instance template to use when creating instances. Can be a string URL to a template, or an
        /// InstanceTemplate object from Get-GceInstanceTemplate.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByZoneProperties, Mandatory = true, Position = 1)]
        [Parameter(ParameterSetName = ParameterSetNames.ByRegionProperties, Mandatory = true, Position = 1)]
        [PropertyByTypeTransformation(Property = nameof(Apis.Compute.v1.Data.InstanceTemplate.SelfLink),
            TypeToTransform = typeof(InstanceTemplate))]
        public string InstanceTemplate { get; set; }

        /// <summary>
        /// <para type="description">
        /// The target number of instances for this instance group to have.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByZoneProperties, Mandatory = true, Position = 2)]
        [Parameter(ParameterSetName = ParameterSetNames.ByRegionProperties, Mandatory = true, Position = 2)]
        public int TargetSize { get; set; }

        /// <summary>
        /// <para type="description">
        /// The base instance name for this group. Instances will take this name and append a hypen and a
        /// random four character string. Defaults to the group name.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByZoneProperties)]
        [Parameter(ParameterSetName = ParameterSetNames.ByRegionProperties)]
        public string BaseInstanceName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The human readable description of this instance group.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByZoneProperties)]
        [Parameter(ParameterSetName = ParameterSetNames.ByRegionProperties)]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The URLs for all TargetPool resources to which instances in the instanceGroup field are added.
        /// The target pools automatically apply to all of the instances in the managed instance group.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByZoneProperties)]
        [Parameter(ParameterSetName = ParameterSetNames.ByRegionProperties)]
        public string[] TargetPool { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name you want to give to a port. Must have the same number of elements as PortNumber.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByZoneProperties)]
        [Parameter(ParameterSetName = ParameterSetNames.ByRegionProperties)]
        public string[] PortName { get; set; } = { };

        /// <summary>
        /// <para type="description">
        /// The number of the port you want to give a name. Must have the same number of elements as PortName.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByZoneProperties)]
        [Parameter(ParameterSetName = ParameterSetNames.ByRegionProperties)]
        public int[] PortNumber { get; set; } = { };

        /// <summary>
        /// <para type="description">
        /// A NamedPort object you want to include in the list of named ports.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByZoneProperties)]
        [Parameter(ParameterSetName = ParameterSetNames.ByRegionProperties)]
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
            bool isZoneInstanceGroup = true;

            switch (ParameterSetName)
            {
                case ParameterSetNames.ByRegionProperties:
                    isZoneInstanceGroup = false;
                    goto case ParameterSetNames.ByZoneProperties;
                case ParameterSetNames.ByZoneProperties:
                    manager = new InstanceGroupManager
                    {
                        Name = Name,
                        InstanceTemplate = InstanceTemplate,
                        BaseInstanceName = BaseInstanceName ?? Name,
                        Description = Description,
                        TargetPools = TargetPool,
                        NamedPorts = BuildNamedPorts(),
                        TargetSize = TargetSize
                    };
                    break;
                case ParameterSetNames.ByObject:
                    manager = Object;
                    isZoneInstanceGroup = ProcessPipedObjectArguments();
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            if (isZoneInstanceGroup)
            {
                AddZoneManagedInstanceGroup(manager, Project, Zone);
            }
            else
            {
                AddRegionManagedInstanceGroup(manager, Project, Region);
            }
        }

        /// <summary>
        /// Processes the arguments when the cmdlet receives a piped ManagedInstanceGroup.
        /// This function will return true if we need to create a zone instance
        /// and return false if we need to create a region.
        /// </summary>
        private bool ProcessPipedObjectArguments()
        {
            bool regionSpecified = MyInvocation.BoundParameters.ContainsKey(nameof(Region));
            bool projectSpecified = MyInvocation.BoundParameters.ContainsKey(nameof(Project));
            bool zoneSpecified = MyInvocation.BoundParameters.ContainsKey(nameof(Zone));

            if (regionSpecified && zoneSpecified)
            {
                throw new PSInvalidOperationException(
                    "Parameters -Region and -Zone cannot be used together with -Object.");
            }

            // Extracts Project from the object if -Project not used.
            if (!projectSpecified && !string.IsNullOrWhiteSpace(Object.SelfLink))
            {
                Project = GetProjectNameFromUri(Object.SelfLink);
            }

            if (regionSpecified)
            {
                return false;
            }

            if (zoneSpecified)
            {
                return true;
            }

            // If we reach here, then regionSpecified and zoneSpecified are both false.
            // So we check the object for clue.
            if (!string.IsNullOrWhiteSpace(Object.Zone))
            {
                Zone = GetZoneNameFromUri(Object.Zone);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(Object.Region))
            {
                Region = GetRegionNameFromUri(Object.Region);
                return false;
            }

            // Creates a zone by default.
            return true;
        }

        private void AddZoneManagedInstanceGroup(InstanceGroupManager manager,
            string project, string zone)
        {
            InstanceGroupManagersResource.InsertRequest request =
                Service.InstanceGroupManagers.Insert(manager, project, zone);
            try
            {
                Operation operation = request.Execute();
                AddZoneOperation(project, zone, operation, () =>
                {
                    WriteObject(Service.InstanceGroupManagers.Get(project, zone, manager.Name).Execute());
                });
            }
            catch (GoogleApiException apiEx) when (apiEx.HttpStatusCode == HttpStatusCode.Conflict)
            {
                WriteResourceExistsError(
                    exceptionMessage: $"Instance Group '{manager.Name}' already exists " +
                      $"in zone '{zone}' of project '{project}'",
                    errorId: "InstanceAlreadyExists",
                    targetObject: manager);
            }
        }

        private void AddRegionManagedInstanceGroup(InstanceGroupManager manager,
            string project, string region)
        {
            RegionInstanceGroupManagersResource.InsertRequest request =
                Service.RegionInstanceGroupManagers.Insert(manager, project, region);
            try
            {
                Operation operation = request.Execute();
                AddRegionOperation(project, region, operation, () =>
                {
                    WriteObject(Service.RegionInstanceGroupManagers.Get(project, region, manager.Name).Execute());
                });
            }
            catch (GoogleApiException apiEx) when (apiEx.HttpStatusCode == HttpStatusCode.Conflict)
            {
                WriteResourceExistsError(
                    exceptionMessage: $"Instance Group '{manager.Name}' already exists " +
                      $"in region '{region}' of project '{project}'",
                    errorId: "InstanceAlreadyExists",
                    targetObject: manager);
            }
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
    ///   <code>PS C:\> Remove-GceManagedInstanceGroup "my-instance-group"</code>
    ///   <para>Removes the instance group named "my-instance-group" in the default project and zone.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceManagedInstanceGroup -Zone "us-central1-a" | Remove-GceManagedInstanceGroup</code>
    ///   <para>Removes all managed instance groups of the default project in zone "us-central1-a".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instanceGroupManagers#resource)">
    /// [Managed Instance Group resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceManagedInstanceGroup", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.ByNameZone)]
    public class RemoveManagedInstanceGroupCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByNameZone = "ByNameZone";
            public const string ByObject = "ByObject";
            public const string ByNameRegion = "ByNameRegion";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the managed instance group.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameZone)]
        [Parameter(ParameterSetName = ParameterSetNames.ByNameRegion)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone the managed instance group is in.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameZone)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The region the regional managed instance group is in.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameRegion)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the managed instance group to delete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameZone, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByNameRegion, Mandatory = true,
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
                case ParameterSetNames.ByNameRegion:
                    DeleteByRegion(Project, Region, Name);
                    break;
                case ParameterSetNames.ByNameZone:
                    DeleteByZone(Project, Zone, Name);
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
            string name = Object.Name;
            if (!string.IsNullOrWhiteSpace(Object.Region))
            {
                string region = GetRegionNameFromUri(Object.Region);
                DeleteByRegion(project, region, name);
            }
            else
            {
                string zone = GetZoneNameFromUri(Object.Zone);
                DeleteByZone(project, zone, name);
            }
        }

        private void DeleteByZone(string project, string zone, string name)
        {
            if (ShouldProcess($"{project}/{zone}/{name}", "Remove Instance Group Manager"))
            {
                Operation operation = Service.InstanceGroupManagers.Delete(project, zone, name).Execute();
                AddZoneOperation(project, zone, operation);
            }
        }

        private void DeleteByRegion(string project, string region, string name)
        {
            if (ShouldProcess($"{project}/{region}/{name}", "Remove Instance Group Manager"))
            {
                Operation operation = Service.RegionInstanceGroupManagers.Delete(project, region, name).Execute();
                AddRegionOperation(project, region, operation);
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
    ///   <code>PS C:\> Get-GceInstance "my-instance-1" | Set-ManagedInstanceGroup "my-group" -Abandon</code>
    ///   <para>
    ///   Abandons the instance named "my-instance-1". The instance will still exist, but will no longer 
    ///   be a member of the instance group "my-group". The size of the instance group will decrease to match.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> $instanceUri = (Get-GceInstance "my-instance-2").SelfLink
    ///   PS C:\> Set-ManagedInstanceGroup "my-group" -Delete -InstanceUri $instanceUri
    ///   </code>
    ///   <para>
    ///   Deletes the instance "my-instance-2". The size of the instance group will decrease to match.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Set-GceManagedInstanceGroup "my-group" -Size 5</code>
    ///   <para>Changes the target size of managed instance group "my-group" to be 5.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> $template = Get-GceInstanceTemplate "new-template"
    ///   PS C:\> Set-GceManagedInstanceGroup "my-group" -Template $template
    ///   </code>
    ///   <para>
    ///   The tempalte "new-template" becomes the template for all new instances created by managed
    ///   instance group "my-group"
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instanceGroupManagers#resource)">
    /// [Managed Instance Group resource definition]
    /// </para>
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
        public override string Project { get; set; }

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
        /// The region the managed instance group is in.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

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
            bool regionSpecified = MyInvocation.BoundParameters.ContainsKey(nameof(Region));
            bool zoneSpecified = MyInvocation.BoundParameters.ContainsKey(nameof(Zone));
            if (regionSpecified && zoneSpecified)
            {
                throw new PSInvalidOperationException(
                    $"Parameters -{nameof(Region)} and -{nameof(Zone)} cannot be used together.");
            }

            if (InstanceObject != null)
            {
                InstanceUri = InstanceObject.Select(instance => instance.SelfLink).ToArray();
            }

            switch (ParameterSetName)
            {
                case ParameterSetNames.AbandonUri:
                case ParameterSetNames.AbandonObject:
                    if (regionSpecified)
                    {
                        AbandonUriRegion();
                    }
                    else
                    {
                        AbandonUriZone();
                    }
                    break;
                case ParameterSetNames.DeleteUri:
                case ParameterSetNames.DeleteObject:
                    if (regionSpecified)
                    {
                        DeleteUriRegion();
                    }
                    else
                    {
                        DeleteUriZone();
                    }
                    break;
                case ParameterSetNames.RecreateUri:
                case ParameterSetNames.RecreateObject:
                    if (regionSpecified)
                    {
                        RecreateUriRegion();
                    }
                    else
                    {
                        RecreateUriZone();
                    }
                    break;
                case ParameterSetNames.Resize:
                    if (regionSpecified)
                    {
                        ResizeUriRegion();
                    }
                    else
                    {
                        ResizeUriZone();
                    }
                    break;
                case ParameterSetNames.SetTemplate:
                    if (regionSpecified)
                    {
                        SetTemplateRegion();
                    }
                    else
                    {
                        SetTemplateZone();
                    }
                    break;
                case ParameterSetNames.SetTargetPools:
                    if (regionSpecified)
                    {
                        SetTargetPoolsRegion();
                    }
                    else
                    {
                        SetTargetPoolsZone();
                    }
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private void SetTemplateRegion()
        {
            var regionRequestBody = new RegionInstanceGroupManagersSetTemplateRequest
            {
                InstanceTemplate = Template
            };
            if (ShouldProcess($"{Project}/{Region}/{Name}", "Set Template"))
            {
                RegionInstanceGroupManagersResource.SetInstanceTemplateRequest request =
                    Service.RegionInstanceGroupManagers.SetInstanceTemplate(regionRequestBody, Project, Region, Name);
                Operation operation = request.Execute();
                AddRegionOperation(Project, Region, operation, () =>
                {
                    WriteObject(Service.RegionInstanceGroupManagers.Get(Project, Region, Name).Execute());
                });
            }
        }

        private void SetTemplateZone()
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
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name).Execute());
                });
            }
        }

        private void SetTargetPoolsRegion()
        {
            var regionRequestBody = new RegionInstanceGroupManagersSetTargetPoolsRequest()
            {
                TargetPools = TargetPoolUri
            };
            if (ShouldProcess($"{Project}/{Region}/{Name}", "Set Target Pools"))
            {
                RegionInstanceGroupManagersResource.SetTargetPoolsRequest request =
                    Service.RegionInstanceGroupManagers.SetTargetPools(regionRequestBody, Project, Region, Name);
                Operation operation = request.Execute();
                AddRegionOperation(Project, Region, operation, () =>
                {
                    WriteObject(Service.RegionInstanceGroupManagers.Get(Project, Region, Name).Execute());
                });
            }
        }

        private void SetTargetPoolsZone()
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
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name).Execute());
                });
            }
        }

        private void ResizeUriRegion()
        {
            if (ShouldProcess($"{Project}/{Region}/{Name}", "Resize"))
            {
                RegionInstanceGroupManagersResource.ResizeRequest request =
                    Service.RegionInstanceGroupManagers.Resize(Project, Region, Name, Size);
                Operation operation = request.Execute();
                AddRegionOperation(Project, Region, operation, () =>
                {
                    WriteObject(Service.RegionInstanceGroupManagers.Get(Project, Region, Name).Execute());
                });
            }
        }

        private void ResizeUriZone()
        {
            if (ShouldProcess($"{Project}/{Zone}/{Name}", "Resize"))
            {
                InstanceGroupManagersResource.ResizeRequest request =
                    Service.InstanceGroupManagers.Resize(Project, Zone, Name, Size);
                Operation operation = request.Execute();
                AddZoneOperation(Project, Zone, operation, () =>
                {
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name).Execute());
                });
            }
        }

        private void RecreateUriRegion()
        {
            var regionBody = new RegionInstanceGroupManagersRecreateRequest
            {
                Instances = InstanceUri
            };
            if (ShouldProcess("Recreating instances: " + JoinInstanceNames(regionBody.Instances), null,
                $"Managed Instance Group {Project}/{Region}/{Name}"))
            {
                RegionInstanceGroupManagersResource.RecreateInstancesRequest request =
                    Service.RegionInstanceGroupManagers.RecreateInstances(regionBody, Project, Region, Name);
                Operation operation = request.Execute();
                AddRegionOperation(Project, Region, operation, () =>
                {
                    WriteObject(Service.RegionInstanceGroupManagers.Get(Project, Region, Name).Execute());
                });
            }
        }

        private void RecreateUriZone()
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
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name).Execute());
                });
            }
        }

        private void DeleteUriRegion()
        {
            var regionBody = new RegionInstanceGroupManagersDeleteInstancesRequest
            {
                Instances = InstanceUri
            };
            if (ShouldProcess("Deleting instances: " + JoinInstanceNames(regionBody.Instances), null,
                $"Managed Instance Group {Project}/{Region}/{Name}"))
            {
                RegionInstanceGroupManagersResource.DeleteInstancesRequest request =
                    Service.RegionInstanceGroupManagers.DeleteInstances(regionBody, Project, Region, Name);
                Operation operation = request.Execute();
                AddRegionOperation(Project, Region, operation, () =>
                {
                    WriteObject(Service.RegionInstanceGroupManagers.Get(Project, Region, Name).Execute());
                });
            }
        }

        private void DeleteUriZone()
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
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name).Execute());
                });
            }
        }

        private void AbandonUriRegion()
        {
            var regionBody = new RegionInstanceGroupManagersAbandonInstancesRequest
            {
                Instances = InstanceUri
            };
            if (ShouldProcess("Abandoning instances: " + JoinInstanceNames(regionBody.Instances), null,
                $"Managed Instance Group {Project}/{Region}/{Name}"))
            {
                RegionInstanceGroupManagersResource.AbandonInstancesRequest request =
                    Service.RegionInstanceGroupManagers.AbandonInstances(regionBody, Project, Region, Name);
                Operation operation = request.Execute();
                AddRegionOperation(Project, Region, operation, () =>
                {
                    WriteObject(Service.RegionInstanceGroupManagers.Get(Project, Region, Name).Execute());
                });
            }
        }

        private void AbandonUriZone()
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
                    WriteObject(Service.InstanceGroupManagers.Get(Project, Zone, Name).Execute());
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
    ///   <code>PS C:\> Wait-GceManagedInstanceGroup "my-group" -Timeout 30</code>
    ///   <para>
    ///   Waits for the managed instance group "my-group" to reach a noraml running state for up to 30
    ///   seconds.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceManagedInstanceGroup -Zone "us-central1-a" | Wait-GceManagedInstanceGroup</code>
    ///   <para>
    ///   Waits for all maanged instance groups in zone us-central1-a to reach a normal running
    ///   state.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instanceGroupManagers#resource)">
    /// [Managed Instance Group resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Wait, "GceManagedInstanceGroup",
        DefaultParameterSetName = ParameterSetNames.ByNameZone)]
    public class WaitGceManagedInstanceGroupCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByNameZone = "ByNameZone";
            public const string ByObject = "ByObject";
            public const string ByNameRegion = "ByNameRegion";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the managed instance group.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameZone)]
        [Parameter(ParameterSetName = ParameterSetNames.ByNameRegion)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone the managed instance group is in.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameZone)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The region the managed instance group is in.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameRegion)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the managed instance group to wait on.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameZone, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByNameRegion, Mandatory = true,
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
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByNameZone:
                    WaitZoneManagedInstance(Project, Zone, Name);
                    break;
                case ParameterSetNames.ByNameRegion:
                    WaitRegionManagedInstance(Project, Region, Name);
                    break;
                case ParameterSetNames.ByObject:
                    string project = GetProjectNameFromUri(Object.SelfLink);
                    string name = Object.Name;
                    if (!string.IsNullOrWhiteSpace(Object.Region))
                    {
                        string region = GetRegionNameFromUri(Object.Region);
                        WaitRegionManagedInstance(project, region, name);
                    }
                    else
                    {
                        string zone = GetZoneNameFromUri(Object.Zone);
                        WaitZoneManagedInstance(project, zone, name);
                    }
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private void WaitZoneManagedInstance(string project, string zone, string name)
        {
            InstanceGroupManagersResource.ListManagedInstancesRequest request =
                Service.InstanceGroupManagers.ListManagedInstances(project, zone, name);
            string warningMessage = "Wait-GceManagedInstanceGroup timed out for "
                + $"{request.Project}/{request.Zone}/{request.InstanceGroupManager}";
            WaitForManagedInstancesHelper(
                () => request.Execute().ManagedInstances,
                warningMessage);
        }

        private void WaitRegionManagedInstance(string project, string region, string name)
        {
            RegionInstanceGroupManagersResource.ListManagedInstancesRequest request =
                Service.RegionInstanceGroupManagers.ListManagedInstances(project, region, name);
            string warningMessage = "Wait-GceManagedInstanceGroup timed out for "
                + $"{request.Project}/{request.Region}/{request.InstanceGroupManager}";
            WaitForManagedInstancesHelper(
                () => request.Execute().ManagedInstances,
                warningMessage);
        }

        private void WaitForManagedInstancesHelper(
            Func<IList<ManagedInstance>> getInstanceDelegate,
            string warningMessage)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            IList<ManagedInstance> instances = getInstanceDelegate();
            while (instances != null && instances.Any(i => i.CurrentAction != "NONE") && !Stopping)
            {
                if (Timeout >= 0 && stopwatch.Elapsed.Seconds > Timeout)
                {
                    WriteWarning(warningMessage);
                    break;
                }
                Thread.Sleep(150);
                instances = getInstanceDelegate();
            }
        }
    }
}
