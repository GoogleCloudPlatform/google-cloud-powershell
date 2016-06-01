using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Google.PowerShell.Compute.Instances
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets information about Google Compute Engine VM Instances.
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
        [Parameter(Position = 2, Mandatory = false, ValueFromPipeline = true)]
        [PropertyByTypeTransformation(Property = "Name", Type = typeof(Instance))]
        public string Name { get; set; }
        
        /// <summary>
        /// <para type="description">
        /// A filter to send along with the request. This has the name of the property to filter on, either eq or ne, and a constant to test against.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string Filter { get; set; }

        protected override void ProcessRecord()
        {
            if(Zone == null)
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
    [Cmdlet(VerbsCommon.New, "GceInstance")]
    public class NewGceInstanceCmdlet : GceCmdlet
    {
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
        [Parameter(Mandatory = false)]
        public bool? CanIpForward { get; set; }

        /// <summary>
        /// A description of this resource.
        /// </summary>
        [Parameter(Mandatory = false)]
        public string Description { get; set; }
   
        /// <summary>
        /// Array of disks associated with this instance. Persistent disks must be created
        /// before you can assign them.
        /// </summary>
        [Parameter(Mandatory = false)]
        public AttachedDisk[] Disks { get; set; }

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
        [Parameter(Mandatory = false)]
        public string MachineType { get; set; }

        /// <summary>
        /// The metadata key/value pairs assigned to this instance. This includes custom
        /// metadata and predefined keys.
        /// </summary>
        [Parameter(Mandatory = false)]
        public Hashtable Metadata { get; set; }

        /// <summary>
        /// An array of configurations for this interface. This specifies how this interface
        /// is configured to interact with other network services, such as connecting to
        /// the internet.
        /// </summary>
        [Parameter(Mandatory = true)]
        public NetworkInterface[] NetworkInterfaces { get; set; }

        /// <summary>
        /// Scheduling options for this instance.
        /// </summary>
        [Parameter(Mandatory = false)]
        public Scheduling Scheduling { get; set; }

        /// <summary>
        /// A list of service accounts, with their specified scopes, authorized for this
        /// instance. Service accounts generate access tokens that can be accessed through
        /// the metadata server and used to authenticate applications on the instance. See
        /// Authenticating from Google Compute Engine for more information.
        /// </summary>
        [Parameter(Mandatory = false)]
        public ServiceAccount[] ServiceAccounts { get; set; }

        /// <summary>
        /// A list of tags to apply to this instance. Tags are used to identify valid sources
        /// or targets for network firewalls. Each tag within
        /// the list must comply with RFC1035.
        /// </summary>
        [Parameter(Mandatory = false)]
        public string[] Tags { get; set; }

        protected override void ProcessRecord()
        {
            Instance newInstance = new Instance {
                Name = Name,
                CanIpForward = CanIpForward,
                Description = Description,
                Disks = Disks,
                NetworkInterfaces = NetworkInterfaces,
                MachineType = MachineType,
                Scheduling = Scheduling,
                ServiceAccounts = ServiceAccounts,
            };
            if(MachineType == null)
            {
                newInstance.MachineType =
            }
            if (Metadata != null)
            {
                IList<Metadata.ItemsData> items = new List<Metadata.ItemsData>();
                for(var e = Metadata.GetEnumerator(); e.MoveNext();)
                {
                    items.Add(new Metadata.ItemsData { Key = e.Key.ToString(), Value = e.Value.ToString() });
                }
            }

            if (Tags != null)
            {
                newInstance.Tags = new Tags { Items = Tags.ToList() };
            }
            WriteObject(newInstance);
        }
    }

    [Cmdlet(VerbsCommon.Add, "GceInstance")]
    public class AddGceInstance : GceCmdlet
    {
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", Type = typeof(Project))]
        public string Project { get; set; }

        [Parameter(Position = 1, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", Type = typeof(Zone))]
        public string Zone { get; set; }

        [Parameter(Position = 2, Mandatory = true, ValueFromPipeline = true, ParameterSetName = "FromInstance")]
        public Instance Instance { get; set; }

        protected override void ProcessRecord()
        {
            InstancesResource.InsertRequest request = Service.Instances.Insert(Instance, Project, Zone);
            Operation operation = request.Execute();
            if (operation.Warnings != null)
            {
                foreach (Operation.WarningsData warning in operation.Warnings)
                {
                    WriteWarning(warning.Message);
                }
            }
        }
    }
}
