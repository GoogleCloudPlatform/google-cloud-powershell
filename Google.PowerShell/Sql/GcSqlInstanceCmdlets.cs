﻿// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.SQLAdmin.v1beta4;
using Google.Apis.SQLAdmin.v1beta4.Data;
using System.Management.Automation;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System.Linq;

namespace Google.PowerShell.Sql
{
    /// <summary>
    /// <para type="synopsis">
    /// Retrieves a resource containing information about a Cloud SQL instance, or lists all instances in a project.
    /// </para>
    /// <para type="description">
    /// Retrieves a resource containing the information for the specified Cloud SQL instance, 
    /// or lists all instances in a project.
    /// This is determined by if Instance is specified or not.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlInstance", DefaultParameterSetName = ParameterSetNames.GetList)]
    public class GetGcSqlInstanceCmdlet : GcSqlCmdlet
    {
        internal class ParameterSetNames
        {
            public const string GetSingle = "Single";
            public const string GetList = "List";
        }

        /// <summary>
        /// <para type="description">
        /// Project name of the project that contains instance(s).
        /// Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Cloud SQL instance name. 
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.GetSingle,
            ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            if (Name != null)
            {
                InstancesResource.GetRequest request = Service.Instances.Get(Project, Name);
                DatabaseInstance result = request.Execute();
                WriteObject(result);
            }
            else
            {
                IEnumerable<DatabaseInstance> results = GetAllSqlInstances();
                WriteObject(results, true);
            }
        }

