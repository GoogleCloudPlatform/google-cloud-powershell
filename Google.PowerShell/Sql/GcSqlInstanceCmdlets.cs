// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Services;
using Google.Apis.SQLAdmin.v1beta4;
using Google.Apis.SQLAdmin.v1beta4.Data;
using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Reflection;
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
    /// <example>
    ///   <code>PS C:\> Get-GcSqlInstance</code>
    ///   <para>Gets a list of instances in the project set in gcloud config.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcSqlInstance "myInstance"</code>
    ///   <para>Gets a resource for the instance named "myInstance" in our project.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlInstance", DefaultParameterSetName = ParameterSetNames.GetList)]
    [OutputType(typeof(DatabaseInstance))]
    public class GetGcSqlInstanceCmdlet : GcSqlCmdlet
    {
        internal class ParameterSetNames
        {
            public const string GetSingle = "Single";
            public const string GetList = "List";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

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
    /// <example>
    ///   <code>PS C:\> Add-GcSqlInstance $myInstance</code>
    ///   <para>Adds the instance represented by $myInstance to our project set in gcloud config.</para>
    ///   <para>If successful, the command returns a resource for the added instance.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Add-GcSqlInstance "gootoso" -Project "myproject"</code>
    ///   <para>Adds a default instance named "gootoso" to the project "myproject"</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/tools/powershell/docs/sql/setup)">
    ///   [Setting up Instances]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcSqlInstance")]
    [OutputType(typeof(DatabaseInstance))]
    public class AddGcSqlInstanceCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string Default = "Default";
            public const string ByConfig = "ByConfig";
        }
        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The instance resource, which can be created with New-GcSqlInstanceConfig.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.ByConfig)]
        public DatabaseInstance InstanceConfig { get; set; }

        /// <summary>
        /// <para type="description">
        /// The instance resource, which can be created with New-GcSqlInstanceConfig.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0,
            ParameterSetName = ParameterSetNames.Default)]
        public string Name { get; set; }


        /// <summary>
        /// Creates a default Google Cloud SQL Database instance.
        /// The defaults here are hard-coded, and might become out of date with
        /// the defaults provided by Pantheon the Cloud SDK, etc.
        /// </summary>
        private DatabaseInstance CreateDefaultInstance()
        {
            return new DatabaseInstance
            {
                Settings = new Settings
                {
                    Tier = "db-n1-standard-1",
                    PricingPlan = "PER_USE",
                    ActivationPolicy = "ALWAYS",
                    BackupConfiguration = new BackupConfiguration
                    {
                        BinaryLogEnabled = true,
                        Enabled = true,
                        StartTime = "22:00"
                    },
                    DataDiskSizeGb = 10,
                    IpConfiguration = new IpConfiguration
                    {
                        Ipv4Enabled = false,
                        RequireSsl = false
                    },
                    LocationPreference = new LocationPreference(),
                    MaintenanceWindow = new MaintenanceWindow
                    {
                        Day = 5,
                        Hour = 22,
                    },
                    StorageAutoResize = false,
                    DataDiskType = "PD_SSD",
                    ReplicationType = "SYNCHRONOUS"
                },
                Name = Name,
                Region = "us-central1",
                BackendType = "SECOND_GEN",
                Project = Project,
                DatabaseVersion = "MYSQL_5_7",
                InstanceType = "CLOUD_SQL_INSTANCE",
                State = "RUNNABLE"
            };
        }

        protected override void ProcessRecord()
        {
            DatabaseInstance instance;
            switch (ParameterSetName)
            {
                case ParameterSetNames.Default:
                    instance = CreateDefaultInstance();
                    break;
                case ParameterSetNames.ByConfig:
                    instance = InstanceConfig;
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            instance.Project = Project;
            InstancesResource.InsertRequest request = Service.Instances.Insert(instance, Project);
            WriteVerbose($"Adding instance '{instance.Name}' to Project '{Project}'.");
            Operation result = request.Execute();
            WaitForSqlOperation(result);
            // We get the instance that was just added so that the returned DatabaseInstance is as
            // accurate as possible.
            InstancesResource.GetRequest instanceRequest = Service.Instances.Get(Project, instance.Name);
            WriteObject(instanceRequest.Execute());
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes a Cloud SQL instance.
    /// </para>
    /// <para type="description">
    /// Deletes the specified Cloud SQL instance. Warning: This deletes all data inside of it as
    /// well.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcSqlInstance "myInstance"</code>
    ///   <para>Removes the instance called "myInstance" from our project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcSqlInstance $myInstance</code>
    ///   <para>Removes the instance represented by the resource $myInstance from our project.</para>
    /// </example>
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
        public override string Project { get; set; }

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
            if (ShouldProcess($"{project}/{instance}", "Delete Instance"))
            {
                InstancesResource.DeleteRequest request = Service.Instances.Delete(project, instance);
                WriteVerbose($"Removing instance '{instance}' from Project '{project}'.");
                Operation result = request.Execute();
                WaitForSqlOperation(result);
            }
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
    /// <example>
    ///   <code>PS C:\> Export-GcSqlInstance "myInstance" "gs://bucket/file.gz"</code>
    ///   <para>
    ///   Exports the databases inside the instance "myInstance" to the Cloud Storage bucket file "gs://bucket/file.gz"
    ///   as a MySQL dump file.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///     PS C:\> Export-GcSqlInstance "myInstance" "gs://bucket/file.csv" "SELECT * FROM data.table"
    ///   </code>
    ///   <br></br>
    ///   <para>
    ///   Exports the databases inside the instance "myInstance" to the Cloud Storage bucket file "gs://bucket/file.csv"
    ///   as a CSV file with the select query "SELECT * FROM data.table"
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Export-GcSqlInstance "myInstance" "gs://bucket/file.csv" -Database "myData","myData2"</code>
    ///   <br></br>
    ///   <para>
    ///   Exports the databases "myData" and "myData2" inside the instance "myInstance"
    ///   to the Cloud Storage bucket file "gs://bucket/file.gz" as a MySQL dump file.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/tools/powershell/docs/sql/import-export)">
    ///   [How-To: Importing and Exporting]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/sql/docs/import-export/)">
    ///   [Overview of Importing and Exporting]
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
        public override string Project { get; set; }

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
            WriteVerbose($"Exporting to '{CloudStorageDestination}' from Instance '{Instance}'.");
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
    /// or CSV file stored either in a Google Cloud Storage bucket or on your local machine.
    /// </para>
    /// <para type="description">
    /// Imports data into a Cloud SQL instance from a MySQL dump 
    /// or CSV file stored either in a Google Cloud Storage bucket or on your local machine.
    /// 
    /// Only one database may be imported from a MySQL file,
    /// and only one table may be imported from a CSV file.
    /// </para>
    /// <para type="description">
    /// WARNING: Standard charging rates apply if a file is imported from your local machine.
    /// A Google Cloud Storage bucket will be set up, uploaded to, and imported from during the import process.
    /// It is deleted after the upload and/or import process fails or is completed
    /// </para>
    /// <example>
    ///   <code>
    ///     PS C:\> Import-GcSqlInstance "myInstance" "gs://bucket/file" "myData"
    ///   </code>
    ///   <para>
    ///   Imports the MySQL dump file at "gs://bucket/file" into the already
    ///   existing database "myData" in the instance "myInstance".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///     PS C:\> Import-GcSqlInstance "myInstance" "gs://bucket/file.csv" "myData" "myTable"
    ///   </code>
    ///   <para>
    ///   Imports the CSV file at "gs://bucket/file.csv" into the table "myTable" in the already
    ///   existing database "myData" in the instance "myInstance".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///     PS C:\> Import-GcSqlInstance "myInstance" "C:\Users\Bob\file.csv" "myData" "myTable" 
    ///   </code>
    ///   <para>
    ///   Imports the CSV file at "C:\Users\Bob\file.csv" into the table "myTable" in the already
    ///   existing database "myData" in the instance "myInstance".
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/tools/powershell/docs/sql/import-export)">
    ///   [How-To: Importing and Exporting]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/sql/docs/import-export/)">
    ///   [Overview of Importing and Exporting]
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
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to have data exported to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        ///  The path to the file where the import file is stored.
        ///  A Google Cloud Storage path is in the form "gs://bucketName/fileName".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public string ImportFilePath { get; set; }

        /// <summary>
        /// <para type="description">
        ///  The database inside of the Instance (for example, "guestbook" or "orders") to which the import is made.
        ///  It must already exist.
        /// </para>
        /// <para type="description">
        ///  If filetype is SQL and no database is specified, it is assumed that the database is specified in the
        ///  file to be imported. The filetype of the file is assumed to be the corresponding parameter set.
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

        /// <summary>
        /// Class containing the local file upload methods.
        /// </summary>
        private class GcsFileUploader
        {
            private StorageService _bucketService;
            private string _project;

            public GcsFileUploader(BaseClientService.Initializer serviceInitializer, string project)
            {
                _bucketService = new StorageService(serviceInitializer);
                _project = project;
            }

            /// <summary>
            /// Creates a Google Cloud Storage bucket.
            /// </summary>
            /// <param name="bucketName"></param>
            /// <returns></returns>
            public Bucket CreateBucket(string bucketName)
            {
                Bucket bucket = new Google.Apis.Storage.v1.Data.Bucket();
                bucket.Name = bucketName;
                return _bucketService.Buckets.Insert(bucket, _project).Execute();
            }

            /// <summary>
            /// Uploads a local file to a given bucket. The object's name will be the same as
            /// provided file path. (e.g. "C:\foo\bar.txt".)
            /// </summary>
            public Apis.Storage.v1.Data.Object UploadLocalFile(string filePath, string bucketName)
            {
                string fileName = "toImport";
                using (Stream contentStream = new FileStream(filePath, FileMode.Open))
                {
                    Apis.Storage.v1.Data.Object newGcsObject = new Apis.Storage.v1.Data.Object
                    {
                        Bucket = bucketName,
                        Name = fileName,
                        ContentType = "application/octet-stream"
                    };
                    ObjectsResource.InsertMediaUpload insertReq = _bucketService.Objects.Insert(
                        newGcsObject, bucketName, contentStream, "application/octet-stream");
                    var finalProgress = insertReq.Upload();
                    if (finalProgress.Exception != null)
                    {
                        throw finalProgress.Exception;
                    }
                }

                return _bucketService.Objects.Get(bucketName, fileName).Execute();
            }

            /// <summary>
            /// Adjusts the ACL for an uploaded object so that a SQL instance can access it.
            /// </summary>
            public void AdjustAcl(Apis.Storage.v1.Data.Object bucketObject, string instanceEmail)
            {
                ObjectAccessControl body = new ObjectAccessControl();
                body.Bucket = bucketObject.Bucket;
                body.Entity = "user-" + instanceEmail;
                body.Role = "OWNER";
                body.Object__ = bucketObject.Name;
                ObjectAccessControlsResource.InsertRequest aclRequest =
                    _bucketService.ObjectAccessControls.Insert(body, bucketObject.Bucket, bucketObject.Name);
                try
                {
                    aclRequest.Execute();
                }
                catch (Exception e)
                {
                    DeleteObject(bucketObject);
                    _bucketService.Buckets.Delete(bucketObject.Bucket).Execute();
                    throw e;
                }
            }

            /// <summary>
            /// Deletes the bucket object from the Google Cloud Storage bucket.
            /// </summary>
            public void DeleteObject(Apis.Storage.v1.Data.Object bucketObject)
            {
                _bucketService.Objects.Delete(bucketObject.Bucket, bucketObject.Name).Execute();
            }

            /// <summary>
            /// Deletes a Google Cloud Storage bucket.
            /// </summary>
            public void DeleteBucket(Bucket bucket)
            {
                _bucketService.Buckets.Delete(bucket.Name).Execute();
            }
        }

        private Bucket _tempGcsBucket = null;
        private Apis.Storage.v1.Data.Object _tempGcsObject = null;
        private GcsFileUploader _tempUploader = null;

        protected override void ProcessRecord()
        {
            if (!ImportFilePath.StartsWith("gs://"))
            {
                if (ShouldProcess($"{Project}/{Instance}/{ImportFilePath}",
                    "Create a new Google Cloud Storage bucket and upload the file to it for import.",
                    "Will be deleted after the import completes"))
                {
                    _tempUploader = new GcsFileUploader(GetBaseClientServiceInitializer(), Project);
                    Random rnd = new Random();
                    int bucketRnd = rnd.Next(1000000);
                    string bucketName = "import" + bucketRnd.ToString();
                    WriteVerbose($"Creating a Google Cloud Storage Bucket for the file at {ImportFilePath}.");
                    _tempGcsBucket = _tempUploader.CreateBucket(bucketName);
                    try
                    {
                        WriteVerbose($"Uploading the file at {ImportFilePath} to the new Google Cloud Storage Bucket.");
                        _tempGcsObject = _tempUploader.UploadLocalFile(ImportFilePath, bucketName);
                    }
                    catch (Exception e)
                    {
                        _tempUploader.DeleteBucket(_tempGcsBucket);
                        throw e;
                    }
                    DatabaseInstance myInstance = Service.Instances.Get(Project, Instance).Execute();
                    WriteVerbose("Updating the permissions for the uploaded file.");
                    _tempUploader.AdjustAcl(_tempGcsObject, myInstance.ServiceAccountEmailAddress);
                    ImportFilePath = string.Format("gs://{0}/{1}", bucketName, "toImport");
                }
                else return;
            }

            InstancesImportRequest body = new InstancesImportRequest
            {
                ImportContext = new ImportContext
                {
                    Kind = "sql#importContext",
                    Uri = ImportFilePath,
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
            WriteVerbose($"Importing the file at '{ImportFilePath}' to Instance '{Instance}'.");
            Operation result = request.Execute();
            result = WaitForSqlOperation(result);
            if (_tempUploader != null)
            {
                WriteVerbose("Deleting the Google Cloud Storage Bucket that was created, along with uploaded file.");
                _tempUploader.DeleteObject(_tempGcsObject);
                _tempUploader.DeleteBucket(_tempGcsBucket);
            }
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
    ///   <code>PS C:\> Restart-GcSqlInstance -Project "testing" -Instance "test1"</code>
    ///   <para>Restart the SQL instance "test1" from the Project "testing."</para>
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
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name/ID of the Instance resource to restart.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        [Alias("Name", "Id")]
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
            WriteVerbose($"Restarting the Instance '{instanceName}' in Project '{projectName}'.");
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
    ///   <code>PS C:\> Start-GcSqlReplica -Project "testing" -Replica "testRepl1"</code>
    ///   <para>Start the SQL Replica "testRepl1" from the Project "testing."</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/tools/powershell/docs/sql/replica)">[Replica Instances]</para>
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
        public override string Project { get; set; }

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
            WriteVerbose($"Starting the Read-Replica Instance '{replicaName}' in Project '{projectName}'.");
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
    ///   <code>PS C:\> Stop-GcSqlReplica -Project "testing" -Replica "testRepl1"</code>
    ///   <para>Stop the SQL Replica "testRepl1" from the Project "testing."</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/tools/powershell/docs/sql/replica)">[Replica Instances]</para>
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
        public override string Project { get; set; }

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

            if (ShouldProcess($"{projectName}/{replicaName}", "Stop Replica"))
            {
                InstancesResource.StopReplicaRequest replStopRequest =
                    Service.Instances.StopReplica(projectName, replicaName);
                WriteVerbose($"Stopping the Read-Replica Instance '{replicaName}' in Project '{projectName}'.");
                Operation replStopResponse = replStopRequest.Execute();
                WaitForSqlOperation(replStopResponse);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Convert a Cloud SQL Replica to an SQL Instance.
    /// </para>
    /// <para type="description">
    /// Convert the specified Cloud SQL Replica to a stand-alone Instance.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, it will promote the specified Replica in that Project. Otherwise, promotes the 
    /// replica in the Cloud SDK config project. 
    /// </para>
    /// <example>
    ///   <code>PS C:\> ConvertTo-GcSqlInstance -Project "testing" -Replica "testRepl1"</code>
    ///   <para>Convert the SQL Replica "testRepl1" from the Project "testing."</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/tools/powershell/docs/sql/replica)">[Replica Instances]</para>
    /// </summary>
    [Cmdlet(VerbsData.ConvertTo, "GcSqlInstance")]
    public class ConvertToGcSqlInstanceCmdlet : GcSqlCmdlet
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
        public override string Project { get; set; }

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
            WriteVerbose($"Promoting the Read-Replica Instance '{replicaName}' in Project '{projectName}'.");
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
    ///   <code>
    ///     PS C:\> Restore-GcSqlInstanceBackup -Project "testing" -BackupRunId 1243244 -Instance "testRepl1"
    ///   </code>
    ///   <para>
    ///   Restores backup run with id 0 of the SQL Instance "testRepl1" from the Project "testing" to the same SQL
    ///   Instance.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///     PS C:\> Restore-GcSqlInstanceBackup -Project "testing" -BackupRunId 0 -Instance "testRepl1" `
    ///                                         -BackupInstance "testRepl2"
    ///   </code>
    ///   <para>
    ///   Restores backup run with id 0 of the SQL Instance "testRepl2" from the Project "testing" to the SQL Instance 
    ///   "testRepl1" (which must be in the same project).
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/tools/powershell/docs/sql/backup)">
    ///   [Managing Backups]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/sql/docs/backup-recovery/backups)">
    ///   [Overview of Backups]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/sql/docs/backup-recovery/restore)">
    ///   [Overview of Restoring an instance]
    /// </para>
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
        public override string Project { get; set; }

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
        [Parameter(ParameterSetName = ParameterSetNames.ByInstance, Mandatory = true,
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

            if (ShouldProcess($"{projectName}/{instanceName}, {projectName}/{backupInstanceName}/Backup#{BackupRunId}",
                "Restore Backup"))
            {
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
                WriteVerbose($"Restoring the Instance '{instanceName}'" +
                    $"in Project '{projectName}' to its Backup '{BackupRunId}'.");
                Operation instRestoreBackupResponse = instRestoreBackupRequest.Execute();
                WaitForSqlOperation(instRestoreBackupResponse);
            }
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
    ///   <code>
    ///     PS C:\> Update-GcSqlInstance "myInstance" `
    ///         15 -MaintenanceWindowDay 1 -MaintenanceWindowHour "22:00" -Project "testing" 
    ///   </code>
    ///   <para>
    ///   Patches the SQL Instance "myInstance" (with setting version of 15)
    ///   so that it can have maintenance on Monday at 22:00.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///     PS C:\> Update-GcSqlInstance "myInstance" 18 -Update
    ///   </code>
    ///   <para>
    ///   Updates the SQL Instance "myInstance" (with and setting version of 18)
    ///   so that its settings default.
    ///   </para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsData.Update, "GcSqlInstance", DefaultParameterSetName = ParameterSetNames.ByName)]
    [OutputType(typeof(DatabaseInstance))]
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
        public override string Project { get; set; }

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
        /// The version of instance settings. Required field to make sure concurrent updates are handled properly.
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
        /// Whether binary log is enabled. If backup configuration is disabled, binary log must be
        /// disabled as well.
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

        // TODO(chrsmith): From marcel: "May include other ipConfiguration params, but unsure."
        /// <summary>
        /// <para type="description">
        /// The list of external networks that are allowed to connect to the instance using the IP.
        /// In CIDR notation, also known as 'slash' notation (e.g. "192.168.100.0/24").
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
                WriteVerbose($"Setting up the updated settings for the instance '{instance}'.");
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
                WriteVerbose($"Setting up the updated settings for the instance '{instance}'.");
                newSettings = PopulateSetting(body.Settings);
                body.Settings = newSettings;
                InstancesResource.PatchRequest request = Service.Instances.Patch(body, project, instance);
                Operation result = request.Execute();
                WaitForSqlOperation(result);
            }
            WriteVerbose($"Updating the Instance '{instance}' in Project '{project}'.");
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
                    var prop = newSettings.BackupConfiguration.GetType().GetTypeInfo().GetProperty(param);
                    prop.SetValue(newSettings.BackupConfiguration, entry.Value);
                }
                else if (entry.Key.StartsWith("IpConfig"))
                {
                    param = Regex.Replace(param, @"^IpConfig", string.Empty);
                    if (param.Equals("AuthorizedNetwork"))
                    {
                        param = "AuthorizedNetworks";
                    }
                    var prop = newSettings.IpConfiguration.GetType().GetTypeInfo().GetProperty(param);
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
                    var prop = newSettings.LocationPreference.GetType().GetTypeInfo().GetProperty(param);
                    prop.SetValue(newSettings.LocationPreference, entry.Value);
                }
                else if (entry.Key.StartsWith("MaintenanceWindow"))
                {
                    if (newSettings.MaintenanceWindow == null)
                    {
                        newSettings.MaintenanceWindow = new MaintenanceWindow();
                    }
                    param = Regex.Replace(param, @"^MaintenanceWindow", string.Empty);
                    var prop = newSettings.MaintenanceWindow.GetType().GetTypeInfo().GetProperty(param);
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
                        var prop = newSettings.GetType().GetTypeInfo().GetProperty(param);
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
    ///   <code>PS C:\> Invoke-GcSqlInstanceFailover -Project "testing" -Instance "test1"</code>
    ///   <para>Failover the SQL Instance "test1" in the Project "testing."</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Invoke-GcSqlInstanceFailover -Project "testing" -Instance "test1" - SettingsVersion 3</code>
    ///   <para>Failover the SQL Instance "test1" with current settings version 3 in the Project "testing."</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/tools/powershell/docs/sql/replica)">[Replica Instances]</para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "GcSqlInstanceFailover")]
    public class InvokeGcSqlInstanceFailoverCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByInstance = "ByInstance";
        }

        private const int InvalidSettingsVersionErrCode = 412;
        private const string InvalidSettingsVersionErrMsg =
            "Input or retrieved settings version does not match current settings version for this instance.";

        /// <summary>
        /// <para type="description">
        /// Name of the project in which the Instance resides.
        /// Defaults to the Cloud SDK config project if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

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
        /// The current settings version of the Instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 1)]
        public long? SettingsVersion { get; set; }

        protected override void ProcessRecord()
        {
            string projectName;
            string instanceName;
            DatabaseInstance instanceObject;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    projectName = Project;
                    instanceName = Instance;
                    InstancesResource.GetRequest instanceGetRequest = Service.Instances.Get(projectName, instanceName);
                    instanceObject = instanceGetRequest.Execute();
                    break;
                case ParameterSetNames.ByInstance:
                    projectName = InstanceObject.Project;
                    instanceName = InstanceObject.Name;
                    SettingsVersion = InstanceObject.Settings.SettingsVersion;
                    instanceObject = InstanceObject;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            InstancesFailoverRequest failoverRequestBody = new InstancesFailoverRequest
            {
                FailoverContext = new FailoverContext
                {
                    SettingsVersion = SettingsVersion
                }
            };

            InstancesResource.FailoverRequest failoverRequest =
                Service.Instances.Failover(failoverRequestBody, projectName, instanceName);
            Operation failoverResponse;
            WriteVerbose($"Activating the Failover for the Instance '{instanceName}'.");
            try
            {
                failoverResponse = failoverRequest.Execute();
            }
            catch (GoogleApiException failoverEx)
            {
                if (failoverEx.Error.Code == InvalidSettingsVersionErrCode)
                {
                    throw new GoogleApiException("Google Cloud SQL API", failoverEx.Message +
                                                 InvalidSettingsVersionErrMsg);
                }

                throw failoverEx;
            }
            WaitForSqlOperation(failoverResponse);

            // Wait for recreate operation in failover replica.
            OperationsResource.ListRequest opListRequest =
                Service.Operations.List(projectName, instanceObject.FailoverReplica.Name);
            WriteVerbose("Waiting for the Failover to be re-created.");
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
