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
        public string Project { get; set; }

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
}
