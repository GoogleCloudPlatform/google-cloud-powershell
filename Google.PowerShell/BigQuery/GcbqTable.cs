// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;
using Google.PowerShell.Common;
using System;
using System.Net;
using System.Management.Automation;

namespace Google.PowerShell.BigQuery
{
    /// <summary>
    /// <para type="synopsis">
    /// Lists all tables in the specified dataset or returns a specific table.
    /// </para>
    /// <para type="description">
    /// Lists all tables in the specified dataset. Requires the READER dataset role. If a table ID is specified, 
    /// it will return the table resource, which describes the data in the table. Note that this is not the 
    /// actual data from the table. If no Project is specified, the default project will be used. The dataset 
    /// may be selected by passing in a Dataset object via pipeline or by passing the dataset ID with the 
    /// -Dataset parameter. This cmdlet returns a TableList object if no Table was specified, and a Table otherwise.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset “my_data” | Get-GcbqTable</code>
    ///   <para>This will list all of the tables in the dataset "my_data" in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset “my_data” | Get-GcbqTable "my_table"</code>
    ///   <para>This will return a Table descriptor object for "my_table" in "my_data".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcbqTable -Project "my_proj" -Dataset "my_data" "my_table"</code>
    ///   <para>This returns a Table descriptor object for this project:dataset:table combination.</para>
    /// </example> 
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcbqTable")]
    public class GetGcbqTable : GcbqCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to look for datasets in. If not set via PowerShell parameter processing, it will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        override public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the dataset that you want to get a descriptor object for.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidatePattern("[a-zA-Z0-9_]")]
        public string Dataset { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the dataset that you want to get a descriptor object for. 
        /// (This will be used if both InputObject and Dataset are provided)
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ValueFromPipeline = true)]
        public Dataset InputObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the dataset that you want to get a descriptor object for.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0)]
        public string Table { get; set; }

        protected override void ProcessRecord()
        {
            // Select the DatasetID. Favor InputObject if both present. Default if neither present.
            if (InputObject == null && Dataset == null)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new Exception("No Dataset Specified"),
                    "No Dataset Specified",
                    ErrorCategory.InvalidArgument,
                    InputObject));
                return;
            }
            Dataset = (InputObject != null) ? InputObject.DatasetReference.DatasetId : Dataset;

            // Create and execute request.
            if (Table == null)
            {
                TablesResource.ListRequest request = new TablesResource.ListRequest(Service, Project, Dataset);
                var response = request.Execute();
                if (response != null)
                {
                    WriteObject(response, true);
                }
                else
                {
                    WriteError(new ErrorRecord(
                        new Exception("400"),
                        "Error 400: List request to server failed.",
                        ErrorCategory.InvalidArgument,
                        Dataset));
                }
            }
            else
            {
                TablesResource.GetRequest request = new TablesResource.GetRequest(Service, Project, Dataset, Table);
                try
                {
                    var response = request.Execute();
                    WriteObject(response);
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    WriteError(new ErrorRecord(ex,
                        $"Error 404: Table {Table} not found in {Dataset}.",
                        ErrorCategory.ObjectNotFound,
                        Table));
                }
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Updates information describing an existing BigQuery table.
    /// </para>
    /// <para type="description">
    /// text
    /// </para>
    /// <example>
    ///   <code>PS C:\> Set-GcbqTable</code>
    ///   <para>This does a thing</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GcbqTable")]
    public class SetGcbqTable : GcbqCmdlet
    {
        protected override void ProcessRecord()
        {
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a new empty table in the specified project and dataset.
    /// </para>
    /// <para type="description">
    /// text
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GcbqTable</code>
    ///   <para>This does a thing</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcbqTable")]
    public class NewGcbqTable : GcbqCmdlet
    {
        protected override void ProcessRecord()
        {
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes the specified table.
    /// </para>
    /// <para type="description">
    /// text
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcbqTable</code>
    ///   <para>This does a thing</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcbqTable", SupportsShouldProcess = true)]
    public class RemoveGcbqTable : GcbqCmdlet
    {
        protected override void ProcessRecord()
        {
        }
    }
}
