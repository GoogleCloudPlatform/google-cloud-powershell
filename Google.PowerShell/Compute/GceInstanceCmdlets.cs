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

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets information about Google Compute Engine VM Instances.
    /// </para>
    /// <para type="description">
    /// Gets information about VM Instances. There are two parameter sets. The default will get instance
    /// objects based on the Project, and optional Zone and instance Name. The Instance Group parameter set
    /// will get instance objects based on Project, Zone, InstanceGroup name and optional InstanceState.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceInstance", DefaultParameterSetName = ParameterSetNames.Default)]
    public class GetGceInstanceCmdlet : GceCmdlet
    {
        // The names of the parameter sets of this cmdlet
        internal class ParameterSetNames
        {
            public const string Default = "Default";
            public const string InstanceGroup = "InstanceGroup";
        }

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
            ValueFromPipeline = true, ParameterSetName = ParameterSetNames.Default)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Instance))]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance group to get the instances of.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.InstanceGroup)]
        public string InstanceGroup { get; set; }

        /// <summary>
        /// <para type="description">
        /// The state of the instances to get. Valid options are ALL and RUNNING.
        /// Defaults to ALL
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.InstanceGroup)]
        public string InstanceState { get; set; }

        /// <summary>
        /// <para type="description">
        /// A filter to send along with the request. This has the name of the property to filter on, either eq
        /// or ne, and a constant to test against.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string Filter { get; set; }

        protected override void ProcessRecord()
        {
            IEnumerable<Instance> output;
            switch (ParameterSetName)
            {
                case ParameterSetNames.InstanceGroup:
                    output = GetGroupInstances();
                    break;
                case ParameterSetNames.Default:
                    output = GetInstancesDefault();
                    break;
                default:
                    throw new InvalidOperationException(
                        $"{ParameterSetName} is not a valid ParameterSet. " +
                        $"Should be {ParameterSetNames.Default} or {ParameterSetNames.InstanceGroup}");
            }

            foreach (Instance instance in output)
            {
                WriteObject(instance);
            }
        }

        /// <summary>
        /// Gets instances for the Default parameter set
        /// </summary>
        private IEnumerable<Instance> GetInstancesDefault()
        {
            if (String.IsNullOrEmpty(Zone))
            {
                return GetAllProjectInstances();

            }
            else if (String.IsNullOrEmpty(Name))
            {
                return GetZoneInstances();
            }
            else
            {
                return GetExactInstance();
            }
        }

        private IEnumerable<Instance> GetGroupInstances()
        {
            string pageToken = null;
            do
            {
                var requestData = new InstanceGroupsListInstancesRequest { InstanceState = InstanceState };
                var listRequest = Service.InstanceGroups.ListInstances(requestData, Project, Zone, InstanceGroup);
                listRequest.Filter = Filter;
                listRequest.PageToken = pageToken;
                var response = listRequest.Execute();
                if (response.Items != null)
                {
                    foreach (InstanceWithNamedPorts i in response.Items)
                    {
                        var instanceName = i.Instance.Split('/', '\\').Last();
                        var getRequest = Service.Instances.Get(Project, Zone, instanceName);
                        yield return getRequest.Execute();
                    }
                }
                pageToken = response.NextPageToken;
            }
            while (pageToken != null);
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

        private IEnumerable<Instance> GetExactInstance()
        {
            InstancesResource.GetRequest getRequest = Service.Instances.Get(Project, Zone, Name);
            yield return getRequest.Execute();
        }
    }


    /// <summary>
    /// <para type="synopsis">
    /// Makes a new Google Compute Engine VM instance description.
    /// </para>
    /// <para type="description"> 
    /// Makes a new Google Compute Engine VM instance description.
    /// Use Add-GceInstance to instantiate the instance.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GceInstanceConfig")]
    public class NewGceInstanceCmdlet : GceCmdlet
    {
        internal class ParameterSetNames
        {
            public const string DiskByObject = "DiskByObject";
            public const string DiskByImage = "DiskByImage";
            public const string DiskBySource = "DiskBySource";
        }

        /// <summary>
        /// <para type="description">
        /// The name of the instance. The name must be 1-63 characters long and
        /// match [a-z]([-a-z0-9]*[a-z0-9])?
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// Allows this instance to send and receive packets with non-matching destination
        /// or source IPs. This is required if you plan to use this instance to forward routes.
        /// </para>
        /// </summary>
        [Parameter]
        public bool? CanIpForward { get; set; }

        /// <summary>
        /// <para type="description">
        /// A description of this resource.
        /// </para>
        /// </summary>
        [Parameter]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// Disks associated with this instance.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DiskByObject)]
        public List<AttachedDisk> Disk { get; set; }

        /// <summary>
        /// <para type="description">
        /// The path to the boot disk image.
        /// For example: "projects/debian-cloud/global/images/debian-8-jessie-v20160511".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DiskByImage)]
        public string DiskImage { get; set; }

        /// <summary>
        /// <para type="description">
        /// The path to the boot disk.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DiskBySource)]
        public string DiskSource { get; set; }

        /// <summary>
        /// <para type="description">
        /// A string describing the machine type. This can be either just the machine type name,
        /// or the full url. For example: n1-standard-4
        /// <para type="description">
        /// </summary>
        [Parameter(Mandatory = true)]
        public string MachineType { get; set; }

        /// <summary>
        /// <para type="description">
        /// The metadata key/value pairs assigned to this instance. This includes custom
        /// metadata and predefined keys.
        /// </para>
        /// </summary>
        [Parameter]
        public Hashtable Metadata { get; set; }

        /// <summary>
        /// <para type="description">
        /// An array of configurations for this interface. This specifies how this interface
        /// is configured to interact with other network services, such as connecting to
        /// the internet.
        /// </para>
        /// </summary>
        [Parameter]
        public List<NetworkInterface> NetworkInterface { get; set; }

        /// <summary>
        /// <para type="description">
        /// Scheduling options for this instance.
        /// </para>
        /// </summary>
        [Parameter]
        public Scheduling Scheduling { get; set; }

        /// <summary>
        /// <para type="description">
        /// A list of service accounts, with their specified scopes, authorized for this
        /// instance. Service accounts generate access tokens that can be accessed through
        /// the metadata server and used to authenticate applications on the instance. See
        /// Authenticating from Google Compute Engine for more information.
        /// </para>
        /// </summary>
        [Parameter]
        public List<ServiceAccount> ServiceAccount { get; set; }

        /// <summary>
        /// <para type="description">
        /// A list of tags to apply to this instance. Tags are used to identify valid sources
        /// or targets for network firewalls. Each tag within
        /// the list must comply with RFC1035.
        /// </para>
        /// </summary>
        [Parameter]
        public List<string> Tag { get; set; }

        protected override void ProcessRecord()
        {
            Instance newInstance = ProduceInstance();

            WriteObject(newInstance);
        }

        private Instance ProduceInstance()
        {
            Instance newInstance = new Instance
            {
                Name = Name,
                CanIpForward = CanIpForward,
                Description = Description,
                MachineType = MachineType,
                Scheduling = Scheduling,
                ServiceAccounts = ServiceAccount,
            };

            if (NetworkInterface != null)
            {
                newInstance.NetworkInterfaces = NetworkInterface;
            }
            else
            {
                newInstance.NetworkInterfaces = new List<NetworkInterface> {
                    new NetworkInterface {
                        Network = "global/networks/default",
                        AccessConfigs = new List<AccessConfig> {
                            new AccessConfig {
                                Type = "ONE_TO_ONE_NAT"
                            }
                        }
                    }
                };
            }

            newInstance.Disks = GetDisk();

            if (Metadata != null)
            {
                IList<Metadata.ItemsData> items = new List<Metadata.ItemsData>();
                for (var e = Metadata.GetEnumerator(); e.MoveNext();)
                {
                    items.Add(new Metadata.ItemsData { Key = e.Key.ToString(), Value = e.Value.ToString() });
                }
                newInstance.Metadata = new Metadata
                {
                    Items = items
                };
            }

            if (Tag != null)
            {
                newInstance.Tags = new Tags { Items = Tag.ToList() };
            }

            return newInstance;
        }

        private IList<AttachedDisk> GetDisk()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.DiskByObject:
                    return Disk.ToList();
                case ParameterSetNames.DiskByImage:
                    return new List<AttachedDisk> {
                        new AttachedDisk {
                            Boot = true,
                            AutoDelete = true,
                            InitializeParams = new AttachedDiskInitializeParams {
                                SourceImage = DiskImage
                            }
                        }
                    };
                case ParameterSetNames.DiskBySource:
                    return new List<AttachedDisk> {
                        new AttachedDisk { Boot = true, AutoDelete = false, Source = DiskSource } };
                default:
                    throw new InvalidOperationException(
                        $"{ParameterSetName} is not a valid ParameterSet. " +
                        $"Should be one of {ParameterSetNames.DiskByObject}, " +
                        $"{ParameterSetNames.DiskByImage}, or {ParameterSetNames.DiskBySource}");

            }
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
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance will reside.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public override string Zone { get; set; }

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
            operations.Add(operation);
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
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public override string Zone { get; set; }

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
            var request = Service.Instances.Delete(Project, Zone, Name);
            var operation = request.Execute();
            operations.Add(operation);
        }
    }
}