        private IEnumerable<DatabaseInstance> GetAllSqlInstances()
        {
            InstancesResource.ListRequest request = Service.Instances.List(Project);
            do
            {
                InstancesListResponse aggList = request.Execute();
                IList<DatabaseInstance> instanceList = aggList.Items;
                if (instanceList == null)
                {
                    yield break;
                }
                foreach (DatabaseInstance instance in instanceList)
                {
                    yield return instance;
                }
                request.PageToken = aggList.NextPageToken;
            }
            while (request.PageToken != null);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a new Cloud SQL instance.
    /// </para>
    /// <para type="description">
    /// Creates the Cloud SQL instance resource in the specified project.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcSqlInstance")]
    public class AddGcSqlInstanceCmdlet : GcSqlCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The instance resource. 
        /// Can be created with New-GcSqlInstanceConfig.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        public DatabaseInstance InstanceConfig { get; set; }

        protected override void ProcessRecord()
        {
            InstancesResource.InsertRequest request = Service.Instances.Insert(InstanceConfig, Project);
            Operation result = request.Execute();
            WaitForSqlOperation(result);
            /// We get the instance that was just added
            /// so that the returned DatabaseInstance is as accurate as possible.
            InstancesResource.GetRequest instanceRequest = Service.Instances.Get(Project, InstanceConfig.Name);
            WriteObject(instanceRequest.Execute());
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes a Cloud SQL instance.
    /// </para>
    /// <para type="description">
    /// Deletes the specified Cloud SQL instance.
    /// 
    /// Warning: This deletes all data inside of it as well.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcSqlInstance", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.ByName)]
    public class RemoveGcSqlInstanceCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByInstance = "ByInstance";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to be deleted.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.ByName)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DatabaseInstance that describes the instance we want to remove.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByInstance, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public DatabaseInstance InstanceObject { get; set; }

        protected override void ProcessRecord()
        {
            string project;
            string instance;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    instance = Instance;
                    project = Project;
                    break;
                case ParameterSetNames.ByInstance:
                    instance = InstanceObject.Name;
                    project = InstanceObject.Project;
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            if (!ShouldProcess($"{project}/{instance}", "Delete Instance"))
            {
                return;
            }
            InstancesResource.DeleteRequest request = Service.Instances.Delete(project, instance);
            Operation result = request.Execute();
            WaitForSqlOperation(result);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a Cloud SQL instance as a clone of the source instance. 
    /// </para>
    /// <para type="description">
    /// Creates a Cloud SQL instance as a clone of the specified instance.
    /// WARNING: This may not work for second-generation instances.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Copy, "GcSqlInstance", DefaultParameterSetName = ParameterSetNames.ByName)]
    public class CopyGcSqlInstanceCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByInstance = "ByInstance";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to be cloned.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0,
            ParameterSetName = ParameterSetNames.ByName)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the Cloud SQL instance to be created as a clone.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public string CloneName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the binary log file for a Cloud SQL instance.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 2)]
        public string BinaryLogFileName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Position (offset) within the binary log file.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 3)]
        public long BinaryLogPosition { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DatabaseInstance that describes the instance we want to remove.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByInstance, Position = 0, Mandatory = true,
            ValueFromPipeline = true)]
        public DatabaseInstance InstanceObject { get; set; }

        protected override void ProcessRecord()
        {
            InstancesCloneRequest body = new InstancesCloneRequest
            {
                CloneContext = new CloneContext
                {
                    BinLogCoordinates = new BinLogCoordinates
                    {
                        BinLogFileName = BinaryLogFileName,
                        BinLogPosition = BinaryLogPosition,
                        Kind = "sql#binLogCoordinates"
                    },
                    Kind = "sql#cloneContext",
                    DestinationInstanceName = CloneName
                }
            };
            string project;
            string instance;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    instance = Instance;
                    project = Project;
                    break;
                case ParameterSetNames.ByInstance:
                    instance = InstanceObject.Name;
                    project = InstanceObject.Project;
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            InstancesResource.CloneRequest request = Service.Instances.Clone(body, project, instance);
            Operation result = request.Execute();
            // It takes a long time to clone the instance, so we skip waiting for the operation and return
            // the pending clone instance.
            DatabaseInstance clone = Service.Instances.Get(project, CloneName).Execute();
            WriteObject(clone);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Exports data from a Cloud SQL instance
    /// to a Google Cloud Storage bucket as a MySQL dump or CSV file. 
    /// </para>
    /// <para type="description">
    /// Exports data from the specified Cloud SQL instance
    /// to a Google Cloud Storage bucket as a MySQL dump or CSV file.
    /// Defaults to a SQL file, but if the CSV Parameter set is used it will export as
    /// a CSV file.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsData.Export, "GcSqlInstance", DefaultParameterSetName = ParameterSetNames.Sql)]
    public class ExportGcSqlInstanceCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string Sql = "SQL";
            public const string Csv = "CSV";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to have data exported.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        ///  The path to the file in Google Cloud Storage where the export will be stored.
        ///  The URI is in the form "gs://bucketName/fileName."
        /// </para><para type="description">
        ///  If the file already exists, the operation will fail.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public string CloudStorageDestination { get; set; }

        /// <summary>
        /// <para type="description">
        /// Export only schemas.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.Sql)]
        public SwitchParameter SchemaOnly { get; set; }

        /// <summary>
        /// <para type="description">
        /// The select query used to extract the data. 
        /// If this is used, a CSV file will be exported, rather than SQL.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 2, ParameterSetName = ParameterSetNames.Csv)]
        public string SelectQuery { get; set; }

        /// <summary>
        /// <para type="description">
        /// Databases (for example, "guestbook" or "orders") from which the export is made.
        /// If fileType is SQL and no database is specified, all databases are exported. 
        /// If fileType is CSV, you can optionally specify at most one database to export.
        /// If exporting as CSV and selectQuery also specifies the database, this field will be ignored.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string[] Database { get; set; }


        /// <summary>
        /// <para type="description">
        /// Tables to export, or that were exported, from the specified database.
        /// If you specify tables, specify one and only one database.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.Sql)]
        public string[] Table { get; set; }


        protected override void ProcessRecord()
        {
            InstancesExportRequest body = new InstancesExportRequest
            {
                ExportContext = new ExportContext
                {
                    Kind = "sql#exportContext",
                    Databases = Database,
                    Uri = CloudStorageDestination,
                    FileType = ParameterSetName.ToString()
                }
            };
            switch (ParameterSetName)
            {
                case ParameterSetNames.Sql:
                    body.ExportContext.SqlExportOptions = new ExportContext.SqlExportOptionsData
                    {
                        SchemaOnly = SchemaOnly,
                        Tables = Table
                    };
                    break;
                case ParameterSetNames.Csv:
                    body.ExportContext.CsvExportOptions = new ExportContext.CsvExportOptionsData
                    {
                        SelectQuery = SelectQuery
                    };
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            InstancesResource.ExportRequest request = Service.Instances.Export(body, Project, Instance);
            Operation result = request.Execute();
            result = WaitForSqlOperation(result);
            if (result.Error != null)
            {
                foreach (OperationError error in result.Error.Errors)
                {
                    throw new GoogleApiException("Google Cloud SQL API", error.Message + error.Code);
                }
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Imports data into a Cloud SQL instance from a MySQL dump 
    /// or CSV file in a Google Cloud Storage bucket. 
    /// </para>
    /// <para type="description">
    /// Imports data into a Cloud SQL instance from a MySQL dump 
    /// or CSV file stored in a Google Cloud Storage bucket.
    /// 
    /// Only one database may be imported from a MySQL file,
    /// and only one table may be imported from a CSV file.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsData.Import, "GcSqlInstance", DefaultParameterSetName = ParameterSetNames.Sql)]
    public class ImportGcSqlInstanceCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string Sql = "SQL";
            public const string Csv = "CSV";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the active cloud sdk config for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to have data exported to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        ///  The path to the file in Google Cloud Storage where the import file is stored.
        ///  The URI is in the form "gs://bucketName/fileName".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public string CloudStorageObject { get; set; }

        /// <summary>
        /// <para type="description">
        ///  The database inside of the Instance (for example, "guestbook" or "orders") to which the import is made.
        ///  It must already exist.
        ///  If filetype is SQL and no database is specified,
        ///  it is assumed that the database is specified in the file to be imported.
        ///  The filetype of the file is assumed to be the corresponding parameter set.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 2)]
        public string Database { get; set; }

        /// <summary>
        /// <para type="description">
        ///  The table to which CSV data is imported. Must be specified for a CSV file.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 3, ParameterSetName = ParameterSetNames.Csv)]
        public string DestinationTable { get; set; }

        /// <summary>
        /// <para type="description">
        ///  The columns of the CSV data to import. If not specified, all columns are imported.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 4, ParameterSetName = ParameterSetNames.Csv)]
        public string[] Column { get; set; }

        protected override void ProcessRecord()
        {
            InstancesImportRequest body = new InstancesImportRequest
            {
                ImportContext = new ImportContext
                {
                    Kind = "sql#importContext",
                    Uri = CloudStorageObject,
                    FileType = ParameterSetName.ToString(),
                    Database = Database,
                }
            };
            if (ParameterSetName == ParameterSetNames.Csv)
            {
                body.ImportContext.CsvImportOptions = new ImportContext.CsvImportOptionsData
                {
                    Columns = Column,
                    Table = DestinationTable
                };
            }
            InstancesResource.ImportRequest request = Service.Instances.Import(body, Project, Instance);
            Operation result = request.Execute();
            result = WaitForSqlOperation(result);
            if (result.Error != null) {
                foreach (OperationError error in result.Error.Errors)
                {
                    throw new GoogleApiException("Google Cloud SQL API", error.Message + error.Code);
                }
            }
        }
    }
}
