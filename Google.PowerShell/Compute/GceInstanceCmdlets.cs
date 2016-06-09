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
using System.Text;
using System.Threading.Tasks;
using static Google.Apis.Compute.v1.InstancesResource;

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
    [Cmdlet(VerbsCommon.Get, "GceInstance")]
    public class GetGceInstanceCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = false)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = false,
            ValueFromPipeline = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Instance))]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// A filter to send along with the request. This has the name of the property to filter on, either eq
        /// or ne, and a constant to test against.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string Filter { get; set; }


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
            IEnumerable<Instance> output;
            if (String.IsNullOrEmpty(Zone))
            {
                output = GetAllProjectInstances();
            }
            else if (String.IsNullOrEmpty(Name))
            {
                output = GetZoneInstances();
            }
            else
            {
                output = new Instance[] { GetExactInstance() };
            }

            var tasks = new List<Task<string>>();

            foreach (Instance instance in output)
            {
                if (SerialPortOutput)
                {
                    tasks.Add(GetSerialPortOutputAsync(instance));
                }
                else
                {
                    WriteObject(instance);
                }
            }

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

        private IEnumerable<Instance> GetAllProjectInstances()
        {
            string pageToken = null;
            do
            {
                var aggListRequest = Service.Instances.AggregatedList(Project);
                aggListRequest.Filter = Filter;
                aggListRequest.PageToken = pageToken;
                var aggList = aggListRequest.Execute();
                string nextPageToken = aggList.NextPageToken;
                var instances = aggList.Items.Values
                    .Where(l => l.Instances != null)
                    .SelectMany(l => l.Instances);
                foreach (Instance instance in instances)
                {
                    yield return instance;
                }
            }
            while (pageToken != null);
        }

        private IEnumerable<Instance> GetZoneInstances()
        {
            string pageToken = null;
            do
            {
                InstancesResource.ListRequest listRequest = Service.Instances.List(Project, Zone);
                listRequest.Filter = Filter;
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

        private async Task<string> GetSerialPortOutputAsync(Instance instance)
        {
            string zone = instance.Zone.Split('/', '\\').Last();
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
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance will reside.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
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
    [Cmdlet(VerbsCommon.Remove, "GceInstance")]
    public class RemoveGceInstanceCmdlet : GceConcurrentCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
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

        /// <summary>
        /// <para type="description">
        /// Shows what would happen if the cmdlet runs.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter WhatIf { get; set; }

        /// <summary>
        /// <para type="description">
        /// Skip the confirmation dialog.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            if (WhatIf)
            {
                WriteObject($"WhatIf: Delete VM instance {Name} in zone {Zone} of project {Project}");
            }
            else if (ConfirmAction(Force, $"VM instance {Name} in zone {Zone} of project {Project}", "Remove"))
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
    public class StartGceInstance : GceConcurrentCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
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
    public class StopGceInstance : GceConcurrentCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
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
    public class RestartGceInstance : GceConcurrentCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
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
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
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
        /// ProcessRecord for AccessConfig Parameter Set
        /// </summary>
        private void ProcessAccessConfig()
        {
            foreach (string configName in DeleteAccessConfig)
            {
                DeleteAccessConfigRequest request =
                    Service.Instances.DeleteAccessConfig(
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
        /// ProcessRecord for Disk Parameter Set
        /// </summary>
        private void ProcessDisk()
        {
            foreach (string diskName in DetachDisk)
            {
                DetachDiskRequest request = Service.Instances.DetachDisk(Project, Zone, Instance, diskName);
                Operation operation = request.Execute();
                AddOperation(Project, Zone, operation);
            }

            foreach (object diskParam in AddDisk)
            {
                //Allow for taking AttachedDisk and Disk objects, and strings.
                AttachedDisk newDisk;
                if (diskParam is AttachedDisk)
                {
                    newDisk = diskParam as AttachedDisk;
                }
                else
                {
                    Disk disk = diskParam as Disk ??
                        Service.Disks.Get(Project, Zone, diskParam.ToString()).Execute();

                    newDisk = new AttachedDisk { Source = disk.SelfLink, DeviceName = disk.Name };
                }
                AttachDiskRequest request =
                    Service.Instances.AttachDisk(newDisk, Project, Zone, Instance);
                Operation operation = request.Execute();
                AddOperation(Project, Zone, operation);
            }
        }

        /// <summary>
        /// ProcessRecord for Metadata Parameter Set
        /// </summary>
        private void ProcessMetadata()
        {
            GetRequest getRequest = Service.Instances.Get(Project, Zone, Instance);
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
            SetMetadataRequest request =
                Service.Instances.SetMetadata(metadata, Project, Zone, Instance);
            AddOperation(Project, Zone, request.Execute());
        }

        /// <summary>
        /// ProcessRecord for Tag Parameter Set
        /// </summary>
        private void ProcessTag()
        {
            GetRequest getRequest = Service.Instances.Get(Project, Zone, Instance);
            Instance instance = getRequest.Execute();
            Tags tags = instance.Tags ?? new Tags();
            tags.Items = tags.Items ?? new List<string>();
            tags.Items = tags.Items.Where(tag => !RemoveTag.Contains(tag)).Concat(AddTag).ToList();
            SetTagsRequest setRequest = Service.Instances.SetTags(tags, Project, Zone, Instance);
            Operation operation = setRequest.Execute();
            AddOperation(Project, Zone, operation);
        }
    }
}
