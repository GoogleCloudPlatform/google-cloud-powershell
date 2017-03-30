// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;
using Google.PowerShell.Common;
using System;
using System.Net;
using System.Management.Automation;
using Google.Cloud.BigQuery.V2;
using System.Collections.Generic;

namespace Google.PowerShell.BigQuery
{
    /// <summary>
    /// <para type="synopsis">
    /// Lists all jobs that you started in the specified project or returns information about a specific job.
    /// </para>
    /// <para type="description">
    /// If no Job is specified through the JobId parameter or object via pipeline, a list of all jobs in the 
    /// specified project will be returned. If a Job is specified, it will return a descriptor object for 
    /// that job. Listing requires ‘Viewer’ or ‘Owner’ roles. Viewing information about a specific job 
    /// requires the ‘Owner’ role. Job information is stored for six months after its creation.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-BqJob</code>
    ///   <para>Lists all past or present jobs from the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-BqJob -ProjectId "my_project"</code>
    ///   <para>Lists list all past or present jobs from the specified project, "my_project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> $job = Get-BqJob "job_p6focacVVo29rJ4_yvn8Aabi2wQ"</code>
    ///   <para>This returns a descriptor object for the specified job in the default project.</para>
    /// </example> 
    /// <example>
    ///   <code>PS C:\> $job = $job | Get-BqJob</code>
    ///   <para>This will update the local descriptor $job with the most recent server state.</para>
    /// </example> 
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/jobs)">
    /// [BigQuery Jobs]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "BqJob", DefaultParameterSetName = ParameterSetNames.List)]
    public class GetBqJob : BqCmdlet
    {
        private class ParameterSetNames
        {
            public const string List = "List";
            public const string GetString = "GetString";
            public const string GetObject = "GetObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project to look for jobs in. If not set via PowerShell parameter processing, it will
        /// default to the Cloud SDK's default project.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the Job to get a reference for. Can be passed as a string parameter or
        /// as a Job object through the pipeline. Other types accepted are JobsData and JobReference.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0, ParameterSetName = ParameterSetNames.GetString)]
        [ValidateNotNullOrEmpty]
        public string JobId { get; set; }

        /// <summary>
        /// <para type="description">
        /// JobReference to get an updated Job object for. Other types accepted are Job and JobsData.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0, ValueFromPipeline = true, 
            ParameterSetName = ParameterSetNames.GetObject)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(JobList.JobsData),
            Property = nameof(JobList.JobsData.JobReference))]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Apis.Bigquery.v2.Data.Job),
            Property = nameof(Apis.Bigquery.v2.Data.Job.JobReference))]
        [ValidateNotNullOrEmpty]
        public JobReference InputObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// Filter jobs returned by state.  Options are 'Done', 'Pending', and 'Running'.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.List)]
        public JobsResource.ListRequest.StateFilterEnum State { get; set; }

        /// <summary>
        /// <para type="description">
        /// Forces the cmdlet to display jobs owned by all users in the project.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.List)]
        public SwitchParameter AllUsers { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                
                case ParameterSetNames.List:
                    WriteObject(DoListRequest(), true);
                    break;
                case ParameterSetNames.GetString:
                    DoGetRequest();
                    break;
                case ParameterSetNames.GetObject:
                    JobId = InputObject.JobId;
                    Project = InputObject.ProjectId;
                    DoGetRequest();
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        /// <summary>
        /// Executes a List Jobs request and writes returned objects or errors.
        /// </summary>
        public IEnumerable<JobList.JobsData> DoListRequest()
        {
            var request = Service.Jobs.List(Project);
            request.StateFilter = State;
            request.AllUsers = AllUsers;
            do
            {
                JobList response = request.Execute();
                if (response == null)
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new Exception("The List query returned null instead of a well formed list."),
                        "Null List Returned", ErrorCategory.ReadError, Project));
                }
                if (response.Jobs != null)
                {
                    foreach (var job in response.Jobs)
                    {
                        yield return job;
                    }
                }
                request.PageToken = response.NextPageToken;
            }
            while (!Stopping && request.PageToken != null);
        }

        /// <summary>
        /// Executes a Get Jobs request and writes the returned Job or error.
        /// </summary>
        public void DoGetRequest()
        {
            try
            {
                var request = Service.Jobs.Get(Project, JobId);
                var response = request.Execute();
                WriteObject(response);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"Error {ex.HttpStatusCode}: Job '{JobId}' not found in '{Project}'.",
                    ErrorCategory.ObjectNotFound, JobId));
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"Error while attempting to perform Get request.",
                    ErrorCategory.ObjectNotFound, JobId));
            }
        }
    }

    
}
