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
using System.Net;
using System.Threading.Tasks;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets information about one or more Google Compute Engine VM instances.
    /// </para>
    /// <para type="description">
    /// Gets information about all Google Compute Engine VM instances. Can get all instances of a project, or 
    /// all instances in a zone, or a specific instance by name. Can also get all instances of a managed
    /// instance group.
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instances#resource)">
    /// [Instance resource definition]
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GceInstance -Project "my-project"</code>
    ///   <para>Gets all instances of the project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceInstance -Zone "us-west1-a"</code>
    ///   <para>Gets all instances in the zone "us-west1-a" in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceInstance "my-instance"</code>
    ///   <para>Gets the instance named "my-instance" in the default project and zone</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceInstance -ManagedGroupName "my-group"</code>
    ///   <para>Gets all instances that are members of the managed instance group named "my-group".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceInstance "my-instance" -SerialPortOutput -Port 4.</code>
    ///   <para>Returns the data from serial port 4 of "my-instance".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceInstance", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(Instance), typeof(string))]
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
        public override string Project { get; set; }

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

        /// <summary>
        /// <para type="description">
        /// The number of the serial port to read from. Defaults to 1. Has no effect if -SerialPortOutput is
        /// not set. Must be between 1 and 4, inclusive.
        /// </para>
        /// </summary>
        [Parameter]
        public int? PortNumber { get; set; } = 1;

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
            request.Port = PortNumber;
            SerialPortOutput output = await request.ExecuteAsync();
            return output.Contents;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates and starts a Google Compute Engine VM instance.
    /// </para>
    /// <para type="description">
    /// Creates and starts a Google Compute Engine VM instance. You create a new instance by either using an 
    /// instance config created by New-GceInstanceConfig, or by specifying the parameters you want on this
    /// cmdlet.
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GceInstanceConfig -Name "new-instance" -BootDiskImage $image |
    ///           Add-GceInstance -Project "my-project" -Zone "us-central1-a"
    ///   </code>
    ///   <para>Creates a new instance from an instance config.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///     PS C:\> Add-GceInstance -Name "new-instance" -BootDisk $disk `
    ///         -MachineType "n1-standard-4" `
    ///         -Tag http, https `
    ///         -Metadata @{"windows-startup-script-ps1" =
    ///                 "Read-GcsObject bucket object -OutFile temp.txt"}
    ///   </code>
    ///   <para>
    ///     Creates a new instance in the default project and zone. The boot disk is the prexisting disk
    ///     stored in $disk, the machine type has 4 cores, it runs a script on startup, and it is tagged as an
    ///     http and https server.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///     PS C:\> Add-GceInstance -Name "new-instance" -BootDisk $disk `
    ///         -MachineType "n1-standard-4" `
    ///         -Subnetwork "my-subnetwork" `
    ///         -Address "10.128.0.1"
    ///   </code>
    ///   <para>
    ///     Creates a new instance in the default project and zone. The boot disk is the prexisting disk
    ///     stored in $disk, the machine type has 4 cores, it uses the subnetwork "my-subnetwork" and
    ///     the ip address "10.123.0.1" (this address must be within the subnetwork).
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instances#resource)">
    /// [Instance resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceInstance",
        DefaultParameterSetName = ParameterSetNames.ByValues)]
    [OutputType(typeof(Instance))]
    public class AddGceInstanceCmdlet : GceInstanceDescriptionCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByValues = "ByValues";
            public const string ByValuesCustomMachine = "ByValuesCustomMachine";
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
        public override string Project { get; set; }

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
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
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
        public override string MachineType { get; set; }

        /// <summary>
        /// <para type="description">
        /// Number of vCPUs used for a custom machine type.
        /// This has to be used together with CustomMemory.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true,
            ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override int CustomCpu { get; set; }

        /// <summary>
        /// <para type="description">
        /// Total amount of memory used for a custom machine type.
        /// This has to be used together with CustomCpu.
        /// The amount of memory is in MB.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true,
            ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override int CustomMemory { get; set; }

        /// <summary>
        /// <para type="description">
        /// Enables instances to send and receive packets for IP addresses other than their own. Switch on if
        /// this instance will be used as an IP gateway or it will be set as the next-hop in a Route
        /// resource.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override SwitchParameter CanIpForward { get; set; }

        /// <summary>
        /// <para type="description">
        /// Human readable description of this instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The persistant disk to use as a boot disk. Use Get-GceDisk to get one of these.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override Disk BootDisk { get; set; }

        /// <summary>
        /// <para type="description">
        /// The the image used to create the boot disk. Use Get-GceImage to get one of these.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        [Alias("DiskImage")]
        public override Image BootDiskImage { get; set; }

        /// <summary>
        /// <para type="description">
        /// An existing disk to attach. It will attach in read-only mode.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override Disk[] ExtraDisk { get; set; }

        /// <summary>
        /// <para type="description">
        /// An AttachedDisk object specifying a disk to attach. Do not specify `-BootDiskImage` or
        /// `-BootDiskSnapshot` if this is a boot disk. You can build one using New-GceAttachedDiskConfig.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override AttachedDisk[] Disk { get; set; }

        /// <summary>
        /// <para type="description">
        /// The keys and values of the Metadata of this instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override IDictionary Metadata { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the network to use. If not specified, is default. This can be a Network object you get
        /// from Get-GceNetwork.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        [PropertyByTypeTransformation(Property = nameof(Apis.Compute.v1.Data.Network.SelfLink),
            TypeToTransform = typeof(Network))]
        public override string Network { get; set; }

        /// <summary>
        /// <para type="description">
        /// The region in which the subnet of the instance will reside. Defaults to the region in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Region))]
        public override string Region { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the subnetwork to use.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        [ValidateNotNullOrEmpty]
        public override string Subnetwork { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the instance will not have an external ip address.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override SwitchParameter NoExternalIp { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the instance will be preemptible. If set, AutomaticRestart will be false.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override SwitchParameter Preemptible { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the instance will not restart when shut down by Google Compute Engine.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override bool AutomaticRestart { get; set; } = true;

        /// <summary>
        /// <para type="description">
        /// If set, the instance will terminate rather than migrate when the host undergoes maintenance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override SwitchParameter TerminateOnMaintenance { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ServiceAccount used to specify access tokens. Use New-GceServiceAccountConfig to build one.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override ServiceAccount[] ServiceAccount { get; set; }

        /// <summary>
        /// <para type="description">
        /// A tag of this instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override string[] Tag { get; set; }

        /// <summary>
        /// <para type="description">
        /// The map of labels (key/value pairs) to be applied to the instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        public override Hashtable Label { get; set; }

        /// <summary>
        /// <para type="description">
        /// The static ip address this instance will have. Can be a string, or and Address object from
        /// Get-GceAddress.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValuesCustomMachine)]
        [PropertyByTypeTransformation(Property = nameof(Apis.Compute.v1.Data.Address.AddressValue),
            TypeToTransform = typeof(Address))]
        public override string Address { get; set; }

        protected override void ProcessRecord()
        {
            Instance instance;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByValues:
                    if (string.IsNullOrEmpty(MachineType))
                    {
                        MachineType = "n1-standard-1";
                    }
                    instance = BuildInstance();
                    break;
                case ParameterSetNames.ByValuesCustomMachine:
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
            try
            {
                Operation operation = request.Execute();
                AddZoneOperation(Project, Zone, operation, () =>
                {
                    WriteObject(Service.Instances.Get(Project, Zone, instance.Name).Execute());
                });
            }
            catch (GoogleApiException apiEx) when (apiEx.HttpStatusCode == HttpStatusCode.Conflict)
            {
                WriteResourceExistsError(
                    exceptionMessage: $"Instance '{Name}' already exists in zone '{Zone}' of project '{Project}'",
                    errorId: "InstanceAlreadyExists",
                    targetObject: instance);
            }
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
    /// <example>
    ///     <code>PS C:\> Remove-GceInstance "my-instance"</code>
    ///     <para>Removes the instance named "my-instance" in the default project and zone.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Get-GceInstance -Project "my-project"|
    ///       where Status -eq Stopped |
    ///       Remove-GceInstance
    ///   </code>
    ///   <para>Removes all instances in project "my-project" that are currently stopped.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instances#resource)">
    /// [Instance resource definition]
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
        public override string Project { get; set; }

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
    /// <example>
    ///   <code>PS C:\> Start-GceInstance "my-instance"</code>
    ///   <para>Starts the instance named "my-instance" in the default project and zone.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Get-GceInstance -Project "my-project"|
    ///       where Status -eq Stopped |
    ///       Start-GceInstance
    ///   </code>
    ///   <para>Starts all instances in project "my-project" that are currently stopped.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instances#resource)">
    /// [Instance resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Start, "GceInstance")]
    [OutputType(typeof(Instance))]
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
        public override string Project { get; set; }

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
    /// <example>
    ///   <code>PS C:\> Stop-GceInstance "my-instance"</code>
    ///   <para>Stops the instance named "my-instance" in the default project and zone.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Get-GceInstance -Project "my-project"|
    ///       where Status -eq Running |
    ///       Stop-GceInstance
    ///   </code>
    ///   <para>Stops all instances in project "my-project" that are currently running.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instances#resource)">
    /// [Instance resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Stop, "GceInstance")]
    [OutputType(typeof(Instance))]
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
        public override string Project { get; set; }

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
    /// <example>
    ///   <code>PS C:\> Reset-GceInstance "my-instance"</code>
    ///   <para>Resets the instance named "my-instance" in the default project and zone.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Get-GceInstance -Project "my-project"|
    ///       Reset-GceInstance
    ///   </code>
    ///   <para>Removes all instances in project "my-project".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instances#resource)">
    /// [Instance resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Restart, "GceInstance")]
    [OutputType(typeof(Instance))]
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
        public override string Project { get; set; }

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
    /// With this cmdlet, you can update metadata, attach and detach disks, add and remove access configs,
    /// or add and remove tags.
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GceInstance -Name "my-instance" -AttachDisk $disk
    ///   </code>
    ///   <para>Attach disk $disk to the instance "my-instance" in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GceInstance -Name "my-instance" -RemoveDisk "my-disk" -Project "my-project"
    ///   </code>
    ///   <para>
    ///   Remove disk "my-disk" from the instance "my-instance" in the project "my-project".
    ///   Please note that "my-disk" is the device name of the disk in the instance, not the
    ///   persistent name of the disk.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GceInstance -Name "my-instance" -TurnOnAutoDeleteDisk "my-disk"
    ///   </code>
    ///   <para>
    ///   Turn on autodelete for disk "my-disk" from the instance "my-instance".
    ///   Please note that "my-disk" is the device name of the disk in the instance, not the
    ///   persistent name of the disk.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GceInstance -Name "my-instance" -TurnOffAutoDeleteDisk $disk1, $disk2
    ///   </code>
    ///   <para>
    ///   Turn off autodelete for disk $disk1 and $disk2 from the instance "my-instance".
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instances#resource)">
    /// [Instance resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GceInstance")]
    [OutputType(typeof(Instance))]
    public class SetGceInstanceCmdlet : GceConcurrentCmdlet
    {
        private static class ParameterSetNames
        {
            public const string AccessConfig = "AccessConfig";
            public const string Disk = "Disk";
            public const string AutoDeleteDisk = "AutoDeleteDisk";
            public const string Metadata = "Metadata";
            public const string Tag = "Tag";
            public const string AccessConfigByObject = "AccessConfigByObject";
            public const string DiskByObject = "DiskByObject";
            public const string AutoDeleteDiskByObject = "AutoDeleteDiskByObject";
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
        [Parameter(ParameterSetName = ParameterSetNames.AutoDeleteDisk)]
        [Parameter(ParameterSetName = ParameterSetNames.Metadata)]
        [Parameter(ParameterSetName = ParameterSetNames.Tag)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public override string Project { get; set; }

        private string _project;

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AccessConfig)]
        [Parameter(ParameterSetName = ParameterSetNames.Disk)]
        [Parameter(ParameterSetName = ParameterSetNames.AutoDeleteDisk)]
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
        [Parameter(ParameterSetName = ParameterSetNames.AutoDeleteDisk, Mandatory = true,
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
        [Parameter(ParameterSetName = ParameterSetNames.AutoDeleteDiskByObject, Mandatory = true,
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
        [ValidateNotNullOrEmpty]
        public object[] AddDisk { get; set; } = { };

        /// <summary>
        /// <para type="description">
        /// The name of the disk to detach.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Disk)]
        [Parameter(ParameterSetName = ParameterSetNames.DiskByObject)]
        [ArrayPropertyTransform(typeof(Disk), nameof(Disk.SelfLink))]
        [ValidateNotNullOrEmpty]
        public string[] RemoveDisk { get; set; } = { };

        /// <summary>
        /// <para type="description">
        /// The names of the disks to turn on autodelete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AutoDeleteDisk)]
        [Parameter(ParameterSetName = ParameterSetNames.AutoDeleteDiskByObject)]
        [ArrayPropertyTransform(typeof(Disk), nameof(Disk.SelfLink))]
        [ValidateNotNullOrEmpty]
        public string[] TurnOnAutoDeleteDisk { get; set; } = { };

        /// <summary>
        /// <para type="description">
        /// The names of the disks to turn off autodelete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AutoDeleteDisk)]
        [Parameter(ParameterSetName = ParameterSetNames.AutoDeleteDiskByObject)]
        [ArrayPropertyTransform(typeof(Disk), nameof(Disk.SelfLink))]
        [ValidateNotNullOrEmpty]
        public string[] TurnOffAutoDeleteDisk { get; set; } = { };

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

        // List of attached disks of the instance.
        private IList<AttachedDisk> attachedDisks;

        protected override void ProcessRecord()
        {
            // Parameter set can change between pipeline inputs.
            UpdateConfigPropertyNameAttribute();
            switch (ParameterSetName)
            {
                case ParameterSetNames.AccessConfig:
                case ParameterSetNames.Disk:
                case ParameterSetNames.AutoDeleteDisk:
                case ParameterSetNames.Metadata:
                case ParameterSetNames.Tag:
                    _project = Project;
                    _zone = Zone;
                    _name = Name;
                    break;
                case ParameterSetNames.AccessConfigByObject:
                case ParameterSetNames.DiskByObject:
                case ParameterSetNames.AutoDeleteDiskByObject:
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
                case ParameterSetNames.AutoDeleteDisk:
                case ParameterSetNames.AutoDeleteDiskByObject:
                    ProcessAutoDeleteDisk();
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
                AddZoneOperation(_project, _zone, operation, () =>
                {
                    WriteObject(Service.Instances.Get(_project, _zone, _name));
                });
            }

            foreach (AccessConfig accessConfig in AddAccessConfig)
            {
                InstancesResource.AddAccessConfigRequest request = Service.Instances.AddAccessConfig(
                    accessConfig, _project, _zone, _name, NetworkInterface);
                Operation response = request.Execute();
                AddZoneOperation(_project, _zone, response, () =>
                {
                    WriteObject(Service.Instances.Get(_project, _zone, _name));
                });
            }
        }

        // Given a list of string, transform any selflink URI into attached disk name.
        private IEnumerable<string> GetAttachedDiskName(string[] diskNames)
        {
            if (diskNames.Any(diskName => Uri.IsWellFormedUriString(diskName, UriKind.Absolute)))
            {
                // The disks on the GCE instance we need to set.
                // This is used for the case when there are self-links instead of disk names.
                // We cached this in private field so subsequent calls to the function can use it.
                if (attachedDisks == null)
                {
                    Instance gceInstance = Service.Instances.Get(_project, _zone, _name).Execute();
                    attachedDisks = gceInstance?.Disks;
                }

                return diskNames.Select(diskName =>
                    {
                        // If the diskName is a self link, we have to get the device name from attachedDisks list.
                        if (Uri.IsWellFormedUriString(diskName, UriKind.Absolute))
                        {
                            AttachedDisk attachedDisk = attachedDisks.FirstOrDefault(
                                disk => string.Equals(disk.Source, diskName, StringComparison.OrdinalIgnoreCase));
                            if (attachedDisk == null)
                            {
                                WriteResourceMissingError($"Disk '{diskName}' cannot be found.", "MissingAttachedDisk", diskName);
                            }
                            return attachedDisk.DeviceName;
                        }

                        return diskName;
                    });
            }

            return diskNames;
        }

        /// <summary>
        /// ProcessRecord for Disk parameter set.
        /// </summary>
        private void ProcessDisk()
        {
            foreach (string diskName in GetAttachedDiskName(RemoveDisk))
            {
                InstancesResource.DetachDiskRequest request = Service.Instances.DetachDisk(
                    _project, _zone, _name, diskName);
                Operation operation = request.Execute();
                AddZoneOperation(_project, _zone, operation, () =>
                {
                    WriteObject(Service.Instances.Get(_project, _zone, _name));
                });
            }

            foreach (object diskParam in AddDisk)
            {
                // Allow for taking Disk, AttachedDisk, and string objects.
                var newDisk = diskParam as AttachedDisk ?? (diskParam as PSObject)?.BaseObject as AttachedDisk;
                if (newDisk == null)
                {
                    var disk = diskParam as Disk ?? (diskParam as PSObject)?.BaseObject as Disk;
                    if (disk == null)
                    {
                        disk = Service.Disks.Get(_project, _zone, diskParam.ToString()).Execute();
                    }
                    newDisk = new AttachedDisk { Source = disk.SelfLink, DeviceName = disk.Name };
                }
                InstancesResource.AttachDiskRequest request =
                    Service.Instances.AttachDisk(newDisk, _project, _zone, _name);
                Operation operation = request.Execute();
                AddZoneOperation(_project, _zone, operation, () =>
                {
                    WriteObject(Service.Instances.Get(_project, _zone, _name));
                });
            }
        }

        /// <summary>
        /// ProcessRecord for AutoDeleteDisk parameter set.
        /// </summary>
        private void ProcessAutoDeleteDisk()
        {
            SetAutoDeleteDisk(true, TurnOnAutoDeleteDisk);
            SetAutoDeleteDisk(false, TurnOffAutoDeleteDisk);
        }

        /// <summary>
        /// Given an array of disks, turn on or off autodelete for them.
        /// </summary>
        private void SetAutoDeleteDisk(bool autoDelete, string[] diskNames)
        {
            foreach (string diskName in GetAttachedDiskName(diskNames))
            {
                InstancesResource.SetDiskAutoDeleteRequest request = Service.Instances.SetDiskAutoDelete(
                    _project, _zone, _name, autoDelete, diskName);
                Operation operation = request.Execute();
                AddZoneOperation(_project, _zone, operation, () =>
                {
                    WriteObject(Service.Instances.Get(_project, _zone, _name));
                });
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
            Operation operation = request.Execute();
            AddZoneOperation(_project, _zone, operation, () =>
            {
                WriteObject(Service.Instances.Get(_project, _zone, _name));
            });
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
            AddZoneOperation(_project, _zone, operation, () =>
            {
                WriteObject(Service.Instances.Get(_project, _zone, _name));
            });
        }
    }
}
