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
    /// If no table is specified, lists all tables in the specified dataset (Requires the "READER" 
    /// dataset role). If a table is specified, it will return the table resource. Note that 
    /// this is not the actual data from the table. If no Project is specified, the default 
    /// project will be used. Dataset can be specified by the "-DatasetId" parameter or by 
    /// passing in a Dataset object. This cmdlet returns a single Table if a table ID is 
    /// specified, and any number of TableList.TablesData objects otherwise.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-BqDataset "my_data" | Get-BqTable</code>
    ///   <para>This will list all of the tables in the dataset "my_data" in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-BqDataset "my_data" | Get-BqTable "my_table"</code>
    ///   <para>This will return a Table descriptor object for "my_table" in "my_data".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-BqTable "my_table" -Project "my_proj" -Dataset "my_data"</code>
    ///   <para>This returns a Table descriptor object for this project:dataset:table combination.</para>
    /// </example> 
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "BqTable")]
    public class GetBqTable : BqCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByValue = "ByValue";
            public const string ByDatasetObject = "ByDatasetObject";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project to look for tables in. If not set via PowerShell parameter processing, it will
        /// default to the Cloud SDK's default project.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValue)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the table that you want to get a descriptor object for.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0, ParameterSetName = ParameterSetNames.ByValue)]
        [Parameter(Mandatory = false, Position = 0, ParameterSetName = ParameterSetNames.ByDatasetObject)]
        public string Table { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the dataset to search. Can be a string, a Dataset, a DatasetReference, or a DatasetsData object.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByValue)]
        [ValidatePattern("[a-zA-Z0-9_]")]
        public string DatasetId { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Dataset that you would like to search. This field takes Dataset or DatasetRefrence objects.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ByDatasetObject)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Dataset), 
            Property = nameof(Apis.Bigquery.v2.Data.Dataset.DatasetReference))]
        public DatasetReference Dataset { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Table object to get a reference for.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0, ParameterSetName = ParameterSetNames.ByObject)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Table),
            Property = nameof(Apis.Bigquery.v2.Data.Table.TableReference))]
        [PropertyByTypeTransformation(TypeToTransform = typeof(TableList.TablesData),
            Property = nameof(TableList.TablesData.TableReference))]
        public TableReference InputObject { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                // No processing needed for ByValue parameter sets.
                case ParameterSetNames.ByValue:
                    break;
                case ParameterSetNames.ByObject:
                    Project = InputObject.ProjectId;
                    DatasetId = InputObject.DatasetId;
                    Table = InputObject.TableId;
                    break;
                case ParameterSetNames.ByDatasetObject:
                    Project = Dataset.ProjectId;
                    DatasetId = Dataset.DatasetId;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            if (Table == null && InputObject == null)
            {
                DoListRequest(Service.Tables.List(Project, DatasetId));
            }
            else
            {
                DoGetRequest(Service.Tables.Get(Project, DatasetId, Table));
            }
        }

        public void DoListRequest(TablesResource.ListRequest request)
        {
            do
            {
                TableList response = request.Execute();
                if (response == null)
                {
                    ThrowTerminatingError(new ErrorRecord(
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

        public void DoGetRequest(TablesResource.GetRequest request)
        {
            try
            {
                var response = request.Execute();
                WriteObject(response);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"Error {ex.HttpStatusCode}: Table '{Table}' not found in '{Dataset}'.",
                    ErrorCategory.ObjectNotFound,
                    Table));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Updates information describing an existing BigQuery table.
    /// </para>
    /// <para type="description">
    /// Updates information in an existing table. Pass in the updated Table object via the 
    /// pipeline or the "-InputObject" parameter. This cmdlet returns the updated Table object.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $my_tab = Get-BqTable "my_table" -DatasetId "my_data" 
    /// PS C:\> $my_tab.Description = "Some new description!"
    /// PS C:\> $my_tab | Set-BqTable
    ///   </code>
    ///   <para>This is an example of how to locally update a field within a table and then 
    ///   push your changes to the cloud resource</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "BqTable")]
    public class SetBqTable : BqCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The updated Table object. Must have the same tableId as an existing table in the dataset.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        public Table InputObject { get; set; }

        protected override void ProcessRecord()
        {
            Table response;
            bool needToInsert = false;
            var request = Service.Tables.Update(InputObject,
                InputObject.TableReference.ProjectId,
                InputObject.TableReference.DatasetId,
                InputObject.TableReference.TableId);
            try
            {
                response = request.Execute();
                WriteObject(response);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"Conflict while updating '{InputObject.TableReference.DatasetId}'.",
                    ErrorCategory.WriteError,
                    InputObject));
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"You do not have permission to modify '{InputObject.TableReference.DatasetId}'.",
                    ErrorCategory.PermissionDenied,
                    InputObject));
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                if (!ex.Message.Contains(TAB_404))
                {
                    ThrowTerminatingError(new ErrorRecord(ex, 
                        $"Dataset '{InputObject.TableReference.ProjectId}:{InputObject.TableReference.DatasetId}' not found.",
                        ErrorCategory.ObjectNotFound, Project));
                }
                needToInsert = true;
            }

            if (needToInsert)
            {
                // Turn a Set- into a New- in the case of a 404 on the object to set.
                TablesResource.InsertRequest insertRequest = Service.Tables.Insert(InputObject,
                    InputObject.TableReference.ProjectId, InputObject.TableReference.DatasetId);
                try
                {
                    var insertResponse = insertRequest.Execute();
                    WriteObject(insertResponse);
                }
                catch (Exception e2)
                {
                    ThrowTerminatingError(new ErrorRecord(e2,
                        $"Table was not found and an error occured while creating a new Table.",
                        ErrorCategory.NotSpecified, InputObject));
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
    /// via the pipeline or the "-InputObject" parameter, or it can be instantiated by value 
    /// with the flags below. The Dataset ID can be specified by passing in a string to 
    /// "-DatasetId", or you can pass a Dataset or DatasetReference to the "-Dataset" parameter. 
    /// Schemas can be set by passing in a TableSchema object with the "-Schema" flag. If no 
    /// Project is specified, the default project will be used. This cmdlet returns a Table object.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> New-BqTable "new_tab" 
    ///                     -Dataset "my_data" 
    ///                     -Description "Some nice data!" 
    ///                     -Expiration (60*60*24*30)
    ///   </code>
    ///   <para>This makes a new Table called "new_tab" with a lifetime of 30 days.</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> Get-BqDataset "my_data" | New-BqTable "new_tab"
    ///   </code>
    ///   <para>This shows how the pipeline can be used to specify Dataset and Project.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "BqTable")]
    public class NewBqTable : BqCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByObject = "ByObject";
            public const string ByValue = "ByValue";
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
        /// default to the Cloud SDK's default project.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValue)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DatasetId that you would like to add to. This field takes strings.
        /// To pass in an object to specify datasetId, use the Dataset parameter.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ByValue)]
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
        /// The TableId must be unique within the Dataset and match the pattern "[a-zA-Z0-9_]+".
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.ByValue)]
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.ByValueWithRef)]
        [ValidatePattern("[a-zA-Z0-9_]")]
        [ValidateLength(1, 1024)]
        public string TableId { get; set; }

        /// <summary>
        /// <para type="description">
        /// User-friendly name for the table.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValue)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValueWithRef)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// Description of the table.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValue)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValueWithRef)]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The lifetime of this table from the time of creation (in seconds).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValue)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValueWithRef)]
        [ValidateRange(1, (long.MaxValue / 1000))]
        public long Expiration { get; set; }

        /// <summary>
        /// <para type="description">
        /// Schema of the new table. Created by the New-BqSchema and Set-BqSchema cmdlets.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValue)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValueWithRef)]
        public TableSchema Schema { get; set; }

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
                case ParameterSetNames.ByValue:
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
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"A table with the name '{TableId}' already exists in '{Project}:{DatasetId}'.",
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
            newTable.Schema = Schema;
            if (Expiration != 0)
            {
                long currentMillis = Convert.ToInt64(DateTime.Now.ToUniversalTime().Subtract(
                    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    ).TotalMilliseconds);
                newTable.ExpirationTime = (Expiration * 1000) + currentMillis;
            }
            return Service.Tables.Insert(newTable, Project, DatasetId);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes the specified table.
    /// </para>
    /// <para type="description">
    /// Deletes the specified table from the dataset. The table to be deleted should be passed 
    /// in via the pipeline or identified by DatasetId and TableId. If the table contains data, 
    /// this operation will prompt the user for confirmation before any deletions are performed. 
    /// To delete a non-empty table automatically, use the "-Force" parameter. If no Project is 
    /// specified, the default project will be used. This cmdlet returns a Table object. 
    /// This cmdlet supports the ShouldProcess function, so it has the corresponding "-WhatIf" 
    /// and "-Confirm" flags.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $table = Get-BqTable "my_table" -Dataset "my_dataset"
    /// PS C:\> $table | Remove-BqTable
    ///   </code>
    ///   <para>This will remove "my_table" if it is empty, and will prompt for user confirmation 
    ///   if it is not. All data in "my_table" will be deleted if the user accepts.</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> Remove-BqTable "my_table" -DatasetId "my_dataset" -Force
    ///   </code>
    ///   <para>This will remove "my_table" and all of its data.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "BqTable", SupportsShouldProcess = true)]
    
    public class RemoveBqTable : BqCmdlet
    {
        private bool yesToAll = false;
        private bool noToAll = false;
        private class ParameterSetNames
        {
            public const string ByValue = "ByValue";
            public const string ByDatasetObject = "ByDatasetObject";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project to look for tables in. If not set via PowerShell parameter processing, it will
        /// default to the Cloud SDK's default project.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValue)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the table that you want to remove.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByValue)]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByDatasetObject)]
        public string TableId { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the dataset to search. This dataset should contain the table you wish to remove.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByValue)]
        [ValidatePattern("[a-zA-Z0-9_]")]
        public string DatasetId { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Dataset that you would like to search. This field takes Dataset or DatasetRefrence objects.
        /// This dataset should contain the table you wish to remove.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ByDatasetObject)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Dataset),
            Property = nameof(Apis.Bigquery.v2.Data.Dataset.DatasetReference))]
        public DatasetReference Dataset { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Table object that will be sent to the server to be removed. 
        /// Also takes TableReference and TableList.TablesData objects.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0, ParameterSetName = ParameterSetNames.ByObject)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(TableList.TablesData),
            Property = nameof(TableList.TablesData.TableReference))]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Table),
            Property = nameof(Apis.Bigquery.v2.Data.Table.TableReference))]
        public TableReference Table { get; set; }

        /// <summary>
        /// <para type="description">
        /// Forces deletion of non-empty tables and the data contained in them.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByValue:
                    // No processing needed for ByValue parameter sets.
                    break;
                case ParameterSetNames.ByObject:
                    Project = Table.ProjectId;
                    DatasetId = Table.DatasetId;
                    TableId = Table.TableId;
                    break;
                case ParameterSetNames.ByDatasetObject:
                    Project = Dataset.ProjectId;
                    DatasetId = Dataset.DatasetId;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            try
            {
                if (ShouldDelete())
                {
                    Service.Tables.Delete(Project, DatasetId, TableId).Execute();
                }
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"Error {ex.HttpStatusCode}: '{Table}' not found in '{Dataset}'.",
                    ErrorCategory.ObjectNotFound, Table));
            }
        }

        private bool ShouldDelete()
        {
            if (!ShouldProcess(TableId) || noToAll)
            {
                return false;
            }
            else if (Force || yesToAll)
            {
                return true;
            }
            else
            {
                var tableResponse = new TablesResource.GetRequest(
                    Service, Project, DatasetId, TableId).Execute();

                return (tableResponse.NumRows == 0) || ShouldContinue(
                    GetConfirmationMessage(TableId, tableResponse.NumRows),
                    "Confirm Deletion", ref yesToAll, ref noToAll);
            }
        }

        private string GetConfirmationMessage(string tableId, ulong? rows)
        {
            return $@"'{tableId}' has {rows} rows and the -Force parameter was not specified. "
                + "If you continue, all data will be deleted with the table. Are you sure you want to continue?";
        }
    }
}
