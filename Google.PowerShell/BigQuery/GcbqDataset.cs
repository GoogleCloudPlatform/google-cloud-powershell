// Copyright 2015-2016 Google Inc. All Rights Reserved.
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
    /// Creates a new empty dataset in the specified project.  This command can either take a premade Dataset object, 
    /// or build one using parameters.  Premade Datasets can be passed via the pipeline or with the -Dataset parameter.
    /// If both are supplied, the premade dataset object will be used and the builder values will be ignored.  If no 
    /// Project is specified, the default project will be used.  This cmdlet returns a Dataset object.  
    /// The builder option will create a new Dataset object and inject all of the supplied values before insertion. 
    /// Required build parameters are passed directly after the command, whereas optional parameters are passed as flags.
    /// -- Builder parameters: 
    /// DatasetId (required) - unique identifier for the dataset.
    /// Name - descriptive name for the dataset.
    /// Description - user-friendly description of the dataset.
    /// Timeout - default duration in ms for tables in the dataset to exist.
    /// </para>
    /// <example>
    ///   <code>PS C:\> $dataset | New-GcbqDataset -Project $projId </code>
    ///   <para>This command will take a premade Dataset object from the pipeline and insert it into the Cloud project. 
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
        /// <summary>
        /// <para type="description">
        /// The project to look for datasets in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "premade")]
        [Parameter(Mandatory = false, ParameterSetName = "builder")]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The dataset object that will be sent to the server to be inserted.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ValueFromPipeline = true, ParameterSetName = "premade")]
        public Dataset Dataset { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DatasetId for the datset reference object.  Must be unique within the project, and must be 1024 
        /// characters or less.  It must only contain letters (a-z, A-Z), numbers (0-9), or underscores (_).
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "builder")]
        public string DatasetId { get; set; }

        /// <summary>
        /// <para type="description">
        /// The "friendly name" field for the dataset.  Used for a descriptive name for the dataset.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "builder")]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The "description" field for the dataset.  Used for a user-friendly description of the dataset.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "builder")]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The default lifetime for tables in the dataset to exist in ms.  Minimum is 3600000 (1hr).  
        /// When the expiration time is reached, the table will be automatically deleted.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "builder")]
        public int Timeout { get; set; }

        protected override void ProcessRecord()
        {
            Dataset newData;
            // Check whether they are passing in a completed Dataset object or are building one with params.
            if (Dataset != null)
            {
                newData = Dataset;
            }
            // Builder mode selected.
            else
            {
                // Check for required fields.
                if (DatasetId == null)
                {
                    WriteError(new ErrorRecord(new Exception("DatasetId missing."), "Insert failed.",
                        ErrorCategory.InvalidArgument, DatasetId));
                    return;
                }
                // Everything present.  Create a new Dataset object.
                newData = new Dataset();
                newData.FriendlyName = (Name != null) ? Name : "New Dataset";
                newData.Description = (Description != null) ? Description : "New Dataset";
                newData.DefaultTableExpirationMs = Timeout;
                newData.DatasetReference = new DatasetReference();
                newData.DatasetReference.DatasetId = DatasetId;
                newData.DatasetReference.ProjectId = Project;
            }

            // Add the new dataset to the project supplied.
            DatasetsResource.InsertRequest request = Service.Datasets.Insert(newData, Project);
            Dataset response = request.Execute();
            if (response != null)
            {
                WriteObject(response, true);
            }
            else
            {
                WriteError(new ErrorRecord(new Exception("Insert request failed."), "Insert failed.",
                    ErrorCategory.InvalidArgument, newData));
            }
        }
    }
}
