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
    /// Lists all tables in the specified dataset or finds a specific table by name.
    /// </para>
    /// <para type="description">
    /// Lists all tables in the specified dataset if no Table ID is specified. Requires the READER dataset 
    /// role. If a table ID is specified, it will return the table resource, which describes the data in 
    /// the table. Note that this is not the actual data from the table. If no Project is specified, the 
    /// default project will be used. The -Dataset parameter takes a string or a Dataset object, and will 
    /// extract the DatasetId.  This can be passed via parameter or on the pipeline. This cmdlet returns 
    /// a single Table if a table ID is specified, and any number of Tables otherwise.
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
        /// The project to look for tables in. If not set via PowerShell parameter processing, it will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        override public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the dataset to search.  Can be a string or a dataset object.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [ValidatePattern("[a-zA-Z0-9_]")]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Dataset), Property = nameof(Apis.Bigquery.v2.Data.Dataset.DatasetReference))]
        [PropertyByTypeTransformation(TypeToTransform = typeof(DatasetReference), Property = nameof(DatasetReference.DatasetId))]
        public string Dataset { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the table that you want to get a descriptor object for.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0)]
        public string Table { get; set; }

        protected override void ProcessRecord()
        {
            if (Table == null)
            {
                TablesResource.ListRequest request = Service.Tables.List(Project, Dataset);
                do
                {
                    TableList response = request.Execute();
                    if (response == null)
                    {
                        WriteError(new ErrorRecord(
                            new Exception("The List query returned null instead of a well formed list."),
                            "Null List Returned", ErrorCategory.ReadError, Dataset));
                    }
                    if (response.Tables != null)
                    {
                        WriteObject(response.Tables, true);
                    }
                    request.PageToken = response.NextPageToken;
                }
                while (!Stopping && request.PageToken != null);
            }
            else
            {
                TablesResource.GetRequest request = Service.Tables.Get(Project, Dataset, Table);
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
}
