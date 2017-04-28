// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;
using Google.PowerShell.Common;
using System;
using System.Net;
using System.Management.Automation;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace Google.PowerShell.BigQuery
{
    /// <summary>
    /// <para type="synopsis">
    /// Lists BigQuery datasets in a specific Cloud project.
    /// </para>
    /// <para type="description">
    /// If a Dataset is specified, it will return an object describing that dataset. If no Dataset is 
    /// specified, this cmdlet lists all datasets in the specified project to which you have been granted the 
    /// "READER" dataset role. The "-IncludeHidden" flag will include hidden datasets in the search results. 
    /// The "-Filter" flag allows you to filter results by label. The syntax to filter is "name[:value]". 
    /// Multiple filters can be ANDed together by passing them in as a string array. See the link below for 
    /// more on labels. If no Project is specified, the default project will be used. If no Dataset was 
    /// specified, this cmdlet returns any number of DatasetList.DatasetData objects. Otherwise, it returns
    /// a Dataset object.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> Get-BqDataset -Project my-project
    ///   </code>
    ///   <para>This lists all of the non-hidden datasets in the Cloud project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> Get-BqDataset -IncludeHidden -Filter "department:shipping"
    ///   </code>
    ///   <para>This lists all of the datasets in the default Cloud project for your account that have 
    ///   the key "department" with the value "shipping".</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> Get-BqDataset -IncludeHidden -Filter "department:shipping","location:usa"
    ///   </code>
    ///   <para>This is an example of ANDing multiple filters for a list request.</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> Get-BqDataset "my-dataset"
    ///   </code>
    ///   <para>This returns a Dataset object from the default project of the dataset with id "my-dataset".</para>
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
        /// Filters results by label. The syntax for each label is "/<name/>[:/<value/>]".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.List)]
        public string[] Filter { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the dataset that you want to get a descriptor object for. This field also accepts 
        /// DatasetData objects so they can be mapped to full Dataset objects.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, 
            ParameterSetName = ParameterSetNames.GetWithString)]
        public string DatasetId { get; set; }

        /// <summary>
        /// <para type="description">
        /// DatasetRefrence object to get an updated Dataset object for.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, 
            ParameterSetName = ParameterSetNames.GetWithRef)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(DatasetList.DatasetsData),
            Property = nameof(DatasetList.DatasetsData.DatasetReference))]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Dataset), 
            Property = nameof(Apis.Bigquery.v2.Data.Dataset.DatasetReference))]
        public DatasetReference Dataset { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.List:
                    var datasets = DoListRequest(Project);
                    WriteObject(datasets, true);
                    break;
                case ParameterSetNames.GetWithString:
                    var dataset = DoGetRequest(Project, DatasetId);
                    if (dataset != null)
                    {
                        WriteObject(dataset);
                    }
                    break;
                case ParameterSetNames.GetWithRef:
                    var datasetDR = DoGetRequest(Dataset.ProjectId, Dataset.DatasetId);
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
            lRequest.Filter = (Filter == null) ? null : string.Join(" ", Filter.Select(item => $"labels.{item}"));
            do
            {
                DatasetList response = lRequest.Execute();
                if (response == null)
                {
                    ThrowTerminatingError(new ErrorRecord(
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
                ThrowTerminatingError(new ErrorRecord(ex,
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
    /// Updates information describing an existing BigQuery dataset. If the dataset passed in does not
    /// already exist on the server, it will be inserted. Use the -SetLabel and -ClearLabel flags to 
    /// manage the dataset's key:value label pairs. This cmdlet returns a Dataset object.
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
    /// PS C:\> $data = Get-BqDataset "test_set"
    /// PS C:\> $data = $data | Set-BqDataset -SetLabel @{"test"="three";"other"="two"}
    ///   </code>
    ///   <para>This will add the labels "test" and "other" with their values to "test_set".</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> $data = Get-BqDataset "test_set"
    /// PS C:\> $data = $data | Set-BqDataset -ClearLabel "test","other"
    ///   </code>
    ///   <para>This is the opposite of the above. It removes the labels "test" and "other" from the Dataset.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/datasets)">
    /// [BigQuery Datasets]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "BqDataset", DefaultParameterSetName = ParameterSetNames.Default)]
    public class SetBqDataset : BqCmdlet
    {
        private class ParameterSetNames
        {
            public const string Default = "Default";
            public const string SetLabel = "SetLabel";
            public const string ClearLabel = "ClearLabel";
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
        /// The updated Dataset object. Must have the same DatasetId as an existing 
        /// dataset in the project specified.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.Default)]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.SetLabel)]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ClearLabel)]
        [ValidateNotNull]
        public Dataset Dataset { get; set; }

        /// <summary>
        /// <para type="description">
        /// Sets the labels in Keys to the values in Values for the target Dataset.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.SetLabel)]
        [ValidateNotNullOrEmpty]
        public Hashtable SetLabel { get; set; }

        /// <summary>
        /// <para type="description">
        /// Clears the keys in Keys for the target Dataset.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ClearLabel)]
        [ValidateNotNullOrEmpty]
        public string[] ClearLabel { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.Default:
                    break;
                case ParameterSetNames.SetLabel:
                    Dataset.Labels = Dataset.Labels ?? new Dictionary<string, string>();
                    foreach (var key in SetLabel.Keys)
                    {
                        Dataset.Labels.Remove((string) key);
                        Dataset.Labels.Add((string) key, (string) SetLabel[key]);
                    }
                    break;
                case ParameterSetNames.ClearLabel:
                    foreach (string key in ClearLabel)
                    {
                        Dataset.Labels.Remove(key);
                    }
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            Dataset response;
            var request = Service.Datasets.Update(Dataset,
                Dataset.DatasetReference.ProjectId,
                Dataset.DatasetReference.DatasetId);
            //necessary because of wacky things happening when you throw inside a catch
            bool needToInsert = false;

            try
            {
                response = request.Execute();
                WriteObject(response);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"Conflict while updating '{Dataset.DatasetReference.DatasetId}'.",
                    ErrorCategory.WriteError,
                    Dataset));
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"You do not have permission to modify '{Dataset.DatasetReference.DatasetId}'.",
                    ErrorCategory.PermissionDenied,
                    Dataset));
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                if (!ex.Message.Contains(DS_404))
                {
                    ThrowTerminatingError(new ErrorRecord(ex, $"Project '{Project}' not found.",
                        ErrorCategory.ObjectNotFound, Project));
                }
                needToInsert = true;
            }

            if (needToInsert)
            {
                // Turn a Set- into a New- in the case of a 404 on the object to set.
                DatasetsResource.InsertRequest insertRequest = Service.Datasets.Insert(Dataset, Project);
                try
                {
                    var insertResponse = insertRequest.Execute();
                    WriteObject(insertResponse);
                }
                catch (Exception e2)
                {
                    ThrowTerminatingError(new ErrorRecord(e2,
                        $"Dataset was not found and an error occured while creating a new Dataset.",
                        ErrorCategory.NotSpecified, Dataset));
                }
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a new empty dataset in the specified project.
    /// </para>
    /// <para type="description">
    /// Creates a new, empty dataset in the specified project. A Dataset can be supplied by object via the 
    /// pipeline or the "-Dataset" parameter, or it can be instantiated by value with the flags below. 
    /// If no Project is specified, the default project will be used. This cmdlet returns a Dataset object.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $premade_dataset | New-BqDataset
    ///   </code>
    ///   <para>This takes a Dataset object from the pipeline and inserts it into the Cloud project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> New-BqDataset "test_data_id" `
    ///                       -Name "Testdata" `
    ///                       -Description "Some interesting data!" `
    ///                       -Expiration 86400000
    ///   </code>
    ///   <para>This builds a new dataset with the supplied datasetId, name, description, and an expiration of 1 day.</para>
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
        public Dataset Dataset { get; set; }

        /// <summary>
        /// <para type="description">
        /// "DatasetId" must be unique within the project and match the pattern "[a-zA-Z0-9_]+".
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
                    Project = Dataset.DatasetReference.ProjectId;
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
                // Send a Get request to correctly populate the ETag field.
                DatasetsResource.GetRequest getCorrectETag = 
                    Service.Datasets.Get(Project, response.DatasetReference.DatasetId);
                response = getCorrectETag.Execute();
                WriteObject(response);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
            {
                ThrowTerminatingError(new ErrorRecord(ex, 
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
    /// pipeline or through the "-Dataset" parameter. You can also specify a projectId:datasetId 
    /// combination through the "-Project" and "-DatasetId" flags. The dataset must be empty to be deleted. 
    /// Use the "-Force" flag if the dataset is not empty to confirm deletion of all tables in the dataset. 
    /// Once this operation is complete, you may create a new dataset with the same name. If no Project 
    /// is specified, the default project will be used. If the deletion runs without error, this cmdlet 
    /// has no output. This cmdlet supports the ShouldProcess function, so it has the corresponding 
    /// "-WhatIf" and "-Confirm" flags.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> Get-BqDataset "my-values" | Remove-BqDataset
    ///   </code>
    ///   <para>This deletes "my-values" only if it is empty.</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> $set = Get-BqDataset "test-values"
    /// PS C:\> Remove-BqDataset $set -Force
    ///   </code>
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
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByValues)]
        public string DatasetId { get; set; }

        /// <summary>
        /// <para type="description">
        /// Dataset to delete. Takes Dataset, DatasetsData, and DatasetReference Objects.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0, ParameterSetName = ParameterSetNames.ByObject)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(DatasetList.DatasetsData),
            Property = nameof(DatasetList.DatasetsData.DatasetReference))]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Dataset),
            Property = nameof(Apis.Bigquery.v2.Data.Dataset.DatasetReference))]
        public DatasetReference Dataset { get; set; }

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
                    Project = Dataset.ProjectId;
                    DatasetId = Dataset.DatasetId;
                    request = Service.Datasets.Delete(Project, DatasetId);
                    break;
                case ParameterSetNames.ByValues:
                    request = Service.Datasets.Delete(Project, DatasetId);
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            String response = "Dataset Removal Stopped.";
            if (ShouldProcess(DatasetId))
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
                            new TablesResource.ListRequest(Service, Project, DatasetId);
                        var tableResponse = checkTables.Execute();
                        if (tableResponse.TotalItems == 0)
                        {
                            response = request.Execute();
                        }
                        else if (ShouldContinue(
                            GetConfirmationMessage(DatasetId, tableResponse.TotalItems),
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
                    ThrowTerminatingError(new ErrorRecord(ex, 
                        $"You do not have permission to delete '{DatasetId}'.",
                        ErrorCategory.PermissionDenied, 
                        DatasetId));
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
