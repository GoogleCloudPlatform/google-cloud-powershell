using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using System;
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
    /// <para type="description">
    /// </para>
    /// <para type="description">
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
        [Parameter(Position = 3, Mandatory = false, ValueFromPipeline = true, ParameterSetName = ByName)]
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
}
