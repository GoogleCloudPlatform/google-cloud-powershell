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

    /// <summary>
    /// <para type="synopsis">
    /// Starts a new, asynchronous BigQuery Job. Currently, the only supported type is Query.
    /// </para>
    /// <para type="description">
    /// Starts a new asynchronous job. This call requires the ‘Viewer’ role. The Type parameter 
    /// can be -Query, -Copy, -Load, or -Extract.  Each of these job types has its own set of 
    /// type-specific parameters to define what the job does.  Job types all share a set of 
    /// parameters that define job attributes such as start time and handle statistics such 
    /// as rows and raw amounts of data processed. This cmdlet supports ShouldProcess, and as 
    /// such, has the -WhatIf parameter to show the projected results of the cmdlet without 
    /// actually changing any server resources.
    /// Use -PollUntilComplete to have the cmdlet treat the job as a blocking operation.  
    /// It will poll until the job has finished, and then it will return a job reference. 
    /// Tables referenced in queries should be fully qualified, but to use any that are not, 
    /// the DefaultDataset parameter must be used to specify where to find them.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $job = Start-BqJob -Query "select * from book_data.classics where Year > 1900"
    ///   </code>
    ///   <para>Queries the classics table and returns a Job object so that results can be viewed.</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> $job = Start-BqJob -Query "select * from classics where Year > 1900" `
    /// -DefaultDataset $dataset -DestinationTable $table
    ///   </code>
    ///   <para>Queries with a default dataset and using a permanent table as the destination for results.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/jobs)">
    /// [BigQuery Jobs]
    /// </para>
    /// </summary>
    [Cmdlet("Start", "BqJob", SupportsShouldProcess = true)]
    public class StartBqJob : BqCmdlet
    {
        private class ParameterSetNames
        {
            public const string DoQuery = "DoQuery";
            public const string DoCopy = "DoCopy";
            public const string DoLoad = "DoLoad";
            public const string DoExtract = "DoExtract";
        }

        // All-Type Parameters.

        /// <summary>
        /// <para type="description">
        /// The project to run jobs in. If not set via PowerShell parameter processing, it will
        /// default to the Cloud SDK's default project.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Turns the async call into a synchronous call by polling until the job is complete before 
        /// returning. Can also be accessed by '-Synchronous'.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [Alias("Synchronous")]
        public SwitchParameter PollUntilComplete { get; set; }

        // Query Parameters.
        //TODO(ahandley): Billing params for Queries.

        /// <summary>
        /// <para type="description">
        /// Selects job type Query.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DoQuery)]
        public SwitchParameter Query { get; set; }

        /// <summary>
        /// <para type="description">
        /// A query string, following the BigQuery query syntax, of the query to execute.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.DoQuery)]
        [ValidateNotNullOrEmpty]
        public string QueryString { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specifies BigQuery's legacy SQL dialect for this query.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoQuery)]
        public SwitchParameter UseLegacySql { get; set; }

        /// <summary>
        /// <para type="description">
        /// The dataset to use for any unqualified table names in QueryString.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoQuery)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Dataset),
            Property = nameof(Dataset.DatasetReference))]
        [Alias("Dataset")]
        public DatasetReference DefaultDataset { get; set; }

        /// <summary>
        /// <para type="description">
        /// The destination table to write the results into. If this is not specified, the 
        /// results will be stored in a temporary table.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoQuery)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Table),
            Property = nameof(Table.TableReference))]
        [Alias("Table", "Dest")]
        public TableReference DestinationTable { get; set; }

        /// <summary>
        /// <para type="description">
        /// Priority of the query.  Can be 'Batch' or 'Interactive'.
        /// </para>
        /// </summary>
        public QueryPriority Priority { get; set; }

        // Copy Parameters.

        /// <summary>
        /// <para type="description">
        /// Selects job type Copy.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DoCopy)]
        public SwitchParameter Copy { get; set; }

        // Load Parameters.

        /// <summary>
        /// <para type="description">
        /// Selects job type Load.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DoLoad)]
        public SwitchParameter Load { get; set; }

        // Extract Parameters.

        /// <summary>
        /// <para type="description">
        /// Selects job type Extract.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DoExtract)]
        public SwitchParameter Extract { get; set; }

        protected override void ProcessRecord()
        {
            Apis.Bigquery.v2.Data.Job result;

            switch (ParameterSetName)
            {
                case ParameterSetNames.DoQuery:
                    result = DoQuery();
                    break;
                case ParameterSetNames.DoCopy:
                    result = DoCopy();
                    break;
                case ParameterSetNames.DoLoad:
                    result = DoLoad();
                    break;
                case ParameterSetNames.DoExtract:
                    result = DoExtract();
                    break;
                default:
                    ThrowTerminatingError(new ErrorRecord(
                        new Exception("You must select a valid BQ Job type."),
                        "Type Not Found", ErrorCategory.ObjectNotFound, this));
                    result = null;
                    break;
            }

            WriteObject(result);
        }

        /// <summary>
        /// Query Job main processing function.
        /// </summary>
        public Apis.Bigquery.v2.Data.Job DoQuery()
        {
            if (ShouldProcess($"\n\nProject: {Project}\nQuery: {QueryString}\n\n"))
            {
                try
                {
                    var options = new CreateQueryJobOptions();
                    options.UseLegacySql = UseLegacySql;
                    options.Priority = Priority;
                    options.DestinationTable = DestinationTable;
                    options.DefaultDataset = DefaultDataset;

                    BigQueryJob bqr = Client.CreateQueryJob(QueryString, options);

                    if (PollUntilComplete)
                    {
                        bqr.PollUntilCompleted();
                    }

                    return bqr.Resource;
                }
                catch (Exception ex)
                {
                    ThrowTerminatingError(new ErrorRecord(ex, "Query rejected",
                        ErrorCategory.InvalidOperation, this));
                }
            }
            return null;
        }

        /// <summary>
        /// Copy Job main processing function.
        /// </summary>
        public Apis.Bigquery.v2.Data.Job DoCopy()
        {
            throw new NotImplementedException(
                "Copy jobs are not implemented yet.  Use the *-BqTabledata cmdlets instead.");
        }

        /// <summary>
        /// Load Job main processing function.
        /// </summary>
        public Apis.Bigquery.v2.Data.Job DoLoad()
        {
            throw new NotImplementedException(
                "Load jobs are not implemented yet.  Use Set-BqTabledata instead.");
        }

        /// <summary>
        /// Extract Job main processing function.
        /// </summary>
        public Apis.Bigquery.v2.Data.Job DoExtract()
        {
            throw new NotImplementedException(
                "Extract jobs are not implemented yet.  Use Get-BqTabledata instead.");
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Returns the result of a completed BQ Job.
    /// </para>
    /// <para type="description">
    /// Returns the result of a completed BQ Job. Requires the ‘Reader’ dataset role. You can specify 
    /// how long the call should wait for the query to be completed, if it is not already finished. 
    /// This is done with the -Timeout parameter. An integer number of seconds is taken, and the 
    /// default is 10. This cmdlet returns BigQueryRow objects.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $job = Start-BqJob -Query "select * from book_data.classics"
    /// PS C:\> $job | Receive-BqJob -Timeout 60
    ///   </code>
    ///   <para>This will run a query in the book_data.classics table and will wait up to 60 seconds 
    ///   for its completion.  When it finishes, it will print a number of BigQueryRow objects to 
    ///   the terminal.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/jobs)">
    /// [BigQuery Jobs]
    /// </para>
    /// </summary>
    [Cmdlet("Receive", "BqJob")]
    public class ReceiveBqJob : BqCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// JobReference to get results from. Other types accepted are Job and JobsData.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0, ValueFromPipeline = true)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(JobList.JobsData),
            Property = nameof(JobList.JobsData.JobReference))]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Apis.Bigquery.v2.Data.Job),
            Property = nameof(Apis.Bigquery.v2.Data.Job.JobReference))]
        [ValidateNotNull]
        public JobReference InputObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// Max time, in seconds, to wait for the job to complete before failing (Default: 10).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public int Timeout { get; set; }

        protected override void ProcessRecord()
        {
            //Set Project for the lazy instantiation of a BQ Client object
            Project = InputObject.ProjectId;

            var options = new GetQueryResultsOptions();
            options.Timeout = new TimeSpan(0, 0, (Timeout < 10) ? 10 : Timeout);
            BigQueryResults result;

            try
            {
                result = Client.GetQueryResults(InputObject, options);
                foreach (BigQueryRow row in result.GetRows())
                {
                    WriteObject(row);
                }
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex, "Failed to receive results.",
                        ErrorCategory.InvalidOperation, this));
            }
        }
    }

}
