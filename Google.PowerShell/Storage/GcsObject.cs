// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Services;
using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.PowerShell.Common;
using System.IO;
using System.Management.Automation;
using System.Net;

namespace Google.PowerShell.CloudStorage
{
    /// <summary>
    /// New-GcsBucket creates a new Google Cloud Storage object with the given name.
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcsObject")]
    public class NewGcsObjectCmdlet : GcsCmdlet
    {
        /// <summary>
        /// The name of the bucket for the object.
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Bucket { get; set; }

        /// <summary>
        /// Object name.
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public string ObjectName { get; set; }

        [Parameter(Position = 2, Mandatory = true)]
        public string FilePath { get; set; }

        // TODO(chrsmith): ParameterSet that supports just the object name, with:
        // gs://<bucket>/<object> syntax?

        [Parameter(Mandatory = false)]
        public string ContentType { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            if (string.IsNullOrEmpty(ContentType))
            {
                ContentType = "application/octet-stream";
            }

            string qualifiedPath = Path.GetFullPath(FilePath);
            if (!File.Exists(qualifiedPath))
            {
                throw new FileNotFoundException("File Not Found", qualifiedPath);
            }

            using (var fileStream = new FileStream(qualifiedPath, FileMode.Open))
            {
                Object newGcsObject = new Object
                {
                    Bucket = Bucket,
                    Name = ObjectName,
                    ContentType = ContentType
                };

                ObjectsResource.InsertMediaUpload insertReq = service.Objects.Insert(
                    newGcsObject, Bucket, fileStream, ContentType);

                var finalProgress = insertReq.Upload();
                if (finalProgress.Exception != null)
                {
                    throw finalProgress.Exception;
                }

                if (insertReq.ResponseBody != null)
                {
                    WriteObject(insertReq.ResponseBody);
                }
            }
        }
    }

    /// <summary>
    /// Get-GcsBucket returns the Google Cloud Storage Object metadata with the given name, or all
    /// objects within a particular bucket.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcsObject")]
    public class GetGcsObjectCmdlet : GcsCmdlet
    {
        /// <summary>
        /// The name of the bucket to check.
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Bucket { get; set; }

        /// <summary>
        /// Object name.
        /// </summary>
        [Parameter(Position = 1, Mandatory = false)]
        public string ObjectName { get; set; }

        // TODO(chrsmith): ParameterSet that supports just the object name, with:
        // gs://<bucket>/<object> syntax.

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            if (ObjectName != null)
            {
                ObjectsResource.GetRequest getReq = service.Objects.Get(Bucket, ObjectName);
                Object gcsObject = getReq.Execute();
                WriteObject(gcsObject);
            }
            else
            {
                // TODO(chrsmith): Provide search filters.
                ObjectsResource.ListRequest listReq = service.Objects.List(Bucket);
                Objects objects = listReq.Execute();
                // TODO(chris): This will only work if there is a single page of items. If there are
                // hundreds of objects the list will be incomplete.
                WriteObject(objects.Items, true);
            }
        }
    }

    /// <summary>
    /// Remove-GcsObject deletes the specified GCS Object.
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsObject")]
    public class RemoveGcsObjectCmdlet : GcsCmdlet
    {
        /// <summary>
        /// The name of the bucket to check.
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Bucket { get; set; }

        /// <summary>
        /// Object name.
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public string ObjectName { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            ObjectsResource.DeleteRequest delReq = service.Objects.Delete(Bucket, ObjectName);
            string result = delReq.Execute();
            WriteObject(result);
        }
    }
}
