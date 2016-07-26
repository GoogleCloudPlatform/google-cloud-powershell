// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.SQLAdmin.v1beta4;
using Google.Apis.SQLAdmin.v1beta4.Data;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// <para type="synopsis">
    /// Restarts a Cloud SQL Instance.
    /// </para>
    /// <para type="description">
    /// Restarts the specified Cloud SQL Instance.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, it will restart the specified Instance in that project. Otherwise, the Project 
    /// defaults to the Cloud SDK config for properties. 
    /// </para>
    /// <example>
    ///   <para> Restart the SQL instance "test1" from the Project "testing."</para>
    ///   <para><code>PS C:\> Restart-GcSqlInstance -Project "testing" -Instance "test1"</code></para>
    ///   <br></br>
    ///   <para>(If successful, the command returns nothing.)</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Restart, "GcSqlInstance")]
    public class RestartGcSqlInstanceCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByInstance = "ByInstance";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project in which the instance resides.
        /// Defaults to the Cloud SDK config for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name/ID of the Instance resource to restart.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        [Alias("Name","Id")]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DatabaseInstance that describes the Instance we want to restart.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByInstance, Mandatory = true, Position = 0, 
                   ValueFromPipeline = true)]
        public DatabaseInstance InstanceObject { get; set; }

        protected override void ProcessRecord()
        {
            string projectName;
            string instanceName;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    projectName = Project;
                    instanceName = Instance;
                    break;
                case ParameterSetNames.ByInstance:
                    projectName = InstanceObject.Project;
                    instanceName = InstanceObject.Name;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            InstancesResource.RestartRequest instRestartRequest = Service.Instances.Restart(projectName, instanceName);
            Operation instRestartResponse = instRestartRequest.Execute();
            WaitForSqlOperation(instRestartResponse);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Starts a Cloud SQL Replica.
    /// </para>
    /// <para type="description">
    /// Starts the specified Cloud SQL Replica.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, it will start the specified Replica in that Project. Otherwise, starts the replica
    /// in the Cloud SDK config project. 
    /// </para>
    /// <example>
    ///   <para>Start the SQL Replica "testRepl1" from the Project "testing."</para>
    ///   <para><code>PS C:\> Start-GcSqlReplica -Project "testing" -Replica "testRepl1"</code></para>
    ///   <br></br>
    ///   <para>(If successful, the command returns nothing.)</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Start, "GcSqlReplica")]
    public class StartGcSqlReplicaCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByInstance = "ByInstance";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project in which the instance Replica resides.
        /// Defaults to the Cloud SDK config for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name/ID of the Replica resource to start.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        public string Replica { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DatabaseInstance that describes the Replica we want to start.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByInstance, Mandatory = true, Position = 0,
                   ValueFromPipeline = true)]
        public DatabaseInstance ReplicaObject { get; set; }

        protected override void ProcessRecord()
        {
            string projectName;
            string replicaName;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    projectName = Project;
                    replicaName = Replica;
                    break;
                case ParameterSetNames.ByInstance:
                    projectName = ReplicaObject.Project;
                    replicaName = ReplicaObject.Name;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            InstancesResource.StartReplicaRequest replStartRequest = 
                Service.Instances.StartReplica(projectName, replicaName);
            Operation replStartResponse = replStartRequest.Execute();
            WaitForSqlOperation(replStartResponse);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Stops a Cloud SQL Replica.
    /// </para>
    /// <para type="description">
    /// Stops the specified Cloud SQL Replica.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, it will stop the specified Replica in that Project. Otherwise, stops the replica
    /// in the Cloud SDK config project. 
    /// </para>
    /// <example>
    ///   <para>Stop the SQL Replica "testRepl1" from the Project "testing."</para>
    ///   <para><code>PS C:\> Stop-GcSqlReplica -Project "testing" -Replica "testRepl1"</code></para>
    ///   <br></br>
    ///   <para>(If successful, the command returns nothing.)</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Stop, "GcSqlReplica", SupportsShouldProcess = true)]
    public class StopGcSqlReplicaCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByInstance = "ByInstance";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project in which the instance Replica resides.
        /// Defaults to the Cloud SDK config for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name/ID of the Replica resource to stop.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        public string Replica { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DatabaseInstance that describes the Replica we want to stop.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByInstance, Mandatory = true, Position = 0,
                   ValueFromPipeline = true)]
        public DatabaseInstance ReplicaObject { get; set; }

        protected override void ProcessRecord()
        {
            string projectName;
            string replicaName;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    projectName = Project;
                    replicaName = Replica;
                    break;
                case ParameterSetNames.ByInstance:
                    projectName = ReplicaObject.Project;
                    replicaName = ReplicaObject.Name;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            if (!ShouldProcess($"{projectName}/{replicaName}", "Stop Replica"))
            {
                return;
            }

            InstancesResource.StopReplicaRequest replStopRequest = 
                Service.Instances.StopReplica(projectName, replicaName);
            Operation replStopResponse = replStopRequest.Execute();
            WaitForSqlOperation(replStopResponse);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Promotes a Cloud SQL Replica.
    /// </para>
    /// <para type="description">
    /// Promotes the specified Cloud SQL Replica to a stand-alone Instance.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, it will promote the specified Replica in that Project. Otherwise, promotes the 
    /// replica in the Cloud SDK config project. 
    /// </para>
    /// <example>
    ///   <para>Promote the SQL Replica "testRepl1" from the Project "testing."</para>
    ///   <para><code>PS C:\> Promote-GcSqlReplica -Project "testing" -Replica "testRepl1"</code></para>
    ///   <br></br>
    ///   <para>(If successful, the command returns nothing.)</para>
    /// </example>
    /// </summary>
    [Cmdlet("Promote", "GcSqlReplica")]
    public class PromoteGcSqlReplicaCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByInstance = "ByInstance";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project in which the instance Replica resides.
        /// Defaults to the Cloud SDK config for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name/ID of the Replica resource to promote.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        public string Replica { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DatabaseInstance that describes the Replica we want to promote.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByInstance, Mandatory = true, Position = 0,
                   ValueFromPipeline = true)]
        public DatabaseInstance ReplicaObject { get; set; }

        protected override void ProcessRecord()
        {
            string projectName;
            string replicaName;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    projectName = Project;
                    replicaName = Replica;
                    break;
                case ParameterSetNames.ByInstance:
                    projectName = ReplicaObject.Project;
                    replicaName = ReplicaObject.Name;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            InstancesResource.PromoteReplicaRequest replPromoteRequest = 
                Service.Instances.PromoteReplica(projectName, replicaName);
            Operation replPromoteResponse = replPromoteRequest.Execute();
            WaitForSqlOperation(replPromoteResponse);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Restores a backup of a Cloud SQL Instance.
    /// </para>
    /// <para type="description">
    /// Restores the specified backup of the specified Cloud SQL Instance.
    /// </para>
    /// <para type="description">
    /// If a BackupInstance is specified, it will restore the specified backup run of that instance to the specified
    /// Instance. Otherwise, it will assume the backup instance is the same as the specified Instance. 
    /// </para>
    /// <para type="description">
    /// If a Project is specified, it will restore the specified backup in that project. Otherwise, restores the 
    /// backup in the Cloud SDK config project. 
    /// </para>
    /// <example>
    ///   <para>
    ///   Restores backup run with id 0 of the SQL Instance "testRepl1" from the Project "testing" to the same SQL
    ///   Instance.
    ///   </para>
    ///   <para><code>
    ///     PS C:\> Restore-GcSqlInstanceBackup -Project "testing" -BackupRunId 1243244 -Instance "testRepl1"
    ///   </code></para>
    ///   <br></br>
    ///   <para>(If successful, the command returns nothing.)</para>
    /// </example>
    /// <example>
    ///   <para>
    ///   Restores backup run with id 0 of the SQL Instance "testRepl2" from the Project "testing" to the SQL Instance 
    ///   "testRepl1" (which must be in the same project).
    ///   </para>
    ///   <para><code>
    ///     PS C:\> Restore-GcSqlInstanceBackup -Project "testing" -BackupRunId 0 -Instance "testRepl1"
    ///     -BackupInstance "testRepl2"
    ///   </code></para>
    ///   <br></br>
    ///   <para>(If successful, the command returns nothing.)</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsData.Restore, "GcSqlInstanceBackup", SupportsShouldProcess = true)]
    public class RestoreGcSqlInstanceBackupCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByInstance = "ByInstance";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project in which the instances to backup to and from reside.
        /// Defaults to the Cloud SDK config for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The id of the BackupRun to restore to. 
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public long BackupRunId { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name/ID of Instance we are restoring the backup to. 
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 1)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DatabaseInstance that describes the Instance we are restoring the backup to. 
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByInstance, Mandatory = true, Position = 1,
                   ValueFromPipeline = true)]
        public DatabaseInstance InstanceObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name/ID of Instance we are backing up from. 
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string BackupInstance { get; set; }

        protected override void ProcessRecord()
        {
            string projectName;
            string instanceName;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    projectName = Project;
                    instanceName = Instance;
                    break;
                case ParameterSetNames.ByInstance:
                    projectName = InstanceObject.Project;
                    instanceName = InstanceObject.Name;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            string backupInstanceName = BackupInstance ?? instanceName;

            if (!ShouldProcess($"{projectName}/{instanceName}, {projectName}/{backupInstanceName}/Backup#{BackupRunId}",
                "Restore Backup"))
            {
                return;
            }

            InstancesRestoreBackupRequest backupRequestBody = new InstancesRestoreBackupRequest
            {
                RestoreBackupContext = new RestoreBackupContext
                {
                    BackupRunId = BackupRunId,
                    InstanceId = backupInstanceName
                }
            };

            InstancesResource.RestoreBackupRequest instRestoreBackupRequest = 
                Service.Instances.RestoreBackup(backupRequestBody, projectName, instanceName);
            Operation instRestoreBackupResponse = instRestoreBackupRequest.Execute();
            WaitForSqlOperation(instRestoreBackupResponse);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Updates settings of a Cloud SQL instance, or patches them.
    /// </para>
    /// <para type="description">
    /// Updates settings of the specified Cloud SQL instance, or patches them. 
    /// If “Update” is true, it will update them. Otherwise it patches.
    /// </para>
    /// <para>
    /// Caution: If "Update" is true, this is not a partial update, so you must include values for all the settings that you want to retain.
    /// </para>
    /// <example>
    ///   <para>
    ///   Patches the SQL Instance "myInstance" (with tier "db-n1-standard-1" and setting version of 15)
    ///   so that it can have maintenance on Monday at 22:00.
    ///   </para>
    ///   <para><code>
    ///     PS C:\> Update-GcSqlInstance "myInstance" "db-n1-standard-1"`
    ///         15 -MaintenanceWindowDay 1 -MaintenanceWindowHour "22:00" -Project "testing" 
    ///   </code></para>
    ///   <br></br>
    ///   <para>(If successful, the command returns the resource for the updated instance.)</para>
    /// </example>
    /// <example>
    ///   <para>
    ///   Updates the SQL Instance "myInstance" (with tier "db-n1-standard-1" and setting version of 18)
    ///   so that its settings default.
    ///   </para>
    ///   <para><code>
    ///     PS C:\> Update-GcSqlInstance "myInstance" "db-n1-standard-1" 18 -Update
    ///   </code></para>
    ///   <br></br>
    ///   <para>(If successful, the command returns the resource for the updated instance.)</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsData.Update, "GcSqlInstance", DefaultParameterSetName = ParameterSetNames.ByName)]
    public class UpdateGcSqlInstanceCmdlet : GcSqlCmdlet
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
        /// The name of the instance to be updated/patched.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0,
            ParameterSetName = ParameterSetNames.ByName)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// The version of instance settings. 
        /// This is a required field to make sure concurrent updates are handled properly.
        /// During update, use the most recent settingsVersion value for the instance and do not try to update this value.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = ParameterSetNames.ByName)]
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.ByInstance)]
        public long SettingsVersion { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DatabaseInstance that describes the instance we want to update/patch.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByInstance, Mandatory = true,
            ValueFromPipeline = true)]
        public DatabaseInstance InstanceObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// If true, updates the instance with only the specified parameters. All other parameters revert back to the default.
        /// If false, follows patch semantics and patches the instance. Unspecified parameters will stay the same.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter Update { get; set; }

        /// <summary>
        /// <para type="description">
        /// The tier of service for this instance, for example "db-n1-standard-1".
        /// Pricing information is available at https://cloud.google.com/sql/pricing.
        /// Get-GcSqlTiers will also tell you what tiers are available for your project.
        /// If not specified, this will be acquired from the instance.
        /// </para>
        /// </summary>
        [Parameter]
        public string Tier { get; set; }

        /// <summary>
        /// <para type="description">
        /// Configuration specific to read replica instances. Indicates whether replication is enabled or not.
        /// </para>
        /// </summary>
        [Parameter]
        public bool DatabaseReplicationEnabled { get; set; }

        public enum ActivationPolicy
        {
            ALWAYS,
            NONE
        }

        /// <summary>
        /// <para type="description">
        /// The activation policy specifies when the instance is activated;
        /// it is applicable only when the instance state is RUNNABLE. Can be ALWAYS, or NEVER. 
        /// </para>
        /// </summary>
        [Parameter]
        public ActivationPolicy Policy { get; set; }

        /// <summary>
        /// <para type="description">
        /// Whether binary log is enabled.
        /// If backup configuration is disabled, binary log must be disabled as well.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public bool BackupBinaryLogEnabled { get; set; }

        /// <summary>
        /// <para type="description">
        /// Whether the backup configuration is enabled or not.
        /// </para>
        /// </summary>
        [Parameter]
        public bool BackupEnabled { get; set; }

        /// <summary>
        /// <para type="description">
        /// Start time for the daily backup configuration in UTC timezone in the 24 hour format - HH:MM
        /// </para>
        /// </summary>
        [Parameter]
        public string BackupStartTime { get; set; }

        /// <summary>
        /// <para type="description">
        /// The size of data disk, in GB. The data disk size minimum is 10 GB. 
        /// Applies only to Second generation instances.
        /// </para>
        /// </summary>
        [Parameter]
        public long DataDiskSizeGb { get; set; }

        /// <summary>
        /// <para type="description">
        /// The database flags passed to the instance at startup.
        /// </para>
        /// </summary>
        [Parameter]
        public DatabaseFlags[] DatabaseFlag { get; set; }

        /// <summary>
        /// <para type="description">
        /// The list of external networks that are allowed to connect to the instance using the IP.
        /// In CIDR notation, also known as 'slash' notation (e.g. 192.168.100.0/24).
        /// May include other ipConfiguration params, but unsure.
        /// </para>
        /// </summary>
        [Parameter]
        public AclEntry[] IpConfigAuthorizedNetwork { get; set; }

        /// <summary>
        /// <para type="description">
        /// Whether the instance should be assigned an IP address or not.
        /// </para>
        /// </summary>
        [Parameter]
        public bool IpConfigIpv4Enabled { get; set; }

        /// <summary>
        /// <para type="description">
        /// Whether the mysqld should default to “REQUIRE X509” for users connecting over IP.
        /// </para>
        /// </summary>
        [Parameter]
        public bool IpConfigRequireSsl { get; set; }

        /// <summary>
        /// <para type="description">
        /// The AppEngine application to follow, it must be in the same region as the Cloud SQL instance.
        /// </para>
        /// </summary>
        [Parameter]
        public string LocationPreferenceFollowGae { get; set; }

        /// <summary>
        /// <para type="description">
        /// The preferred Compute Engine Zone (e.g. us-central1-a, us-central1-b, etc.).
        /// </para>
        /// </summary>
        [Parameter]
        public string LocationPreferenceZone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Day of the week (1-7) starting monday that the instance may be restarted for maintenance purposes.
        /// Applies only to Second Generation instances.
        /// </para>
        /// </summary>
        [Parameter]
        public int MaintenanceWindowDay { get; set; }

        /// <summary>
        /// <para type="description">
        /// Hour of day (0-23) that the instance may be restarted for maintenance purposes.
        /// Applies only to Second Generation instances.
        /// </para>
        /// </summary>
        [Parameter]
        public int MaintenanceWindowHour { get; set; }

        /// <summary>
        /// <para type="description">
        /// Configuration to increase storage size automatically.
        /// Applies only to Second Generation instances.
        /// </para>
        /// </summary>
        [Parameter]
        public bool StorageAutoResize { get; set; }

        public enum DataDiskType
        {
            PD_SSD,
            PD_HDD
        }

        /// <summary>
        /// <para type="description">
        /// The type of data disk: PD_SSD (default) or PD_HDD.
        /// Applies only to Second Generation instances.
        /// </para>
        /// </summary>
        [Parameter]
        public DataDiskType DiskType { get; set; }

        protected override void ProcessRecord()
        {
            string instance;
            string project;
            DatabaseInstance body;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByInstance:
                    {
                        body = InstanceObject;
                        project = InstanceObject.Project;
                        instance = InstanceObject.Name;
                        break;
                    }
                case ParameterSetNames.ByName:
                    {
                        InstancesResource.GetRequest request = Service.Instances.Get(Project, Instance);
                        body = request.Execute();
                        instance = Instance;
                        project = Project;
                        break;
                    }
                default:
                    {
                        throw UnknownParameterSetException;
                    }
            }
            Settings newSettings;
            if (Update)
            {
                newSettings = new Settings
                {
                    BackupConfiguration = new BackupConfiguration(),
                    IpConfiguration = new IpConfiguration(),
                    LocationPreference = new LocationPreference(),
                    MaintenanceWindow = new MaintenanceWindow(),
                };
                newSettings = PopulateSetting(newSettings);
                if (newSettings.Tier == null)
                {
                    newSettings.Tier = body.Settings.Tier;
                }
                body.Settings = newSettings;
                InstancesResource.UpdateRequest request = Service.Instances.Update(body, project, instance);
                Operation result = request.Execute();
                WaitForSqlOperation(result);
            }
            else
            {
                newSettings = PopulateSetting(body.Settings);
                body.Settings = newSettings;
                InstancesResource.PatchRequest request = Service.Instances.Patch(body, project, instance);
                Operation result = request.Execute();
                WaitForSqlOperation(result);
            }
            DatabaseInstance updated = Service.Instances.Get(project, instance).Execute();
            WriteObject(updated);
        }

        private Settings PopulateSetting(Settings newSettings)
        {
            foreach (KeyValuePair<string, object> entry in MyInvocation.BoundParameters)
            {
                string param = entry.Key;
                if (entry.Key.StartsWith("Backup"))
                {
                    param = Regex.Replace(param, @"^Backup", string.Empty);
                    var prop = newSettings.BackupConfiguration.GetType().GetProperty(param);
                    prop.SetValue(newSettings.BackupConfiguration, entry.Value);
                }
                else if (entry.Key.StartsWith("IpConfig"))
                {
                    param = Regex.Replace(param, @"^IpConfig", string.Empty);
                    if (param.Equals("AuthorizedNetwork"))
                    {
                        param = "AuthorizedNetworks";
                    }
                    var prop = newSettings.IpConfiguration.GetType().GetProperty(param);
                    prop.SetValue(newSettings.IpConfiguration, entry.Value);
                }
                else if (entry.Key.StartsWith("LocationPreference"))
                {
                    if (newSettings.LocationPreference == null)
                    {
                        newSettings.LocationPreference = new LocationPreference();
                    }
                    param = Regex.Replace(param, @"^LocationPreference", string.Empty);
                    if (param.Equals("FollowGae"))
                    {
                        param = "FollowGaeApplication";
                    }
                    var prop = newSettings.LocationPreference.GetType().GetProperty(param);
                    prop.SetValue(newSettings.LocationPreference, entry.Value);
                }
                else if (entry.Key.StartsWith("MaintenanceWindow"))
                {
                    if (newSettings.MaintenanceWindow == null)
                    {
                        newSettings.MaintenanceWindow = new MaintenanceWindow();
                    }
                    param = Regex.Replace(param, @"^MaintenanceWindow", string.Empty);
                    var prop = newSettings.MaintenanceWindow.GetType().GetProperty(param);
                    prop.SetValue(newSettings.MaintenanceWindow, entry.Value);
                }
                else
                {
                    //Some parameters aren't used or don't match up perfectly.
                    if (param.Equals("Update") | param.Equals("Project") | param.Equals("Instance")
                        | param.Equals("InstanceObject"))
                    {
                        continue;
                    }
                    else if (param.Equals("DiskType"))
                    {
                        newSettings.DataDiskType = entry.Value.ToString();
                    }
                    else if (param.Equals("Policy"))
                    {
                        newSettings.ActivationPolicy = entry.Value.ToString();
                    }
                    else if (param.Equals("DatabaseFlag"))
                    {
                        DatabaseFlags[] flags = (DatabaseFlags[])entry.Value;
                    }
                    else
                    {
                        var prop = newSettings.GetType().GetProperty(param);
                        prop.SetValue(newSettings, entry.Value);
                    }
                }

            };
            return newSettings;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Failover a Cloud SQL Instance.
    /// </para>
    /// <para type="description">
    /// Failover the specified Cloud SQL Instance to its failover replica instance.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, it will failover the specified Instance in that Project. Otherwise, failsover the 
    /// Instance in the Cloud SDK config project. 
    /// </para>
    /// <example>
    ///   <para>Failover the SQL Instance "test1" in the Project "testing."</para>
    ///   <para><code>PS C:\> Failover-GcSqlReplica -Project "testing" -Instance "test1"</code></para>
    ///   <br></br>
    ///   <para>(If successful, the command returns nothing.)</para>
    /// </example>
    /// <example>
    ///   <para>Failover the SQL Instance "test1" with current settings version 3 in the Project "testing."</para>
    ///   <para>
    ///     <code>PS C:\> Failover-GcSqlReplica -Project "testing" -Instance "test1" - SettingsVersion 3</code>
    ///   </para>
    ///   <br></br>
    ///   <para>(If successful, the command returns nothing.)</para>
    /// </example>
    /// </summary>
    [Cmdlet("Failover", "GcSqlInstance")]
    public class FailoverGcSqlInstanceCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByInstance = "ByInstance";
        }

        private class ErrorCodes
        {
            public const int InvalidSettingsVersion = 412;
        }

        private class ErrorMessages
        {
            public const string InvalidSettingsVersion =
                "Input or retrieved settings version does not match current settings version for this instance.";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project in which the Instance resides.
        /// Defaults to the Cloud SDK config for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name/ID of the Instance resource to failover.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DatabaseInstance that describes the Instance we want to failover.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByInstance, Mandatory = true, Position = 0,
                   ValueFromPipeline = true)]
        public DatabaseInstance InstanceObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// The current settings version of the Instance. If not specified, it will be retrieved from the settings data
        /// of the Instance. 
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public long? SettingsVersion { get; set; }

        protected override void ProcessRecord()
        {
            string projectName;
            string instanceName;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    projectName = Project;
                    instanceName = Instance;
                    break;
                case ParameterSetNames.ByInstance:
                    projectName = InstanceObject.Project;
                    instanceName = InstanceObject.Name;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            InstancesResource.GetRequest instanceGetRequest = Service.Instances.Get(projectName, instanceName);
            DatabaseInstance instanceGetResponse = instanceGetRequest.Execute();

            InstancesFailoverRequest failoverRequestBody = new InstancesFailoverRequest
            {
                FailoverContext = new FailoverContext
                {
                    SettingsVersion = SettingsVersion ?? instanceGetResponse.Settings.SettingsVersion
                }
            };

            InstancesResource.FailoverRequest failoverRequest =
                Service.Instances.Failover(failoverRequestBody, projectName, instanceName);
            Operation failoverResponse;
            try
            {
                failoverResponse = failoverRequest.Execute();
            }
            catch (GoogleApiException failoverEx)
            {
                if (failoverEx.Error.Code == ErrorCodes.InvalidSettingsVersion)
                {
                    throw new GoogleApiException("Google Cloud SQL API", failoverEx.Message +
                                                 ErrorMessages.InvalidSettingsVersion);
                }

                throw failoverEx;
            }
            WaitForSqlOperation(failoverResponse);

            // Wait for recreate operation in failover replica.
            OperationsResource.ListRequest opListRequest =
                Service.Operations.List(projectName, instanceGetResponse.FailoverReplica.Name);
            do
            {
                OperationsListResponse opListResponse = opListRequest.Execute();
                if (opListResponse.Items == null)
                {
                    return;
                }
                foreach (Operation operation in opListResponse.Items)
                {
                    if (operation.OperationType == "RECREATE_REPLICA")
                    {
                        WaitForSqlOperation(operation);
                        return;
                    }
                }
                opListRequest.PageToken = opListResponse.NextPageToken;
            }
            while (opListRequest.PageToken != null);
        }
    }
}
