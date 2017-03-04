// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;
using Google.PowerShell.Common;
using System;
using System.Net;
using System.Management.Automation;
using System.Collections.Generic;

namespace Google.PowerShell.BigQuery
{
    /// <summary>
    /// <para type="synopsis">
    /// Lists BigQuery datasets in a specific Cloud project.
    /// </para>
    /// <para type="description">
    /// If a DatasetId is specified, it will return an object describing that dataset. If no DatasetId is 
    /// specified, this cmdlet lists all datasets in the specified project to which you have been granted the 
    /// READER dataset role. The -IncludeHidden flag will include hidden datasets in the search results. The -Filter 
    /// flag allows you to filter results by label. The syntax to filter is "labels.name[:value]". Multiple filters 
    /// can be ANDed together by connecting with a space. See the link below for more information.
    /// If no Project is specified, the default project will be used. This cmdlet returns any number of 
    /// DatasetList.DatasetData objects if no DatasetId was specified, or a Dataset otherwise.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-BqDataset "my-dataset"</code>
    ///   <para>This returns a Dataset object from the default project of the dataset with id <code>my-dataset</code>.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-BqDataset -Project my-project</code>
    ///   <para>This lists all of the non-hidden datasets in the Cloud project <code>my-project</code>.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-BqDataset -IncludeHidden</code>
    ///   <para>This lists all of the datasets in the default Cloud project for your account.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-BqDataset -IncludeHidden -Filter "labels.department:shipping"</code>
    ///   <para>This lists all of the datasets in the default Cloud project for your account that have 
    ///   the <code>department</code> key with a value <code>shipping</code>.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-BqDataset -IncludeHidden -Filter "labels.department:shipping labels.location:usa"</code>
    ///   <para>This is an example of ANDing multiple filters.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/datasets)">
    /// [BigQuery Datasets]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/labeling-datasets#filtering_datasets_using_labels)">
    /// [Filtering datasets using labels]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "BqDataset")]
    public class GetBqDataset : BqCmdlet
    {
        private class ParameterSetNames
        {
            public const string List = "List";
            public const string GetWithString = "GetWithString";
            public const string GetWithRef = "GetWithRef";
        }

