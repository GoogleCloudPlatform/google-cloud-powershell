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
    /// flag allows you to filter results by label. The syntax is "labels.name[:value]". Multiple filters 
    /// can be ANDed together by connecting with a space. See the link below for more information. This command 
    /// will read a ProjectData object from the pipeline if provided and will use that as the selected project.
    /// If no Project is specified, the default project will be used. This cmdlet returns a DatasetList if 
    /// no DatasetId was specified, and a Dataset otherwise.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset "my-dataset"</code>
    ///   <para>This command will return a Dataset object from the default project of the dataset with id my-dataset</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset -Project my-project</code>
    ///   <para>This command will list all of the non-hidden datasets in the Cloud project $projId.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset -All</code>
    ///   <para>This command will list all of the datasets in the default Cloud project for your account.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset -All -Filter "labels.department:shipping"</code>
    ///   <para>This command will list all of the datasets in the default Cloud project for your account that have 
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
        /// The project to look for datasets in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.List)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.Get)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        override public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Flag to include hidden datasets in the search results.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.List)]
        public SwitchParameter All { get; set; }

        /// <summary>
        /// <para type="description">
        /// Flag to filter results by label. The syntax is "labels./<name/>[:/<value/>]". Multiple filters can 
        /// be ANDed together by connecting with a space.
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
                        WriteError(new ErrorRecord(new Exception("400"), "List request to server failed.",
                            ErrorCategory.InvalidArgument, Project));
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
                        WriteError(new ErrorRecord(new Exception("400"), "Get request to server failed.",
                            ErrorCategory.InvalidArgument, Dataset));
                    }
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a new empty dataset in the specified project.
    /// </para>
    /// <para type="description">
    /// Creates a new empty dataset in the specified project from a Dataset Object.  This Dataset can be supplied by object via the 
    /// pipeline or with the -ByObject parameter, or they can be supplied by value with the flags listed below.  If no Project is 
    /// specified, the default project will be used.  This cmdlet returns a Dataset object.  Required value parameters are to be 
    /// passed in order after the command, and optional parameters are passed as flags.
    ///    ByValue Parameters: 
    /// DatasetId (required) - unique identifier for the dataset.
    /// Name - descriptive name for the dataset.
    /// Description - user-friendly description of the dataset.
    /// Timeout - default duration in ms for tables in the dataset to exist. 
    /// </para>
    /// <example>
    ///   <code>PS C:\> $dataset | New-GcbqDataset -Project "my-project" </code>
    ///   <para>This command will take a Dataset object from the pipeline and insert it into the Cloud project "my-project". 
    ///   project $projId</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GcbqDataset "test_data_id" -Name "Testdata" -Description "Some interesting data!" -Timeout 86400000</code>
    ///   <para>This command will build a new dataset with the supplied datasetId, name, description, and a timeout of 1 day.</para>
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
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByObject)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValues)]
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
        /// The DatasetId for the datset reference object. Must be unique within the project, and must be 1024 
        /// characters or less. It must only contain letters (a-z, A-Z), numbers (0-9), or underscores (_).
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.ByValues)]
        [ValidatePattern("[a-zA-Z0-9]")]
        [ValidateLength(1, 1024)]
        public string DatasetId { get; set; }

        /// <summary>
        /// <para type="description">
        /// The "friendly name" field for the dataset. Used for a descriptive name for the dataset.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValues)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The "description" field for the dataset. Used for a user-friendly description of the dataset.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValues)]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The default lifetime for tables in the dataset to exist in ms. Minimum is 3600000 (1hr).  
        /// When the expiration time is reached, the table will be automatically deleted.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValues)]
        [ValidateRange(3600000, long.MaxValue)]
        public long Timeout { get; set; }

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
                    newData.DefaultTableExpirationMs = Timeout;
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
                WriteError(new ErrorRecord(new Exception("400"), "Insert request to server failed.",
                    ErrorCategory.InvalidArgument, newData));
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
    /// Use the Force flag if the dataset is not empty to confirm deletion of all tables in the dataset. 
    /// Once this operation is complete, you may create a new dataset with the same name. If no Project is specified, 
    /// the default project will be used. If the deletion runs without error, this cmdlet has no output.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset "my-values" | Remove-GcbqDataset </code>
    ///   <para>This command will delete "my-values" only if it is empty.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcbqDataset "test-values" | Remove-GcbqDataset -Force</code>
    ///   <para>This command will delete "test-values" and all of its contents.</para>
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
        /// The Dataset object to delete. This must be empty, or you also need to specify --DeleteContents.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        public Dataset ByObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// Bypass confirmation of removal operations in the case that the Dataset contains tables.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            // Form and send request.
            DatasetsResource.DeleteRequest request = new DatasetsResource.DeleteRequest(Service, Project, ByObject.DatasetReference.DatasetId);
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
                    TablesResource.ListRequest checkTables = new TablesResource.ListRequest(Service, Project, ByObject.DatasetReference.DatasetId);
                    var tableResponse = checkTables.Execute();
                    if (tableResponse.TotalItems == 0)
                    {
                        response = request.Execute();
                    }
                    else if (ShouldContinue(GetConfirmationMessage(ByObject, tableResponse.TotalItems), "Confirm Deletion"))
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

        /// <summary>
        /// Helper method to generate the confirmation message for deletion of a dataset that still contains tables
        /// </summary>
        /// <param name="d">Dataset in question</param>
        /// <param name="tables">Number of tables that will be deleted</param>
        /// <returns></returns>
        private string GetConfirmationMessage(Dataset d, int? tables)
        {
            return d.DatasetReference.DatasetId
                + " has "
                + tables
                + " tables and the Force parameter was not specified. If you continue, all tables will be removed with the dataset. Are you sure you want to continue?";
        }
    }
}
