// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;
using Google.PowerShell.Common;
using System;
using System.Management.Automation;

namespace Google.PowerShell.BigQuery
{
    /// <summary>
    /// <para type="synopsis">
    /// Lists BigQuery datasets in a specific Cloud project.
    /// </para>
    /// <para type="description">
    /// If a DatasetId is specified, it will return an object describing that dataset. If no DatasetId is 
    /// specified, this cmdlet lists all datasets in the specified project to which you have been granted the 
    /// READER dataset role. The -All flag will include hidden datasets in the search results. The -Filter 
    /// flag allows you to filter results by label. The syntax to filter is "labels.name[:value]". Multiple filters 
    /// can be ANDed together by connecting with a space. See the link below for more information.
    /// If no Project is specified, the default project will be used. This cmdlet returns a DatasetList if 
    /// no DatasetId was specified, and a Dataset otherwise.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset "my-dataset"</code>
    ///   <para>This returns a Dataset object from the default project of the dataset with id <code>my-dataset</code>.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset -Project my-project</code>
    ///   <para>This lists all of the non-hidden datasets in the Cloud project <code>my-project</code>.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset -All</code>
    ///   <para>This lists all of the datasets in the default Cloud project for your account.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset -All -Filter "labels.department:shipping"</code>
    ///   <para>This lists all of the datasets in the default Cloud project for your account that have 
    ///   the <code>department</code> key with a value <code>shipping</code>.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/datasets)">
    /// [BigQuery Datasets]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/labeling-datasets#filtering_datasets_using_labels)">
    /// [Filtering datasets using labels]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcbqDataset")]
    public class GetGcbqDataset : GcbqCmdlet
    {
        private class ParameterSetNames
        {
            public const string List = "List";
            public const string Get = "Get";
        }

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
        /// Includes hidden datasets in the output if set.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.List)]
        public SwitchParameter All { get; set; }

        /// <summary>
        /// <para type="description">
        /// Filters results by label. The syntax is "labels./<name/>[:/<value/>]". Multiple filters can 
        /// be ANDed together by a space.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.List)]
        public string Filter { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the dataset that you want to get a descriptor object for.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.Get)]
        public string Dataset { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.List:
                    DatasetsResource.ListRequest lRequest = new DatasetsResource.ListRequest(Service, Project);
                    lRequest.All = All;
                    lRequest.Filter = Filter;
                    var lResponse = lRequest.Execute();
                    if (lResponse != null)
                    {
                        WriteObject(lResponse, true);
                    }
                    else
                    {
                        WriteError(new ErrorRecord(
                            new Exception("400"), 
                            "List request to server failed.",
                            ErrorCategory.InvalidArgument, 
                            Project));
                    }
                    break;
                case ParameterSetNames.Get:
                    DatasetsResource.GetRequest gRequest = new DatasetsResource.GetRequest(Service, Project, Dataset);
                    var gResponse = gRequest.Execute();
                    if (gResponse != null)
                    {
                        WriteObject(gResponse);
                    }
                    else
                    {
                        WriteError(new ErrorRecord(
                            new Exception("400"), 
                            "Get request to server failed.",
                            ErrorCategory.InvalidArgument, 
                            Dataset));
                    }
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Updates information in an existing dataset.
    /// </para>
    /// <para type="description">
    /// Updates information in an existing dataset. If no Project is specified, the default project will be used. 
    /// This cmdlet returns a Dataset object.
    /// </para>
    /// <example>
    ///   <code>PS C:\> $updatedSet | Set-GcbqDataset</code>
    ///   <para>This will update the values stored in the cloud for the dataset passed via pipeline.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Set-GcbqDataset -Project my_project -ByObject $modifedSet</code>
    ///   <para>This overwrites my_project:my_data_id with the dataset from $modifiedSet.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/datasets)">
    /// [BigQuery Datasets]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GcbqDataset")]
    public class SetGcbqDataset : GcbqCmdlet
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
        /// The updated Dataset object.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        public Dataset ByObject { get; set; }

        protected override void ProcessRecord()
        {
            Dataset response;
            var request = new DatasetsResource.UpdateRequest(Service, ByObject, Project, ByObject.DatasetReference.DatasetId);
            response = request.Execute();
            
            if (response != null)
            {
                WriteObject(response);
            }
            else
            {
                WriteError(new ErrorRecord(
                    new Exception("400"),
                    $"Set request for {ByObject.DatasetReference.DatasetId} failed.",
                    ErrorCategory.InvalidArgument,
                    ByObject));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a new empty dataset in the specified project.
    /// </para>
    /// <para type="description">
    /// Creates a new, empty dataset in the specified project. A Dataset can be supplied by object via the 
    /// pipeline or the -ByObject parameter, or it can be instantiated by value with the flags below. 
    /// If no Project is specified, the default project will be used. This cmdlet returns a Dataset object.
    /// </para>
    /// <example>
    ///   <code>PS C:\> $dataset | New-GcbqDataset -Project "my-project"</code>
    ///   <para>This takes a Dataset object from the pipeline and inserts it into the Cloud project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GcbqDataset "test_data_id" -Name "Testdata" `
    ///   -Description "Some interesting data!" -Expiration 86400000</code>
    ///   <para>This builds a new dataset with the supplied datasetId, name, description, and an Expiration of 1 day.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/datasets)">
    /// [BigQuery Datasets]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcbqDataset")]
    public class NewGcbqDataset : GcbqCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByObject = "ByObject";
            public const string ByValues = "ByValue";
        }

        /// <summary>
        /// <para type="description">
        /// The project to look for datasets in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        override public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The dataset object that will be sent to the server to be inserted.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ByObject)]
        public Dataset Dataset { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DatasetId must be unique within the project and match the pattern [a-zA-Z0-9_]+.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.ByValues)]
        [ValidatePattern("[a-zA-Z0-9_]")]
        [ValidateLength(1, 1024)]
        public string DatasetId { get; set; }

        /// <summary>
        /// <para type="description">
        /// User-friendly name for the dataset
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValues)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// Description of the dataset.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValues)]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The default lifetime for tables in the dataset (in seconds).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValues)]
        [ValidateRange(3600, (long.MaxValue/1000))]
        public long Expiration { get; set; }

