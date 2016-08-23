// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets information about Google Compute Engine VM Instances.
    /// </para>
    /// <para type="description">
    /// Gets information about VM instances.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceInstance", DefaultParameterSetName = ParameterSetNames.OfProject)]
    public class GetGceInstanceCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string OfZone = "OfZone";
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
            public const string OfInstanceGroupManager = "OfInstanceGroupManager";
            public const string OfInstanceGroupManagerObject = "OfInstanceGroupManagerObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfProject)]
        [Parameter(ParameterSetName = ParameterSetNames.OfZone)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [Parameter(ParameterSetName = ParameterSetNames.OfInstanceGroupManager)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfZone, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [Parameter(ParameterSetName = ParameterSetNames.OfInstanceGroupManager)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Instance object to get a new copy of.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Instance Object { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance group manager to get the instances of.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfInstanceGroupManager, Mandatory = true)]
        public string ManagedGroupName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The InstanceGroupManager object to get the instances of.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfInstanceGroupManagerObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public InstanceGroupManager ManagedGroupObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// When this switch is set, the cmdlet will output the string contents of the serial port of the
        /// instance rather than the normal data about the instance.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter SerialPortOutput { get; set; }

        protected override void ProcessRecord()
        {
            // Parameter sets can change when using the pipeline. Make sure all the needed parameters are
            // initialized.
            UpdateConfigPropertyNameAttribute();
            IEnumerable<Instance> instances;
            switch (ParameterSetName)
            {
                case ParameterSetNames.OfProject:
                    instances = GetAllProjectInstances();
                    break;
                case ParameterSetNames.OfZone:
                    instances = GetZoneInstances();
                    break;
                case ParameterSetNames.ByName:
                    instances = new[] { GetExactInstance() };
                    break;
                case ParameterSetNames.ByObject:
                    instances = new[] { GetByObject() };
                    break;
                case ParameterSetNames.OfInstanceGroupManager:
                    instances = GetManagedGroupInstances();
                    break;
                case ParameterSetNames.OfInstanceGroupManagerObject:
                    instances = GetManagedGroupInstancesByObject();
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            if (SerialPortOutput)
            {
                WriteSerialPortOutput(instances);
            }
            else
            {
                WriteObject(instances, true);
            }
        }

        private IEnumerable<Instance> GetManagedGroupInstancesByObject()
        {
            string groupProject = GetProjectNameFromUri(ManagedGroupObject.SelfLink);
            string groupZone = GetZoneNameFromUri(ManagedGroupObject.Zone);
            string groupName = ManagedGroupObject.Name;
            InstanceGroupManagersResource.ListManagedInstancesRequest request =
                Service.InstanceGroupManagers.ListManagedInstances(groupProject, groupZone, groupName);
            InstanceGroupManagersListManagedInstancesResponse response = request.Execute();
            return GetActiveInstances(response);
        }

        private IEnumerable<Instance> GetManagedGroupInstances()
        {
            InstanceGroupManagersResource.ListManagedInstancesRequest request =
                Service.InstanceGroupManagers.ListManagedInstances(Project, Zone, ManagedGroupName);
            InstanceGroupManagersListManagedInstancesResponse response = request.Execute();
            return GetActiveInstances(response);
        }

        private IEnumerable<Instance> GetActiveInstances(InstanceGroupManagersListManagedInstancesResponse response)
        {
            if (response.ManagedInstances != null)
            {
                foreach (ManagedInstance managedInstance in response.ManagedInstances)
                {
                    if (managedInstance.InstanceStatus != null)
                    {
                        string project = GetProjectNameFromUri(managedInstance.Instance);
                        string zone = GetZoneNameFromUri(managedInstance.Instance);
                        string name = GetUriPart("instances", managedInstance.Instance);
                        yield return Service.Instances.Get(project, zone, name).Execute();
                    }
                }
            }
        }

        private Instance GetByObject()
        {
            string project = GetProjectNameFromUri(Object.SelfLink);
            string zone = GetZoneNameFromUri(Object.SelfLink);
            InstancesResource.GetRequest getRequest = Service.Instances.Get(project, zone, Object.Name);
            return getRequest.Execute();
        }

        private IEnumerable<Instance> GetAllProjectInstances()
        {
            InstancesResource.AggregatedListRequest aggListRequest =
                Service.Instances.AggregatedList(Project);
            do
            {
                var aggList = aggListRequest.Execute();
                var instances = aggList.Items.Values
                    .Where(l => l.Instances != null)
                    .SelectMany(l => l.Instances);
                foreach (Instance instance in instances)
                {
                    yield return instance;
                }
                aggListRequest.PageToken = aggList.NextPageToken;
            }
            while (aggListRequest.PageToken != null);
        }

        private IEnumerable<Instance> GetZoneInstances()
        {
            string pageToken = null;
            do
            {
                InstancesResource.ListRequest listRequest = Service.Instances.List(Project, Zone);
                listRequest.PageToken = pageToken;
                InstanceList response = listRequest.Execute();

                if (response.Items != null)
                {
                    foreach (Instance instance in response.Items)
                    {
                        if (instance != null)
                        {
                            yield return instance;
                        }
                    }
                }
                pageToken = response.NextPageToken;
            }
            while (pageToken != null);
        }

        private Instance GetExactInstance()
        {
            InstancesResource.GetRequest getRequest = Service.Instances.Get(Project, Zone, Name);
            return getRequest.Execute();
        }

        private void WriteSerialPortOutput(IEnumerable<Instance> instances)
        {
            var tasks = instances.Select(i => GetSerialPortOutputAsync(i));

            var exceptions = new List<Exception>();
            foreach (Task<string> task in tasks)
            {
                try
                {
                    WriteObject(task.Result);
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            if (exceptions.Count == 1)
            {
                throw exceptions.First();
            }
            else if (exceptions.Count > 1)
            {
                throw new AggregateException(exceptions);
            }
        }

        private async Task<string> GetSerialPortOutputAsync(Instance instance)
        {
            string zone = GetZoneNameFromUri(instance.Zone);
            InstancesResource.GetSerialPortOutputRequest request =
                Service.Instances.GetSerialPortOutput(Project, zone, instance.Name);
            SerialPortOutput output = await request.ExecuteAsync();
            return output.Contents;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates and starts a Google Compute Engine VM instance.
    /// </para>
    /// <para type="description">
    /// Creates and starts a Google Compute Engine VM instance. Use New-GceInstanceConfig to create an instance
    /// description.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceInstance")]
    public class AddGceInstanceCmdlet : GceInstanceDescriptionCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByValues = "ByValues";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that will own the instance.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance will reside.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The definition of the instance to create.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Instance InstanceConfig { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to add.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.ByValues)]
        public override string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The machine type of this instance. Can be a name, a URL or a MachineType object from
        /// Get-GceMachineType. Defaults to "n1-standard-1".
        /// </para>
        /// </summary>
        [Parameter(Position = 1, ParameterSetName = ParameterSetNames.ByValues)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(MachineType),
            Property = nameof(Apis.Compute.v1.Data.MachineType.SelfLink))]
        public override string MachineType { get; set; } = "n1-standard-1";

        /// <summary>
        /// <para type="description">
        /// Enables instances to send and receive packets for IP addresses other than their own. Switch on if
        /// this instance will be used as an IP gateway or it will be set as the next-hop in a Route
        /// resource.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override SwitchParameter CanIpForward { get; set; }

        /// <summary>
        /// <para type="description">
        /// Human readable description of this instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The persistant disk to use as a boot disk. Use Get-GceDisk to get one of these.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override Disk BootDisk { get; set; }

        /// <summary>
        /// <para type="description">
        /// The the image used to create the boot disk. Use Get-GceImage to get one of these.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Alias("DiskImage")]
        public override Image BootDiskImage { get; set; }


        /// <summary>
        /// <para type="description">
        /// An existing disk to attach. It will attach in read-only mode.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override Disk[] ExtraDisk { get; set; }

        /// <summary>
        /// <para type="description">
        /// An AttachedDisk object specifying a disk to attach. Do not specify `-BootDiskImage` or
        /// `-BootDiskSnapshot` if this is a boot disk. You can build one using New-GceAttachedDiskConfig.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override AttachedDisk[] Disk { get; set; }

        /// <summary>
        /// <para type="description">
        /// The keys and values of the Metadata of this instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override IDictionary Metadata { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the network to use. If not specified, is default. This can be a Network object you get
        /// from Get-GceNetwork.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [PropertyByTypeTransformation(Property = nameof(Apis.Compute.v1.Data.Network.SelfLink),
            TypeToTransform = typeof(Network))]
        public override string Network { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the instance will not have an external ip address.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override SwitchParameter NoExternalIp { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the instance will be preemptible. If set, AutomaticRestart will be false.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override SwitchParameter Preemptible { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the instance will not restart when shut down by Google Compute Engine.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override bool AutomaticRestart { get; set; } = true;

        /// <summary>
        /// <para type="description">
        /// If set, the instance will terminate rather than migrate when the host undergoes maintenance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override SwitchParameter TerminateOnMaintenance { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ServiceAccount used to specify access tokens. Use New-GceServiceAccountConfig to build one.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override ServiceAccount[] ServiceAccount { get; set; }

        /// <summary>
        /// <para type="description">
        /// A tag of this instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override string[] Tag { get; set; }

        /// <summary>
        /// <para type="description">
        /// The static ip address this instance will have. Can be a string, or and Address object from
        /// Get-GceAddress.
        /// </para>
        /// </summary>
        [PropertyByTypeTransformation(Property = nameof(Apis.Compute.v1.Data.Address.AddressValue),
            TypeToTransform = typeof(Address))]
        protected override string Address { get; set; }

        protected override void ProcessRecord()
        {
            Instance instance;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByValues:
                    instance = BuildInstance();
                    break;
                case ParameterSetNames.ByObject:
                    instance = InstanceConfig;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            instance.MachineType = GetMachineTypeUrl(instance.MachineType);
            InstancesResource.InsertRequest request = Service.Instances.Insert(instance, Project, Zone);
            Operation operation = request.Execute();
            AddZoneOperation(Project, Zone, operation, () =>
            {
                WriteObject(Service.Instances.Get(Project, Zone, instance.Name).Execute());
            });
        }

        private string GetMachineTypeUrl(string machineType)
        {
            if (machineType.Split('/', '\\').Length < 2)
            {
                if (machineType.Contains("custom"))
                {
                    machineType = $"zones/{Zone}/machineTypes/{machineType}";
                }
                else
                {
                    machineType =
                        $"projects/{Project}/zones/{Zone}/machineTypes/{machineType}";
                }
            }
            return machineType;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes a Google Compute Engine VM instance.
    /// </para>
    /// <para type="description">
    /// Deletes a Google Compute Engine VM instance.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceInstance", SupportsShouldProcess = true)]
    public class RemoveGceInstanceCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }
        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to delete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The instance object to delete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Instance Object { get; set; }

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
                    throw UnknownParameterSetException;
            }

            if (ShouldProcess($"{project}/{zone}/{name}", "Remove VM instance"))
            {
                InstancesResource.DeleteRequest request = Service.Instances.Delete(project, zone, name);
                Operation operation = request.Execute();
                AddZoneOperation(project, zone, operation);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Starts a Google Compute Engine VM instance.
    /// </para>
    /// <para type="description">
    /// Starts a Google Compute Engine VM instance.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Start, "GceInstance")]
    public class StartGceInstanceCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to start.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The instance object to start.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Instance Object { get; set; }

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
                    throw UnknownParameterSetException;
            }
            InstancesResource.StartRequest request = Service.Instances.Start(project, zone, name);
            Operation operation = request.Execute();
            AddZoneOperation(project, zone, operation, () =>
            {
                WriteObject(Service.Instances.Get(project, zone, name).Execute());
            });
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Stops a Google Compute Engine VM instance.
    /// </para>
    /// <para type="description">
    /// Stops a Google Compute Engine VM instance.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Stop, "GceInstance")]
    public class StopGceInstanceCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to stop.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The instance object to stop.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Instance Object { get; set; }

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
                    throw UnknownParameterSetException;
            }
            InstancesResource.StopRequest request = Service.Instances.Stop(project, zone, name);
            Operation operation = request.Execute();
            AddZoneOperation(project, zone, operation, () =>
            {
                WriteObject(Service.Instances.Get(project, zone, name).Execute());
            });
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Resets a Google Compute Engine VM instance.
    /// </para>
    /// <para type="description">
    /// Resets a Google Compute Engine VM instance.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Restart, "GceInstance")]
    public class RestartGceInstanceCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to reset.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The instance object to restart.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Instance Object { get; set; }

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
                    throw UnknownParameterSetException;
            }
            InstancesResource.ResetRequest request = Service.Instances.Reset(project, zone, name);
            Operation operation = request.Execute();
            AddZoneOperation(project, zone, operation, () =>
            {
                WriteObject(Service.Instances.Get(project, zone, name).Execute());
            });
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Sets various attributes of a VM instance.
    /// </para>
    /// <para type="description">
    /// With this cmdlet, you can update metadata, attach and detach disks, add and remove acces configs,
    /// or add and remove tags.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GceInstance")]
    public class SetGceInstanceCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string AccessConfig = "AccessConfig";
            public const string Disk = "Disk";
            public const string Metadata = "Metadata";
            public const string Tag = "Tag";
            public const string AccessConfigByObject = "AccessConfigByObject";
            public const string DiskByObject = "DiskByObject";
            public const string MetadataByObject = "MetadataByObject";
            public const string TagByObject = "TagByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the instance to update.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AccessConfig)]
        [Parameter(ParameterSetName = ParameterSetNames.Disk)]
        [Parameter(ParameterSetName = ParameterSetNames.Metadata)]
        [Parameter(ParameterSetName = ParameterSetNames.Tag)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        private string _project;

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AccessConfig)]
        [Parameter(ParameterSetName = ParameterSetNames.Disk)]
        [Parameter(ParameterSetName = ParameterSetNames.Metadata)]
        [Parameter(ParameterSetName = ParameterSetNames.Tag)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        private string _zone;

        /// <summary>
        /// <para type="description">
        /// The name of the instance to update.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AccessConfig, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.Disk, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.Metadata, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.Tag, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        private string _name;

        /// <summary>
        /// <para type="description">
        /// The instance object to update.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AccessConfigByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.DiskByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.MetadataByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.TagByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Instance Object { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the network interface to add or remove access configs.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.AccessConfig)]
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.AccessConfigByObject)]
        [PropertyByTypeTransformation(Property = nameof(Apis.Compute.v1.Data.NetworkInterface.Name),
            TypeToTransform = typeof(NetworkInterface))]
        public string NetworkInterface { get; set; }

        /// <summary>
        /// <para type="description">
        /// The new access config to add to a network interface.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AccessConfig)]
        [Parameter(ParameterSetName = ParameterSetNames.AccessConfigByObject)]
        public AccessConfig[] AddAccessConfig { get; set; } = { };

        /// <summary>
        /// <para type="description">
        /// The name of the access config to remove from the network interface.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AccessConfig)]
        [Parameter(ParameterSetName = ParameterSetNames.AccessConfigByObject)]
        public string[] RemoveAccessConfig { get; set; } = { };

        /// <summary>
        /// <para type="description">
        /// The disk to attach. Can the name of a disk, a disk object from Get-GceDisk, or an attached disk
        /// object from New-GceAttachedDiskConfig.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Disk)]
        [Parameter(ParameterSetName = ParameterSetNames.DiskByObject)]
        public object[] AddDisk { get; set; } = { };

        /// <summary>
        /// <para type="description">
        /// The name of the disk to detach.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Disk)]
        [Parameter(ParameterSetName = ParameterSetNames.DiskByObject)]
        public string[] RemoveDisk { get; set; } = { };

        /// <summary>
        /// <para type="description">
        /// The keys and values of the metadata to add.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Metadata)]
        [Parameter(ParameterSetName = ParameterSetNames.MetadataByObject)]
        public Hashtable AddMetadata { get; set; } = new Hashtable();

        /// <summary>
        /// <para type="description">
        /// The keys of the metadata to remove.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Metadata)]
        [Parameter(ParameterSetName = ParameterSetNames.MetadataByObject)]
        public string[] RemoveMetadata { get; set; } = { };

        /// <summary>
        /// <para type="description">
        /// The tag to add.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Tag)]
        [Parameter(ParameterSetName = ParameterSetNames.TagByObject)]
        public string[] AddTag { get; set; } = { };

        /// <summary>
        /// <para type="description">
        /// The tag to remove.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Tag)]
        [Parameter(ParameterSetName = ParameterSetNames.TagByObject)]
        public string[] RemoveTag { get; set; } = { };

        protected override void ProcessRecord()
        {
            // Parameter set can change between pipeline inputs.
            UpdateConfigPropertyNameAttribute();
            switch (ParameterSetName)
            {
                case ParameterSetNames.AccessConfig:
                case ParameterSetNames.Disk:
                case ParameterSetNames.Metadata:
                case ParameterSetNames.Tag:
                    _project = Project;
                    _zone = Zone;
                    _name = Name;
                    break;
                case ParameterSetNames.AccessConfigByObject:
                case ParameterSetNames.DiskByObject:
                case ParameterSetNames.MetadataByObject:
                case ParameterSetNames.TagByObject:
                    _project = GetProjectNameFromUri(Object.SelfLink);
                    _zone = GetZoneNameFromUri(Object.Zone);
                    _name = Object.Name;
                    break;
            }

            switch (ParameterSetName)
            {
                case ParameterSetNames.AccessConfig:
                case ParameterSetNames.AccessConfigByObject:
                    ProcessAccessConfig();
                    break;
                case ParameterSetNames.Disk:
                case ParameterSetNames.DiskByObject:
                    ProcessDisk();
                    break;
                case ParameterSetNames.Metadata:
                case ParameterSetNames.MetadataByObject:
                    ProcessMetadata();
                    break;
                case ParameterSetNames.Tag:
                case ParameterSetNames.TagByObject:
                    ProcessTag();
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        /// <summary>
        /// ProcessRecord for AccessConfig parameter set.
        /// </summary>
        private void ProcessAccessConfig()
        {
            foreach (string configName in RemoveAccessConfig)
            {
                InstancesResource.DeleteAccessConfigRequest request = Service.Instances.DeleteAccessConfig(
                    _project, _zone, _name, configName, NetworkInterface);
                Operation operation = request.Execute();
                AddZoneOperation(_project, _zone, operation);
            }

            foreach (AccessConfig accessConfig in AddAccessConfig)
            {
                InstancesResource.AddAccessConfigRequest request = Service.Instances.AddAccessConfig(
                    accessConfig, _project, _zone, _name, NetworkInterface);
                Operation response = request.Execute();
                AddZoneOperation(_project, _zone, response);
            }
        }

        /// <summary>
        /// ProcessRecord for Disk parameter set.
        /// </summary>
        private void ProcessDisk()
        {
            foreach (string diskName in RemoveDisk)
            {
                InstancesResource.DetachDiskRequest request = Service.Instances.DetachDisk(
                    _project, _zone, _name, diskName);
                Operation operation = request.Execute();
                AddZoneOperation(_project, _zone, operation);
            }

            foreach (object diskParam in AddDisk)
            {
                // Allow for taking Disk, AttachedDisk, and string objects.
                AttachedDisk newDisk;
                if (diskParam is AttachedDisk || (diskParam as PSObject)?.BaseObject is AttachedDisk)
                {
                    newDisk = diskParam as AttachedDisk ?? (diskParam as PSObject)?.BaseObject as AttachedDisk;
                }
                else
                {
                    Disk disk = diskParam as Disk ?? (diskParam as PSObject)?.BaseObject as Disk;
                    if (disk == null)
                    {
                        disk = Service.Disks.Get(_project, _zone, diskParam.ToString()).Execute();
                    }
                    newDisk = new AttachedDisk { Source = disk.SelfLink, DeviceName = disk.Name };
                }
                InstancesResource.AttachDiskRequest request =
                    Service.Instances.AttachDisk(newDisk, _project, _zone, _name);
                Operation operation = request.Execute();
                AddZoneOperation(_project, _zone, operation);
            }
        }

        /// <summary>
        /// ProcessRecord for Metadata parameter set.
        /// </summary>
        private void ProcessMetadata()
        {
            InstancesResource.GetRequest getRequest = Service.Instances.Get(_project, _zone, _name);
            Instance instance = getRequest.Execute();
            Metadata metadata = instance.Metadata ?? new Metadata();
            metadata.Items = metadata.Items ?? new List<Metadata.ItemsData>();
            metadata.Items = metadata.Items.Where(id => !RemoveMetadata.Contains(id.Key)).ToList();
            foreach (DictionaryEntry entry in AddMetadata)
            {
                metadata.Items.Add(new Metadata.ItemsData
                {
                    Key = entry.Key.ToString(),
                    Value = entry.Value.ToString()
                });
            }
            InstancesResource.SetMetadataRequest request =
                Service.Instances.SetMetadata(metadata, _project, _zone, _name);
            AddZoneOperation(_project, _zone, request.Execute());
        }

        /// <summary>
        /// ProcessRecord for Tag parameter set.
        /// </summary>
        private void ProcessTag()
        {
            InstancesResource.GetRequest getRequest = Service.Instances.Get(_project, _zone, _name);
            Instance instance = getRequest.Execute();
            Tags tags = instance.Tags ?? new Tags();
            tags.Items = tags.Items ?? new List<string>();
            tags.Items = tags.Items.Where(tag => !RemoveTag.Contains(tag)).Concat(AddTag).ToList();
            InstancesResource.SetTagsRequest setRequest =
                Service.Instances.SetTags(tags, _project, _zone, _name);
            Operation operation = setRequest.Execute();
            AddZoneOperation(_project, _zone, operation);
        }
    }
}
