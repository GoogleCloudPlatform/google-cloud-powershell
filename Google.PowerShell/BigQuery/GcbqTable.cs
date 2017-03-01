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
        /// The ID of the dataset to search. Can be a string, a Dataset, a DatasetReference, or a DatasetsData object.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [ValidatePattern("[a-zA-Z0-9_]")]
        [PropertyByTypeTransformation(TypeToTransform = typeof(DatasetList.DatasetsData), 
            Property = nameof(DatasetList.DatasetsData.DatasetReference))]
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
                        $"Error {ex.HttpStatusCode}: Table {Table} not found in {Dataset}.",
                        ErrorCategory.ObjectNotFound,
                        Table));
                }
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a new empty table in the specified project and dataset.
    /// </para>
    /// <para type="description">
    /// Creates a new empty table in the specified dataset. A Table can be supplied by object 
    /// via the pipeline or the -InputObject parameter, or it can be instantiated by value 
    /// with the flags below. The Dataset ID can be specified by passing in a string to 
    /// -DatasetId, or you can pass a Dataset or DatasetReference to the -Dataset parameter. 
    /// If no Project is specified, the default project will be used. This cmdlet returns 
    /// a Table object.
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GcbqTable “new_tab” -Dataset “my_data” -Description “Some nice data!” 
    ///         -Expiration (60*60*24*30)</code>
    ///   <para>This makes a new Table called "new_tab" with a lifetime of 30 days.</para>
    ///   <code>PS C:\> Get-GcbqDataset "my_data" | New-GcbqTable “new_tab”</code>
    ///   <para>This shows how the pipeline can be used to specify Dataset and Project.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcbqTable")]
    public class NewGcbqTable : GcbqCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByObject = "ByObject";
            public const string ByValues = "ByValue";
            public const string ByValueWithRef = "ByValueWithRef";
        }

        /// <summary>
        /// <para type="description">
        /// The Table object that will be sent to the server to be inserted.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ByObject)]
        public Table InputObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// The project to put the table in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValues)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DatasetId that you would like to add to. This field takes strings.
        /// To pass in a Dataset or DatasetId object for this field, use the ByValuesWithRef parameter set.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ByValues)]
        public string DatasetId { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Dataset that you would like to add to. This field takes Dataset or DatasetRefrence objects.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ByValueWithRef)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Dataset), Property = nameof(Apis.Bigquery.v2.Data.Dataset.DatasetReference))]
        public DatasetReference Dataset { get; set; }

        /// <summary>
        /// <para type="description">
        /// The TableId must be unique within the Dataset and match the pattern [a-zA-Z0-9_]+.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.ByValueWithRef)]
        [ValidatePattern("[a-zA-Z0-9_]")]
        [ValidateLength(1, 1024)]
        public string TableId { get; set; }

        /// <summary>
        /// <para type="description">
        /// User-friendly name for the table.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValueWithRef)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// Description of the table.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValueWithRef)]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The lifetime of this table from the time of creation (in seconds).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValues)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValueWithRef)]
        [ValidateRange(1, (long.MaxValue / 1000))]
        public long Expiration { get; set; }

        protected override void ProcessRecord()
        {
            // Set up the Dataset based on parameters
            TablesResource.InsertRequest request;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByObject:
                    request = Service.Tables.Insert(InputObject,
                        InputObject.TableReference.ProjectId,
                        InputObject.TableReference.DatasetId);
                    break;
                case ParameterSetNames.ByValueWithRef:
                    Project = Dataset.ProjectId;
                    DatasetId = Dataset.DatasetId;
                    request = makeInsertReq();
                    break;
                case ParameterSetNames.ByValues:
                    request = makeInsertReq();
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            // Add the new dataset to the project supplied.
            try
            {
                Table response = request.Execute();
                WriteObject(response);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
            {
                WriteError(new ErrorRecord(ex,
                    $"A table with the name {TableId} already exists in {Project}:{DatasetId}.",
                    ErrorCategory.InvalidArgument,
                    TableId));
            }
        }

        public TablesResource.InsertRequest makeInsertReq()
        {
            Table newTable = new Table();
            newTable.TableReference = new TableReference
            {
                ProjectId = Project,
                DatasetId = DatasetId,
                TableId = TableId
            };
            newTable.FriendlyName = Name;
            newTable.Description = Description;
            if (Expiration != 0)
            {
                long currentMillis = Convert.ToInt64(DateTime.Now.ToUniversalTime().Subtract(
                    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    ).TotalMilliseconds);
                newTable.ExpirationTime = (Expiration * 1000) + currentMillis;
                // Note: The code below is a more elegant solution, but is not supported by the current version
                //newTable.ExpirationTime = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            }
            return Service.Tables.Insert(newTable, Project, DatasetId);
        }
    }
}
