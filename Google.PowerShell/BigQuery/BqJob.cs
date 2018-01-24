// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;
using Google.PowerShell.Common;
using System;
using System.Net;
using System.Linq;
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
    /// that job. Listing requires "Viewer" or "Owner" roles. Viewing information about a specific job 
    /// requires the "Owner" role. Job information is stored for six months after its creation.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> Get-BqJob
    ///   </code>
    ///   <para>Lists all past or present jobs from the default project.</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> Get-BqJob -ProjectId "my_project"
    ///   </code>
    ///   <para>Lists list all past or present jobs from the specified project, "my_project".</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> $job = Get-BqJob "job_p6focacVVo29rJ4_yvn8Aabi2wQ"
    ///   </code>
    ///   <para>This returns a descriptor object for the specified job in the default project.</para>
    /// </example> 
    /// <example>
    ///   <code>
    /// PS C:\> $job = $job | Get-BqJob
    ///   </code>
    ///   <para>This will update the local descriptor "$job" with the most recent server state.</para>
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
        /// Filter jobs returned by state. Options are "DONE", "PENDING", and "RUNNING"
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
    /// Starts a new, asynchronous BigQuery Job.
    /// </para>
    /// <para type="description">
    /// Starts a new asynchronous job. This call requires the "Viewer" role. The Type parameter 
    /// can be "-Query", "-Copy", "-Load", or "-Extract". Each of these job types has its own set of 
    /// type-specific parameters to define what the job does (see below). Job types all share a set 
    /// of parameters that define job attributes such as start time and handle statistics such 
    /// as rows and raw amounts of data processed. This PowerShell module does not support billing 
    /// tier or maximum billed data control for individual queries, so the project defaults will be 
    /// taken. This cmdlet supports "ShouldProcess()", and as such, has the "-WhatIf" parameter to 
    /// show the projected results of the cmdlet without actually changing any server resources. 
    /// 
    /// Use "-PollUntilComplete" to have the cmdlet treat the job as a blocking operation. 
    /// It will poll until the job has finished, and then it will return a job reference. 
    /// Tables referenced in queries should be fully qualified, but to use any that are not, 
    /// the DefaultDataset parameter must be used to specify where to find them.
    /// 
    /// | All Job Flags: -Project -PollUntilComplete
    /// | Query Job Flags: -QueryString, -UseLegacySql, -DefaultDataset, -Priority
    /// | Copy Job Flags: -Source, -Destination, WriteMode
    /// | Load Job Flags: -Destination, -Type, -SourceUris, -Encoding, -FieldDelimiter, -Quote, -SkipLeadingRows, 
    /// -AllowUnknownFields, -AllowJaggedRows, -AllowQuotedNewlines
    /// | Extract Job Flags: -Source, -Type, -DestinationUris, -FieldDelimiter, -Compress, -NoHeader
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
    /// <example>
    ///   <code>
    /// PS C:\> $source_table | Get-BqTable -DatasetId "books" "classics"
    /// PS C:\> $dest_table | Get-BqTable -DatasetId "books" "suggestions"
    /// PS C:\> $source_table | Start-BqJob -Copy $dest_table -WriteMode WriteAppend -PollUntilComplete
    ///   </code>
    ///   <para>Copies the contents of the source to the end of the destination table as long as the 
    ///   source and destination schemas match.</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> $gcspath = "gs://ps_test"
    /// PS C:\> $table | Get-BqTable -DatasetId "books" "classics"
    /// PS C:\> $job = $table | Start-BqJob -Load CSV "$gcspath/basic.csv" -SkipLeadingRows 1 -Synchronous
    ///   </code>
    ///   <para>Loads in a table from "basic.csv" in the GCS bucket "ps_test".</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> $gcspath = "gs://ps_test"
    /// PS C:\> $table | Get-BqTable -DatasetId "books" "classics"
    /// PS C:\> $job = $table | Start-BqJob -Extract CSV "$gcspath/basic.csv" -Synchronous
    ///   </code>
    ///   <para>Exports the given table to a .csv file in Cloud Storage.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/jobs)">
    /// [BigQuery Jobs]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/storage/)">
    /// [Google Cloud Storage]
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
        /// returning. Can also be accessed by "-Synchronous".
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
        [PropertyByTypeTransformation(TypeToTransform = typeof(DatasetList.DatasetsData),
            Property = nameof(DatasetList.DatasetsData.DatasetReference))]
        public DatasetReference DefaultDataset { get; set; }

        /// <summary>
        /// <para type="description">
        /// Priority of the query. Can be "Batch" or "Interactive".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoQuery)]
        public QueryPriority Priority { get; set; }

        // Copy Parameters.

        /// <summary>
        /// <para type="description">
        /// Selects job type Copy.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DoCopy)]
        public SwitchParameter Copy { get; set; }

        /// <summary>
        /// <para type="description">
        /// The source table to copy from.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.DoCopy)]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.DoExtract)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Table),
            Property = nameof(Table.TableReference))]
        [PropertyByTypeTransformation(TypeToTransform = typeof(TableList.TablesData),
            Property = nameof(TableList.TablesData.TableReference))]
        [ValidateNotNull]
        public TableReference Source { get; set; }

        /// <summary>
        /// <para type="description">
        /// The destination table to write to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.DoCopy)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoQuery)]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.DoLoad)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Table),
            Property = nameof(Table.TableReference))]
        [PropertyByTypeTransformation(TypeToTransform = typeof(TableList.TablesData),
            Property = nameof(TableList.TablesData.TableReference))]
        public TableReference Destination { get; set; }

        /// <summary>
        /// <para type="description">
        /// Write Disposition of the operation. Handles what happens if the destination table 
        /// already exists. If this parameter is not supplied, this defaults to WriteEmpty.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoCopy)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoLoad)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoQuery)]
        public WriteDisposition? WriteMode { get; set; }

        // Load Parameters.

        /// <summary>
        /// <para type="description">
        /// Selects job type Load.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DoLoad)]
        public SwitchParameter Load { get; set; }

        /// <summary>
        /// <para type="description">
        /// The format to input/output (CSV, JSON, AVRO, DATASTORE_BACKUP).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.DoLoad)]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.DoExtract)]
        public DataFormats Type { get; set; }

        /// <summary>
        /// <para type="description">
        /// A list of fully-qualified Google Cloud Storage URIs where data should be imported from.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.DoLoad)]
        [ValidateNotNullOrEmpty]
        public string[] SourceUris { get; set; }

        /// <summary>
        /// <para type="description">
        /// The character encoding of the data. The supported values are "UTF-8" (default) or "ISO-8859-1".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoLoad)]
        public string Encoding { get; set; } = "UTF-8";

        /// <summary>
        /// <para type="description">
        /// Delimiter to use between fields in the exported data. Default value is comma (,).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoLoad)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoExtract)]
        public string FieldDelimiter { get; set; }

        /// <summary>
        /// <para type="description">
        /// The value that is used to quote data sections in a CSV file. Default value is double-quote (").
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoLoad)]
        public string Quote { get; set; }

        /// <summary>
        /// <para type="description">
        /// The number of rows to skip from the input file. (Usually used for headers.)
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoLoad)]
        public int? SkipLeadingRows { get; set; }

        /// <summary>
        /// <para type="description">
        /// Allows insertion of rows with fields that are not in the schema, ignoring the extra fields.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoLoad)]
        public SwitchParameter AllowUnknownFields { get; set; }

        /// <summary>
        /// <para type="description">
        /// Allows insertion of rows that are missing trailing optional columns.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoLoad)]
        public SwitchParameter AllowJaggedRows { get; set; }

        /// <summary>
        /// <para type="description">
        /// Allows quoted data sections to contain newlines
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoLoad)]
        public SwitchParameter AllowQuotedNewlines { get; set; }

        /// <summary>
        /// <para type="description">
        /// The maximum number of bad records that BigQuery can ignore while
        /// running the job. If the number of bad records exceeds this value,
        /// an invalid error is returned in the job result.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoLoad)]
        [ValidateRange(0, Int32.MaxValue)]
        public int? MaxBadRecords { get; set; }

        // Extract Parameters.

        /// <summary>
        /// <para type="description">
        /// Selects job type Extract.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DoExtract)]
        public SwitchParameter Extract { get; set; }

        /// <summary>
        /// <para type="description">
        /// A list of fully-qualified Google Cloud Storage URIs where the extracted table should be written.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.DoExtract)]
        [ValidateNotNullOrEmpty]
        public string[] DestinationUris { get; set; }

        /// <summary>
        /// <para type="description">
        /// Instructs the server to output with GZIP compression. Otherwise, no compression is used.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoExtract)]
        public SwitchParameter Compress { get; set; }

        /// <summary>
        /// <para type="description">
        /// Disables printing of a header row in the results. Otherwise, a header will be printed.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DoExtract)]
        public SwitchParameter NoHeader { get; set; }

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

            // Stop in case of -WhatIf.
            if (result == null) { return; }

            // Check if the user is requesting a synchronous operation.
            if (PollUntilComplete)
            {
                result = PollForCompletion(result);
                result = Client.GetJob(result.JobReference).Resource;
            }

            // Check for error conditions before writing result.
            if (result.Status.ErrorResult != null)
            {
                var e = new Exception($"Reason: {result.Status.ErrorResult.Reason}, " +
                    $"Message: {result.Status.ErrorResult.Message}");
                ThrowTerminatingError(new ErrorRecord(e, "Job Error", ErrorCategory.OperationStopped, result));
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
                    var options = new QueryOptions
                    {
                        UseLegacySql = UseLegacySql,
                        Priority = Priority,
                        DestinationTable = Destination,
                        DefaultDataset = DefaultDataset,
                        WriteDisposition = WriteMode
                    };

                    BigQueryJob bqr = Client.CreateQueryJob(QueryString, null, options);
                    return bqr?.Resource;
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
                {
                    ThrowTerminatingError(new ErrorRecord(ex, "Query rejected: Access denied",
                        ErrorCategory.InvalidOperation, this));
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    ThrowTerminatingError(new ErrorRecord(ex, "Query rejected: Resource not found",
                        ErrorCategory.InvalidOperation, this));
                }
            }
            return null;
        }

        /// <summary>
        /// Copy Job main processing function. 
        /// *This is written using Apis.BigQuery.v2 becuase Cloud.BigQuery did not 
        /// support Copy opertations at the time of writing.
        /// </summary>
        public Apis.Bigquery.v2.Data.Job DoCopy()
        {
            if (ShouldProcess($"Copying {Source.TableId} to {Destination.TableId}"))
            {
                try
                {
                    var copyjob = new Apis.Bigquery.v2.Data.Job
                    {
                        Configuration = new JobConfiguration
                        {
                            Copy = new JobConfigurationTableCopy
                            {
                                DestinationTable = Destination,
                                SourceTable = Source,
                                WriteDisposition = WriteMode?.ToString() ?? WriteDisposition.WriteIfEmpty.ToString()
                            }
                        }
                    };
                    return Service.Jobs.Insert(copyjob, Project).Execute();
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
                {
                    ThrowTerminatingError(new ErrorRecord(ex, "Copy failed: Access denied",
                        ErrorCategory.InvalidOperation, this));
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    ThrowTerminatingError(new ErrorRecord(ex, "Copy failed: Resource not found",
                        ErrorCategory.InvalidOperation, this));
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
                {
                    ThrowTerminatingError(new ErrorRecord(ex, "Copy failed: Duplicate resource",
                        ErrorCategory.InvalidOperation, this));
                }
            }
            return null;
        }

        /// <summary>
        /// Load Job main processing function.
        /// *This is written using Apis.BigQuery.v2 becuase Cloud.BigQuery did not 
        /// support Load opertations at the time of writing.
        /// </summary>
        public Apis.Bigquery.v2.Data.Job DoLoad()
        {
            if (ShouldProcess($"{Destination.TableId} (LOAD)"))
            {
                try
                {
                    var loadjob = new Apis.Bigquery.v2.Data.Job
                    {
                        Configuration = new JobConfiguration
                        {
                            Load = new JobConfigurationLoad
                            {
                                DestinationTable = Destination,
                                SourceFormat = (Type == DataFormats.JSON) ? JSON_TEXT : Type.ToString(),
                                SourceUris = SourceUris,
                                Encoding = Encoding,
                                FieldDelimiter = FieldDelimiter,
                                Quote = Quote,
                                SkipLeadingRows = SkipLeadingRows,
                                IgnoreUnknownValues = AllowUnknownFields,
                                AllowJaggedRows = AllowJaggedRows,
                                AllowQuotedNewlines = AllowQuotedNewlines,
                                WriteDisposition = WriteMode.ToString(),
                                MaxBadRecords = MaxBadRecords ?? 0
                            }
                        }
                    };
                    return Service.Jobs.Insert(loadjob, Project).Execute();
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
                {
                    ThrowTerminatingError(new ErrorRecord(ex, "Load failed: Access denied",
                        ErrorCategory.InvalidOperation, this));
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    ThrowTerminatingError(new ErrorRecord(ex, "Load failed: Resource not found",
                        ErrorCategory.InvalidOperation, this));
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
                {
                    ThrowTerminatingError(new ErrorRecord(ex, "Load failed: Duplicate resource",
                        ErrorCategory.InvalidOperation, this));
                }
            }
            return null;
        }

        /// <summary>
        /// Extract Job main processing function.
        /// *This is written using Apis.BigQuery.v2 becuase Cloud.BigQuery did not 
        /// support Extract opertations at the time of writing.
        /// </summary>
        public Apis.Bigquery.v2.Data.Job DoExtract()
        {
            if (ShouldProcess($"{Source.TableId} (EXTRACT)"))
            {
                try
                {
                    var extractjob = new Apis.Bigquery.v2.Data.Job
                    {
                        Configuration = new JobConfiguration
                        { 
                            Extract = new JobConfigurationExtract
                            {
                                SourceTable = Source,
                                DestinationUris = DestinationUris,
                                DestinationFormat = (Type == DataFormats.JSON) ? JSON_TEXT : Type.ToString(),
                                Compression = (Compress) ? COMPRESSION_GZIP : COMPRESSION_NONE,
                                PrintHeader = !NoHeader,
                                FieldDelimiter = FieldDelimiter
                            }
                        }
                    };
                    return Service.Jobs.Insert(extractjob, Project).Execute();
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
                {
                    ThrowTerminatingError(new ErrorRecord(ex, "Extract failed: Access denied",
                        ErrorCategory.InvalidOperation, this));
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    ThrowTerminatingError(new ErrorRecord(ex, "Extract failed: Resource not found",
                        ErrorCategory.InvalidOperation, this));
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
                {
                    ThrowTerminatingError(new ErrorRecord(ex, "Extract failed: Duplicate resource",
                        ErrorCategory.InvalidOperation, this));
                }
            }
            return null;
        }

        /// <summary>
        /// This function waits for a job to reach the "DONE" status and then returns.
        /// </summary>
        /// <param name="job">Job to poll for completion.</param>
        public Apis.Bigquery.v2.Data.Job PollForCompletion(Apis.Bigquery.v2.Data.Job job)
        {
            while (!job.Status.State.Equals(STATUS_DONE))
            {
                // Poll every 250 ms, or 4 times/sec.
                System.Threading.Thread.Sleep(250);
                try
                {
                    job = Service.Jobs.Get(job.JobReference.ProjectId, job.JobReference.JobId).Execute();
                }
                catch (Exception ex)
                {
                    ThrowTerminatingError(new ErrorRecord(ex,
                        "Polling for status was interrupted.",
                        ErrorCategory.InvalidOperation, this));
                    return null;
                }
            }
            return job;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Returns the result of a completed BQ Job.
    /// </para>
    /// <para type="description">
    /// Returns the result of a completed BQ Job. Requires the "Reader" dataset role. You can specify 
    /// how long the call should wait for the query to be completed, if it is not already finished. 
    /// This is done with the "-Timeout" parameter. An integer number of seconds is taken, and the 
    /// default is 10. This cmdlet returns BigQueryRow objects.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $job = Start-BqJob -Query "select * from book_data.classics"
    /// PS C:\> $job | Receive-BqJob -Timeout 60
    ///   </code>
    ///   <para>This will run a query in the book_data.classics table and will wait up to 60 seconds 
    ///   for its completion. When it finishes, it will print a number of BigQueryRow objects to 
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
            // Set Project for the lazy instantiation of a BQ Client object.
            Project = InputObject.ProjectId;

            var options = new GetQueryResultsOptions {
                Timeout = new TimeSpan(0, 0, (Timeout < 10) ? 10 : Timeout)
            };

            try
            {
                BigQueryResults result = Client.GetQueryResults(InputObject, options);
                if (result == null)
                {
                    throw new Exception("Server response came back as null.");
                }
                WriteObject(result, true);
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex, "Failed to receive results.",
                        ErrorCategory.InvalidOperation, this));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Requests that a running BigQuery Job be canceled.
    /// </para>
    /// <para type="description">
    /// Requests that a job be canceled. This call will return immediately, and the client 
    /// is responsible for polling for job status. Canceled jobs may still incur costs. 
    /// This cmdlet returns a Job object if successful.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $job = Start-BqJob -Query "SELECT * FROM book_data.classics"
    /// PS C:\> $job = $job | Stop-BqJob
    ///   </code>
    ///   <para>This will send a request to stop $job as soon as possible. "$job.Status.State" 
    ///   should now be "DONE", but there is a chance that the user will have to continue to
    ///   poll for status with Get-BqJob.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/jobs)">
    /// [BigQuery Jobs]
    /// </para>
    /// </summary>
    [Cmdlet("Stop", "BqJob")]
    public class StopBqJob : BqCmdlet
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

        protected override void ProcessRecord()
        {
            // Set Project for the lazy instantiation of a BQ Client object.
            Project = InputObject.ProjectId;

            try
            {
                // No options currently available, but class was added for future possibilities.
                BigQueryJob result = Client.CancelJob(InputObject, new CancelJobOptions());
                
                if (result == null)
                {
                    throw new Exception("Server response came back as null.");
                }

                WriteObject(result.Resource);
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex, "Failed to cancel job.",
                        ErrorCategory.InvalidOperation, this));
            }
        }
    }
}