        /// <summary>
        /// <para type="description">
        /// The project to look for datasets in. If not set via PowerShell parameter processing, it will
        /// default to the Cloud SDK's default project.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Includes hidden datasets in the output if set.
        /// </para>
        /// </summary>
        [Alias("All")]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.List)]
        public SwitchParameter IncludeHidden { get; set; }

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
        /// The ID of the dataset that you want to get a descriptor object for. This field also accepts 
        /// DatasetData objects so they can be mapped to full Dataset objects.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, 
            ParameterSetName = ParameterSetNames.GetWithString)]
        public string Dataset { get; set; }

        /// <summary>
        /// <para type="description">
        /// DatasetRefrence object used to pass in Project and Dataset values.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, 
            ParameterSetName = ParameterSetNames.GetWithRef)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(DatasetList.DatasetsData),
            Property = nameof(DatasetList.DatasetsData.DatasetReference))]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Dataset), 
            Property = nameof(Apis.Bigquery.v2.Data.Dataset.DatasetReference))]
        public DatasetReference DatasetRef { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.List:
                    var datasets = DoListRequest(Project);
                    WriteObject(datasets, true);
                    break;
                case ParameterSetNames.GetWithString:
                    var dataset = DoGetRequest(Project, Dataset);
                    if (dataset != null)
                    {
                        WriteObject(dataset);
                    }
                    break;
                case ParameterSetNames.GetWithRef:
                    var datasetDR = DoGetRequest(DatasetRef.ProjectId, DatasetRef.DatasetId);
                    if (datasetDR != null)
                    {
                        WriteObject(datasetDR);
                    }
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private IEnumerable<DatasetList.DatasetsData> DoListRequest(string project)
        {
            DatasetsResource.ListRequest lRequest = Service.Datasets.List(project);
            lRequest.All = IncludeHidden;
            lRequest.Filter = Filter;
            do
            {
                DatasetList response = lRequest.Execute();
                if (response == null)
                {
                    WriteError(new ErrorRecord(
                        new Exception("List request to server responded with null."),
                        "List request returned null", ErrorCategory.InvalidArgument, project));
                }
                if (response.Datasets != null)
                {
                    foreach(DatasetList.DatasetsData d in response.Datasets)
                    {
                        yield return d;
                    }
                }
                lRequest.PageToken = response.NextPageToken;
            }
            while (!Stopping && lRequest.PageToken != null);
        }

        private Dataset DoGetRequest(string project, string dataset)
        {
            DatasetsResource.GetRequest gRequest = Service.Datasets.Get(project, dataset);
            try
            {
                return gRequest.Execute();
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                WriteError(new ErrorRecord(ex,
                    $"Error 404: Dataset '{dataset}' not found in '{project}'.",
                    ErrorCategory.ObjectNotFound,
                    dataset));
                return null;
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Updates information describing an existing BigQuery dataset.
    /// </para>
    /// <para type="description">
    /// Updates information describing an existing BigQuery dataset. The projet and dataset specified 
    /// in the dataset's DatasetReference will be used. This cmdlet returns a Dataset object.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $updatedSet = Get-BqDataset "my_dataset"
    /// PS C:\> $updatedSet.Description = "An updated summary of the data contents."
    /// PS C:\> $updatedSet | Set-BqDataset
    ///   </code>
    ///   <para>This will update the values stored for the Bigquery dataset passed in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> $updatedSet = Get-BqDataset "my_dataset"
    /// PS C:\> $updatedSet.DefaultTableExpirationMs = 60 * 60 * 24 * 7
    /// PS C:\> $updatedSet.Description = "A set of tables that last for exactly one week."
    /// PS C:\> Set-BqDataset -InputObject $updatedSet
    ///   </code>
    ///   <para>This will update the values stored for my_dataset and shows how to pass it in as a parameter.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/datasets)">
    /// [BigQuery Datasets]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "BqDataset")]
    public class SetBqDataset : BqCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The projectId from the InputObject will be preferred.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description"> 
        /// The updated Dataset object.  Must have the same datasetId as an existing 
        /// dataset in the project specified.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        public Dataset InputObject { get; set; }

        protected override void ProcessRecord()
        {
            Dataset response;
            var request = Service.Datasets.Update(InputObject,
                InputObject.DatasetReference.ProjectId, 
                InputObject.DatasetReference.DatasetId);
            try
            {
                response = request.Execute();
                WriteObject(response);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
            {
                WriteError(new ErrorRecord(ex,
                    $"Conflict while updating '{InputObject.DatasetReference.DatasetId}'.",
                    ErrorCategory.WriteError,
                    InputObject));
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
            {
                WriteError(new ErrorRecord(ex,
                    $"You do not have permission to modify '{InputObject.DatasetReference.DatasetId}'.",
                    ErrorCategory.PermissionDenied,
                    InputObject));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a new empty dataset in the specified project.
    /// </para>
    /// <para type="description">
    /// Creates a new, empty dataset in the specified project. A Dataset can be supplied by object via the 
    /// pipeline or the -InputObject parameter, or it can be instantiated by value with the flags below. 
    /// If no Project is specified, the default project will be used. This cmdlet returns a Dataset object.
    /// </para>
    /// <example>
    ///   <code>PS C:\> $dataset | New-BqDataset</code>
    ///   <para>This takes a Dataset object from the pipeline and inserts it into the Cloud project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> New-BqDataset "test_data_id" -Name "Testdata" `
    /// -Description "Some interesting data!" -Expiration 86400000
    ///   </code>
    ///   <para>This builds a new dataset with the supplied datasetId, name, description, and an Expiration of 1 day.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/datasets)">
    /// [BigQuery Datasets]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "BqDataset")]
    public class NewBqDataset : BqCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByObject = "ByObject";
            public const string ByValues = "ByValue";
        }

        /// <summary>
        /// <para type="description">
        /// The project to look for datasets in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's default project.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValues)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The dataset object that will be sent to the server to be inserted.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ByObject)]
        public Dataset InputObject { get; set; }

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
                    newData = InputObject;
                    Project = InputObject.DatasetReference.ProjectId;
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
            try
            {
                Dataset response = request.Execute();
                WriteObject(response);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
            {
                WriteError(new ErrorRecord(ex,  
                    $"A dataset with the name '{DatasetId}' already exists in project '{Project}'.",
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
    /// Deletes the specified dataset. This command takes a Dataset object as input, typically off the 
    /// pipeline or through the -InputObject parameter. You can also specify a projectId:datasetId 
    /// combination through the -Project and -Dataset flags. The dataset must be empty to be deleted. 
    /// Use the -Force flag if the dataset is not empty to confirm deletion of all tables in the dataset. 
    /// Once this operation is complete, you may create a new dataset with the same name. If no Project 
    /// is specified, the default project will be used. If the deletion runs without error, this cmdlet 
    /// has no output. This cmdlet supports the ShouldProcess function, so it has the corresponding 
    /// -WhatIf and -Confirm flags.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-BqDataset "my-values" | Remove-BqDataset </code>
    ///   <para>This deletes "my-values" only if it is empty.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-BqDataset "test-values" | Remove-BqDataset -Force</code>
    ///   <para>This deletes "test-values" and all of its contents.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/datasets)">
    /// [BigQuery Datasets]
    /// </para>
    /// <para type="link" uri="(https://msdn.microsoft.com/en-us/library/ms568267.aspx)">
    /// [ShouldProcess]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "BqDataset", SupportsShouldProcess = true)]
    public class RemoveBqDataset : BqCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByObject = "ByObject";
            public const string ByValues = "ByValue";
        }

        /// <summary>
        /// <para type="description">
        /// The project to look for datasets in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's default project.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// DatasetId to delete.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByValues)]
        public string Dataset { get; set; }

        /// <summary>
        /// <para type="description">
        /// Dataset to delete. Takes Dataset, DatasetsData, and DatasetReference Objects.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ByObject)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(DatasetList.DatasetsData),
            Property = nameof(DatasetList.DatasetsData.DatasetReference))]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Dataset),
            Property = nameof(Apis.Bigquery.v2.Data.Dataset.DatasetReference))]
        public DatasetReference InputObject { get; set; }

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
            DatasetsResource.DeleteRequest request;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByObject:
                    Project = InputObject.ProjectId;
                    Dataset = InputObject.DatasetId;
                    request = Service.Datasets.Delete(Project, Dataset);
                    break;
                case ParameterSetNames.ByValues:
                    request = Service.Datasets.Delete(Project, Dataset);
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            String response = "Dataset Removal Stopped.";
            if (ShouldProcess(Dataset))
            {
                try
                {
                    if (Force == true)
                    {
                        request.DeleteContents = true;
                        response = request.Execute();
                    }
                    else
                    {
                        TablesResource.ListRequest checkTables =
                            new TablesResource.ListRequest(Service, Project, Dataset);
                        var tableResponse = checkTables.Execute();
                        if (tableResponse.TotalItems == 0)
                        {
                            response = request.Execute();
                        }
                        else if (ShouldContinue(
                            GetConfirmationMessage(Dataset, tableResponse.TotalItems),
                            "Confirm Deletion"))
                        {
                            request.DeleteContents = true;
                            response = request.Execute();
                        }
                    }
                    WriteObject(response);
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
                {
                    WriteError(new ErrorRecord(ex, 
                        $"You do not have permission to delete '{Dataset}'.",
                        ErrorCategory.PermissionDenied, 
                        Dataset));
                } 
            }
        }

        private string GetConfirmationMessage(string datasetId, int? tables)
        {
            return $@"'{datasetId}' has {tables} tables and the -Force parameter was not specified. "
                + "If you continue, all tables will be removed with the dataset. Are you sure you want to continue?";
        }
    }
}
