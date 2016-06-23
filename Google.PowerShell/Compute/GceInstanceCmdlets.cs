// Copyright 2016 Google Inc. All Rights Reserved.
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
    [Cmdlet(VerbsCommon.Get, "GceInstance", DefaultParameterSetName = "OfProject")]
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
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0,
            ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Instance object to get a new copy of.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true, Position = 0,
            ValueFromPipeline = true)]
        public Instance Object { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance group manager to get the instances of.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfInstanceGroupManager, Mandatory = true)]
        public string InstanceGroupManagerName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The InstanceGroupManager object to get the instances of.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfInstanceGroupManagerObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public InstanceGroupManager InstanceGroupManager { get; set; }

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
                    throw new PSInvalidOperationException($"{ParameterSetName} is not a valid ParameterSet");
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
            string groupProject = GetProjectNameFromUri(InstanceGroupManager.SelfLink);
            string groupZone = InstanceGroupManager.Zone;
            string groupName = InstanceGroupManager.Name;
            InstanceGroupManagersResource.ListManagedInstancesRequest request =
                Service.InstanceGroupManagers.ListManagedInstances(groupProject, groupZone, groupName);
            InstanceGroupManagersListManagedInstancesResponse response = request.Execute();
            return GetActiveInstances(response);
        }

        private IEnumerable<Instance> GetManagedGroupInstances()
        {
            InstanceGroupManagersResource.ListManagedInstancesRequest request =
                Service.InstanceGroupManagers.ListManagedInstances(Project, Zone, InstanceGroupManagerName);
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
    public class AddGceInstanceCmdlet : GceConcurrentCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project that will own the instances.
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
        /// The description of the instance to create, as generated by New-GceInstanceConfig.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ValueFromPipeline = true)]
        public Instance Instance { get; set; }

        protected override void ProcessRecord()
        {
            if (Instance.MachineType.Split('/', '\\').Length < 2)
            {
                if (Instance.MachineType.Contains("custom"))
                {
                    Instance.MachineType = $"zones/{Zone}/machineTypes/{Instance.MachineType}";
                }
                else
                {
                    Instance.MachineType =
                        $"projects/{Project}/zones/{Zone}/machineTypes/{Instance.MachineType}";
                }
            }

            InstancesResource.InsertRequest request = Service.Instances.Insert(Instance, Project, Zone);
            Operation operation = request.Execute();
            AddOperation(Project, Zone, operation);
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
        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to delete.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ValueFromPipeline = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Instance))]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            if (ShouldProcess($"{Project}/{Zone}/{Name}", "Remove VM instance"))
            {
                var request = Service.Instances.Delete(Project, Zone, Name);
                var operation = request.Execute();
                AddOperation(Project, Zone, operation);
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
        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to start.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ValueFromPipeline = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Instance))]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            InstancesResource.StartRequest request = Service.Instances.Start(Project, Zone, Name);
            Operation operation = request.Execute();
            AddOperation(Project, Zone, operation);
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
        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to stop.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ValueFromPipeline = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Instance))]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            InstancesResource.StopRequest request = Service.Instances.Stop(Project, Zone, Name);
            Operation operation = request.Execute();
            AddOperation(Project, Zone, operation);
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
        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to reset.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ValueFromPipeline = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Instance))]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            InstancesResource.ResetRequest request = Service.Instances.Reset(Project, Zone, Name);
            Operation operation = request.Execute();
            AddOperation(Project, Zone, operation);
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
    public class UpdateGceInstanceCmdlet : GceConcurrentCmdlet
    {
        internal class ParameterSetNames
        {
            public const string AccessConfig = "AccessConfig";
            public const string Disk = "Disk";
            public const string Metadata = "Metadata";
            public const string Tag = "Tag";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the instance to update.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to update.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Instance))]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the network interface to add or remove access configs.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.AccessConfig)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(NetworkInterface))]
        public string NetworkInterface { get; set; }

        /// <summary>
        /// <para type="description">
        /// The new access config to add to a network interface.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AccessConfig)]
        public List<AccessConfig> NewAccessConfig { get; set; } = new List<AccessConfig>();

        /// <summary>
        /// <para type="description">
        /// The name of the access config to remove from the network interface.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AccessConfig)]
        public List<string> DeleteAccessConfig { get; set; } = new List<string>();

        /// <summary>
        /// <para type="description">
        /// Name of the disk to attach.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Disk)]
        public List<object> AddDisk { get; set; } = new List<object>();

        /// <summary>
        /// <para type="description">
        /// The name of the disk to detach.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Disk)]
        public List<string> DetachDisk { get; set; } = new List<string>();

        /// <summary>
        /// <para type="description">
        /// The keys and values of the metadata to add.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Metadata)]
        public Hashtable AddMetadata { get; set; } = new Hashtable();

        /// <summary>
        /// <para type="description">
        /// The keys of the metadata to remove.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Metadata)]
        public List<string> RemoveMetadata { get; set; } = new List<string>();

        /// <summary>
        /// <para type="description">
        /// The tag to add.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Tag)]
        public List<string> AddTag { get; set; } = new List<string>();

        /// <summary>
        /// <para type="description">
        /// The tag to remove.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Tag)]
        public List<string> RemoveTag { get; set; } = new List<string>();

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.AccessConfig:
                    ProcessAccessConfig();
                    break;
                case ParameterSetNames.Disk:
                    ProcessDisk();
                    break;
                case ParameterSetNames.Metadata:
                    ProcessMetadata();
                    break;
                case ParameterSetNames.Tag:
                    ProcessTag();
                    break;
                default:
                    throw new PSInvalidOperationException($"{ParameterSetName} is not a valid ParameterSet");
            }
        }

        /// <summary>
        /// ProcessRecord for AccessConfig parameter set.
        /// </summary>
        private void ProcessAccessConfig()
        {
            foreach (string configName in DeleteAccessConfig)
            {
                InstancesResource.DeleteAccessConfigRequest request = Service.Instances.DeleteAccessConfig(
                    Project, Zone, Instance, configName, NetworkInterface);
                Operation operation = request.Execute();
                AddOperation(Project, Zone, operation);
            }

            foreach (AccessConfig accessConfig in NewAccessConfig)
            {
                InstancesResource.AddAccessConfigRequest request = Service.Instances.AddAccessConfig(
                    accessConfig, Project, Zone, Instance, NetworkInterface);
                Operation response = request.Execute();
                AddOperation(Project, Zone, response);
            }
        }

        /// <summary>
        /// ProcessRecord for Disk parameter set.
        /// </summary>
        private void ProcessDisk()
        {
            foreach (string diskName in DetachDisk)
            {
                InstancesResource.DetachDiskRequest request = Service.Instances.DetachDisk(
                    Project, Zone, Instance, diskName);
                Operation operation = request.Execute();
                AddOperation(Project, Zone, operation);
            }

            foreach (object diskParam in AddDisk)
            {
                // Allow for taking Disk, AttachedDisk, and string objects.
                AttachedDisk newDisk;
                if (diskParam is AttachedDisk)
                {
                    newDisk = diskParam as AttachedDisk;
                }
                else
                {
                    Disk disk = diskParam as Disk;
                    if (disk == null)
                    {
                        disk = Service.Disks.Get(Project, Zone, diskParam.ToString()).Execute();
                    }
                    newDisk = new AttachedDisk { Source = disk.SelfLink, DeviceName = disk.Name };
                }
                InstancesResource.AttachDiskRequest request =
                    Service.Instances.AttachDisk(newDisk, Project, Zone, Instance);
                Operation operation = request.Execute();
                AddOperation(Project, Zone, operation);
            }
        }

        /// <summary>
        /// ProcessRecord for Metadata parameter set.
        /// </summary>
        private void ProcessMetadata()
        {
            InstancesResource.GetRequest getRequest = Service.Instances.Get(Project, Zone, Instance);
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
                Service.Instances.SetMetadata(metadata, Project, Zone, Instance);
            AddOperation(Project, Zone, request.Execute());
        }

        /// <summary>
        /// ProcessRecord for Tag parameter set.
        /// </summary>
        private void ProcessTag()
        {
            InstancesResource.GetRequest getRequest = Service.Instances.Get(Project, Zone, Instance);
            Instance instance = getRequest.Execute();
            Tags tags = instance.Tags ?? new Tags();
            tags.Items = tags.Items ?? new List<string>();
            tags.Items = tags.Items.Where(tag => !RemoveTag.Contains(tag)).Concat(AddTag).ToList();
            InstancesResource.SetTagsRequest setRequest =
                Service.Instances.SetTags(tags, Project, Zone, Instance);
            Operation operation = setRequest.Execute();
            AddOperation(Project, Zone, operation);
        }
    }
}