        protected override void ProcessRecord()
        {
            // Set up the Dataset based on parameters
            Dataset newData;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByObject:
                    newData = Dataset;
                    break;
                case ParameterSetNames.ByValues:
                    newData = new Dataset();
                    newData.DatasetReference = new DatasetReference();
                    newData.DatasetReference.DatasetId = DatasetId;
                    newData.DatasetReference.ProjectId = Project;
                    newData.FriendlyName = Name;
                    newData.Description = Description;
                    if (Expiration != 0)
                    {
                        newData.DefaultTableExpirationMs = Expiration * 1000;
                    }
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            // Add the new dataset to the project supplied.
            DatasetsResource.InsertRequest request = Service.Datasets.Insert(newData, Project);
            Dataset response = request.Execute();
            if (response != null)
            {
                WriteObject(response);
            }
            else
            {
                WriteError(new ErrorRecord(
                    new Exception("400"), 
                    "Insert request to server failed.",
                    ErrorCategory.InvalidArgument, 
                    newData));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes the specified dataset.
    /// </para>
    /// <para type="description">
    /// Deletes the specified dataset. This command takes a Dataset object as input, typically off the pipeline. 
    /// You can also use the -ByObject flag and pass it as a parameter. The dataset must be empty to be deleted. 
    /// Use the -Force flag if the dataset is not empty to confirm deletion of all tables in the dataset. 
    /// Once this operation is complete, you may create a new dataset with the same name. If no Project is specified, 
    /// the default project will be used. If the deletion runs without error, this cmdlet has no output.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset "my-values" | Remove-GcbqDataset </code>
    ///   <para>This deletes "my-values" only if it is empty.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset "test-values" | Remove-GcbqDataset -Force</code>
    ///   <para>This deletes "test-values" and all of its contents.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/datasets)">
    /// [BigQuery Datasets]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcbqDataset", SupportsShouldProcess = true)]
    public class RemoveGcbqDataset : GcbqCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to look for datasets in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        override public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Dataset to delete.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        public Dataset ByObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// Forces deletion of tables inside a non-empty Dataset.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            // Form and send request.
            DatasetsResource.DeleteRequest request = 
                new DatasetsResource.DeleteRequest(Service, Project, ByObject.DatasetReference.DatasetId);
            String response = "Dataset Removal Stopped.";
            if (ShouldProcess(ByObject.DatasetReference.DatasetId))
            {
                if (Force == true)
                {
                    request.DeleteContents = true;
                    response = request.Execute();
                }
                else
                {
                    TablesResource.ListRequest checkTables = 
                        new TablesResource.ListRequest(Service, Project, ByObject.DatasetReference.DatasetId);
                    var tableResponse = checkTables.Execute();
                    if (tableResponse.TotalItems == 0)
                    {
                        response = request.Execute();
                    }
                    else if (ShouldContinue(
                        GetConfirmationMessage(ByObject, tableResponse.TotalItems), 
                        "Confirm Deletion"))
                    {
                        request.DeleteContents = true;
                        response = request.Execute();
                    }
                }

                if (response.Length > 0)
                {
                    WriteError(new ErrorRecord(new Exception(response), "Deletion failed.",
                        ErrorCategory.OperationStopped, response));
                }
            }
        }

        private string GetConfirmationMessage(Dataset d, int? tables)
        {
            return $@"{d.DatasetReference.DatasetId} has {tables} tables and the -Force parameter was not specified. "
                + "If you continue, all tables will be removed with the dataset. Are you sure you want to continue?";
        }
    }
}
