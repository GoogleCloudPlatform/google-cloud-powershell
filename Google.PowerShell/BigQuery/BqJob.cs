// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;
using Google.PowerShell.Common;
using System;
using System.Net;
using System.Management.Automation;
using Google.Cloud.BigQuery.V2;

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
    [Cmdlet(VerbsCommon.Get, "BqJob")]
    public class GetBqJob : BqCmdlet
    {
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
        /// The ID of the Job to get a reference for.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0)]
        [ValidatePattern("[a-zA-Z0-9_]")]
        public string JobId { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Job object to get an updated reference for.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ValueFromPipeline = true)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(JobList.JobsData),
            Property = nameof(JobList.JobsData.JobReference))]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Apis.Bigquery.v2.Data.Job),
            Property = nameof(Apis.Bigquery.v2.Data.Job.JobReference))]
        public JobReference InputObject { get; set; }

        protected override void ProcessRecord()
        {
            if (JobId == null && InputObject == null)
            {
                DoListRequest(Service.Jobs.List(Project));
            }
            else
            {
                if (JobId == null)
                {
                    JobId = InputObject.JobId;
                }
                DoGetRequest(Service.Jobs.Get(Project, JobId));
            }
        }

        /// <summary>
        /// Executes a List Jobs request and writes returned objects or errors.
        /// </summary>
        /// <param name="request">Pre-built request ready to be executed</param>
        public void DoListRequest(JobsResource.ListRequest request)
        {
            try
            {
                do
                {
                    JobList response = request.Execute();
                    if (response == null)
                    {
                        WriteError(new ErrorRecord(
                            new Exception("The List query returned null instead of a well formed list."),
                            "Null List Returned", ErrorCategory.ReadError, Project));
                    }
                    if (response.Jobs != null)
                    {
                        WriteObject(response.Jobs, true);
                    }
                    request.PageToken = response.NextPageToken;
                }
                while (!Stopping && request.PageToken != null);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"Error {ex.HttpStatusCode}: Permission denied to read jobs from '{Project}'.",
                    ErrorCategory.PermissionDenied, JobId));
            }
        }

        /// <summary>
        /// Executes a Get Jobs request and writes the returned Job or error.
        /// </summary>
        /// <param name="request">Pre-built request ready to be executed</param>
        public void DoGetRequest(JobsResource.GetRequest request)
        {
            try
            {
                var response = request.Execute();
                WriteObject(response);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"Error {ex.HttpStatusCode}: Job '{JobId}' not found in '{Project}'.",
                    ErrorCategory.ObjectNotFound, JobId));
            }
        }
    }

    
}
