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
        [Parameter(Position = 3, Mandatory = false, ValueFromPipeline = true)]
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
}
