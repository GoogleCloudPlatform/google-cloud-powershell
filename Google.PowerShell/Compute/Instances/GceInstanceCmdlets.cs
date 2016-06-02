// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
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
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceInstance", DefaultParameterSetName = ByName)]
    public class GetGceInstanceCmdlet : GceCmdlet
    {
        private const string ByName = "ByName";
        private const string ByInstanceGroup = "ByInstanceGroup";

        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", Type = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = false)]
        [PropertyByTypeTransformation(Property = "Name", Type = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = false, ValueFromPipeline = true, ParameterSetName = ByName)]
        [PropertyByTypeTransformation(Property = "Name", Type = typeof(Instance))]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance group to get the instances of.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ByInstanceGroup)]
        public string InstanceGroup { get; set; }

        /// <summary>
        /// <para type="description">
        /// The state of the instances to get. Valid options are <code>ALL</code> and <code>RUNNING</code>. Defaults to ALL</para>
        /// </summary>
        [Parameter(ParameterSetName = ByInstanceGroup)]
        public string InstanceState { get; set; }
        
        /// <summary>
        /// <para type="description">
        /// A filter to send along with the request. This has the name of the property to filter on, either eq or ne, and a constant to test against.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string Filter { get; set; }

        protected override void ProcessRecord()
        {
            if(InstanceGroup != null)
            {
                string pageToken = GetGroupList(null);
                while(pageToken != null)
                {
                    pageToken = GetGroupList(pageToken);
                }
            }
            else if(Zone == null)
            {
                WriteDebug($"Zone is null. Getting project {Project}");
                string pageToken = GetAgListPage(null);
                while (pageToken != null)
                {
                    pageToken = GetAgListPage(pageToken);
                }
            }
            else if(Name == null)
            {
                WriteDebug($"Name is null. Getting zone {Zone} and project {Project}");
                string pageToken = GetListPage(null);
                while (pageToken != null)
                {
                    pageToken = GetListPage(pageToken);
                }
            }
            else
            {
                WriteDebug($"Getting instance {Name} from zone {Zone} and project {Project}");
                InstancesResource.GetRequest getRequest = Service.Instances.Get(Project, Zone, Name);
                WriteObject(getRequest.Execute());
            }
        }

        private string GetGroupList(string pageToken)
        {
            var request = Service.InstanceGroups.ListInstances(new InstanceGroupsListInstancesRequest { InstanceState = InstanceState }, Project, Zone, InstanceGroup);
            request.Filter = Filter;
            request.PageToken = pageToken;
            var response = request.Execute();
            foreach (InstanceWithNamedPorts i in response.Items)
            {
                WriteObject(i.Instance);
            }
            return response.NextPageToken;
        }

        private string GetAgListPage(string pageToken)
        {
            InstancesResource.AggregatedListRequest agListRequest = Service.Instances.AggregatedList(Project);
            agListRequest.Filter = Filter;
            agListRequest.PageToken = pageToken;
            InstanceAggregatedList  agList = agListRequest.Execute();
            string nextPageToken = agList.NextPageToken;
            foreach (Instance i in agList.Items.Values.Where(l => l.Instances != null).SelectMany(l => l.Instances))
            {
                WriteObject(i);
            }
            return nextPageToken;
        }

        private string GetListPage(string pageToken)
        {
            InstancesResource.ListRequest listRequest = Service.Instances.List(Project, Zone);
            listRequest.Filter = Filter;
            InstanceList result = listRequest.Execute();

            foreach (Instance i in result.Items)
            {
                WriteObject(i);
            }

            return result.NextPageToken;
        }
    }


    /// <summary>
    /// <para type="synopsis">
    /// Makes a new Google Compute Engine VM instance description. Use Add-GceInstance to instantiate the instance.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GceInstanceConfig")]
    public class NewGceInstanceCmdlet : GceCmdlet
    {
        private const string DiskByObject = "DiskByObject";
        private const string DiskByImage = "DiskByImage";
        private const string DiskBySource = "DiskBySource";

        /// <summary>
        /// <para type="description">
        /// The name of the instance. The name must be 1-63 characters long. The first character must be a lowercase
        /// letter, and all following characters must be a dash, lowercase letter, or digit,
        /// except the last character, which cannot be a dash.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true)]
        public string Name { get; set; }
          
        /// <summary>
        /// Allows this instance to send and receive packets with non-matching destination
        /// or source IPs. This is required if you plan to use this instance to forward routes.
        /// </summary>
        [Parameter()]
        public bool? CanIpForward { get; set; }

        /// <summary>
        /// A description of this resource.
        /// </summary>
        [Parameter()]
        public string Description { get; set; }
   
        /// <summary>
        /// Disks associated with this instance.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = DiskByObject)]
        public List<AttachedDisk> Disk { get; set; }

        /// <summary>
        /// The path to the boot disk image.
        /// For example: "projects/debian-cloud/global/images/debian-8-jessie-v20160511".
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = DiskByImage)]
        public string DiskImage { get; set; }

        /// <summary>
        /// The path to the boot disk.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = DiskBySource)]
        public string DiskSource { get; set; }

        /// <summary>
        /// Full or partial URL of the machine type resource to use for this instance, in
        /// the format: zones/zone/machineTypes/machine-type. For example, the following is a valid partial url
        /// to a predefined machine type: zones/us-central1-f/machineTypes/n1-standard-1
        /// To create a custom machine type, provide a URL to a machine type in the following
        /// format, where CPUS is 1 or an even number up to 32 (2, 4, 6, ... 24, etc), and
        /// MEMORY is the total memory for this instance. Memory must be a multiple of 256
        /// MB and must be supplied in MB (e.g. 5 GB of memory is 5120 MB): zones/zone/machineTypes/custom-CPUS-MEMORY
        /// For example: zones/us-central1-f/machineTypes/custom-4-5120 For a full list of
        /// restrictions, read the Specifications for custom machine types.
        /// </summary>
        [Parameter()]
        public string MachineType { get; set; }

        /// <summary>
        /// The metadata key/value pairs assigned to this instance. This includes custom
        /// metadata and predefined keys.
        /// </summary>
        [Parameter()]
        public Hashtable Metadata { get; set; }

        /// <summary>
        /// An array of configurations for this interface. This specifies how this interface
        /// is configured to interact with other network services, such as connecting to
        /// the internet.
        /// </summary>
        [Parameter()]
        public List<NetworkInterface> NetworkInterface { get; set; }

        /// <summary>
        /// Scheduling options for this instance.
        /// </summary>
        [Parameter()]
        public Scheduling Scheduling { get; set; }

        /// <summary>
        /// A list of service accounts, with their specified scopes, authorized for this
        /// instance. Service accounts generate access tokens that can be accessed through
        /// the metadata server and used to authenticate applications on the instance. See
        /// Authenticating from Google Compute Engine for more information.
        /// </summary>
        [Parameter()]
        public List<ServiceAccount> ServiceAccount { get; set; }

        /// <summary>
        /// A list of tags to apply to this instance. Tags are used to identify valid sources
        /// or targets for network firewalls. Each tag within
        /// the list must comply with RFC1035.
        /// </summary>
        [Parameter()]
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
                Scheduling = Scheduling,
                ServiceAccounts = ServiceAccount,
            };

            if (MachineType == null)
            {
                newInstance.MachineType = "n1-standard-4";
            }
            else
            {
                newInstance.MachineType = MachineType;
            }

            if (NetworkInterface != null)
            {
                newInstance.NetworkInterfaces = NetworkInterface;
            }
            else
            {
                newInstance.NetworkInterfaces = new List<NetworkInterface> {
                    new NetworkInterface {
                        Network = "global/networks/default",
                        AccessConfigs = new List<AccessConfig> { new AccessConfig { Type = "ONE_TO_ONE_NAT"}}
                    }
                };
            }

            if (Disk != null)
            {
                newInstance.Disks = Disk.ToList();
            }
            else if (DiskImage != null)
            {
                newInstance.Disks = new List<AttachedDisk>
                {
                    new AttachedDisk { Boot = true, AutoDelete = true, InitializeParams = new AttachedDiskInitializeParams { SourceImage = DiskImage } }
                };
            }
            else if (DiskSource != null)
            {
                newInstance.Disks = new List<AttachedDisk> { new AttachedDisk { Boot = true, AutoDelete = false, Source = DiskSource } };
            }
            else
            {
                throw new InvalidOperationException("Disk, DiskImage and DiskSource can not all be null.");
            }

            if (Metadata != null)
            {
                IList<Metadata.ItemsData> items = new List<Metadata.ItemsData>();
                for (var e = Metadata.GetEnumerator(); e.MoveNext();)
                {
                    items.Add(new Metadata.ItemsData { Key = e.Key.ToString(), Value = e.Value.ToString() });
                }
            }

            if (Tag != null)
            {
                newInstance.Tags = new Tags { Items = Tag.ToList() };
            }

            return newInstance;
        }
    }

    /// <summary>
    /// Creates and starts a Google Compute Engine VM instance.
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceInstance")]
    public class AddGceInstance : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project that will own the instances.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", Type = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance will reside.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", Type = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The description of the instance to create, as generated by New-GceInstanceConfig.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ValueFromPipeline = true, ParameterSetName = "FromInstance")]
        public Instance Instance { get; set; }

        private IList<Operation> operations = new List<Operation>();

        protected override void ProcessRecord()
        {
            if (Instance.MachineType.Split('/', '\\').Length < 2)
            {
                if(Instance.MachineType.Contains("custom"))
                {
                    Instance.MachineType = "zones/" + Zone + "/machineTypes/" + Instance.MachineType;
                }
                else
                {
                    Instance.MachineType = "projects/" + Project + "/zones/" + Zone + "/machineTypes/" + Instance.MachineType;
                }
            }

            InstancesResource.InsertRequest request = Service.Instances.Insert(Instance, Project, Zone);
            Operation operation = request.Execute();
            operations.Add(operation);
        }

        protected override void EndProcessing()
        {
            foreach (Operation operation in operations)
            {
                WaitForZoneOperation(Service, Project, Zone, operation);
            }
        }
    }

    /// <summary>
    /// Deletes a Google Compute Engine VM instance.
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceInstance")]
    public class RemoveGceInstance : GceCmdlet
    {

        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", Type = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", Type = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to delete.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ValueFromPipeline = true)]
        [PropertyByTypeTransformation(Property = "Name", Type = typeof(Instance))]
        public string Name { get; set; }

        private IList<Operation> operations = new List<Operation>();
        protected override void ProcessRecord()
        {
            var request = Service.Instances.Delete(Project, Zone, Name);
            var operation = request.Execute();
            operations.Add(operation);
        }

        protected override void EndProcessing()
        {
            foreach (Operation operation in operations)
            {
                WaitForZoneOperation(Service, Project, Zone, operation);
            }
        }
    }
}
