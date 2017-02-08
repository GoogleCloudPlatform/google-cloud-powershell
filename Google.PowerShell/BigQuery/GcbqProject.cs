using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;
using System.Management.Automation;
using System;

namespace Google.PowerShell.BigQuery
{
    /// <summary>
    /// <para type="synopsis">
    /// Lists all projects to which you have been granted any project role.
    /// </para>
    /// <para type="description">
    /// Queries to find which projects you have been granted any project role in and returns them in the form of ProjectsData objects.
    /// The flag -MaxResults can be used to specify the maximum number of results retrieved at once, and -PageToken can be used to
    /// specify an offset into the project list at which to start.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcbqProjects -MaxResults 20</code>
    ///   <para>This command will list up to the first 20 BigQuery projects that you've been granted any roles for.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcbqProjects -MaxResults 20 -PageToken 20</code>
    ///   <para>This command will list the second page of up to 20 BigQuery projects that you've been granted any roles for.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/projects)">
    /// [BigQuery Projects]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcbqProject")]
    public class GetGcbqProject : GcbqCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The maximum number of results to return.
        /// </para>
        /// </summary>
        [Alias("mr")]
        [Parameter(Mandatory = false)]
        public uint? MaxResults { get; set; }

        /// <summary>
        /// <para type="description">
        /// Page token, returned by a previous call, to request the next page of results.
        /// </para>
        /// </summary>
        [Alias("pt")]
        [Parameter(Mandatory = false)]
        public string PageToken { get; set; }

        protected override void ProcessRecord()
        {
            //check for valid value of MaxResults
            if (MaxResults <= 0)
            {
                ThrowTerminatingError(new ErrorRecord(new Exception("MaxResults only takes positive numbers."), 
                    "400", ErrorCategory.InvalidArgument, MaxResults));
            }

            //create request with flag results.  nulls should be handled
            ProjectsResource.ListRequest request = Service.Projects.List();
            request.MaxResults = MaxResults;
            request.PageToken = PageToken;

            //send request and parse response
            do
            {
                ProjectList response = request.Execute();
                if (response.Projects != null)
                {
                    WriteObject(response.Projects, true);
                }
                request.PageToken = response.NextPageToken;
            }
            while (!Stopping && request.PageToken != null);
        }
    }
}
