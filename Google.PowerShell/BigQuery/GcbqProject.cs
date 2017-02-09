// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;
using System.Management.Automation;

namespace Google.PowerShell.BigQuery
{
    /// <summary>
    /// <para type="synopsis">
    /// Lists all Google Cloud Platform projects that you have permission for.
    /// </para>
    /// <para type="description">
    /// Queries to find which Google Cloud Platform projects you have been granted any project role in and writes them to the pipeline.
    /// Output is in the form of ProjectData objects that you can then filter before sending to other commands.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcbqProject</code>
    ///   <para>This command will list all of the GCP projects that you've been granted any roles for.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/projects)">
    /// [BigQuery Projects]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcbqProject")]
    public class GetGcbqProject : GcbqCmdlet
    {
        protected override void ProcessRecord()
        {
            // Create the project list request
            ProjectsResource.ListRequest request = Service.Projects.List();

            // Send request and parse response, iterating if needed.
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
