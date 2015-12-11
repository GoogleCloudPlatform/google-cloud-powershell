// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System.Management.Automation;

using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Services;
using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using System.Net;

using Google.PowerShell.Common;

namespace Google.PowerShell.CloudStorage
{
    /// <summary>
    /// Get-GcsBucket returns the Google Cloud Storage bucket with the given name.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcsBucket", DefaultParameterSetName = "SingleBucket")]
    public class GetGcsBucketCmdlet : GcsCmdlet
    {
        /// <summary>
        /// The name of the bucket to return.
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "SingleBucket")]
        public string Name { get; set; }

        /// <summary>
        /// The name of the project associated with the command. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "BucketsByProject")]
        public string Project { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            if (ParameterSetName == "SingleBucket")
            {
                BucketsResource.GetRequest req = service.Buckets.Get(Name);
                req.Projection = BucketsResource.GetRequest.ProjectionEnum.Full;
                Bucket bucket = req.Execute();
                WriteObject(bucket);
            }

            if (ParameterSetName == "BucketsByProject")
            {
                var req = service.Buckets.List(Project ?? CloudSdkSettings.GetDefaultProject());
                req.Projection = BucketsResource.ListRequest.ProjectionEnum.Full;
                Buckets buckets = req.Execute();
                WriteObject(buckets.Items, true);
            }
        }
    }

    [Cmdlet(VerbsCommon.New, "GcsBucket")]
    public class NewGcsBucketCmdlet : GcsCmdlet
    {
        [Parameter(Position = 0, Mandatory = true)]
        public string Name { get; set; }

        /// <summary>
        /// The name of the project associated with the command. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </summary>
        [Parameter(Position = 1, Mandatory = false)]
        public string Project { get; set; }

        [Parameter(Mandatory = false)]
        [ValidateSet("DURABLE_REDUCED_AVAILABILITY", "NEARLINE", "STANDARD", IgnoreCase = true)]
        public string StorageClass { get; set; }

        [Parameter(Mandatory = false)]
        [ValidateSet("ASIA", "EU", "US", IgnoreCase = false)]
        public string Location { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            // While bucket has many properties, these are the only ones
            // that can be set as part of an INSERT operation.
            // https://cloud.google.com/storage/docs/xml-api/put-bucket-create
            // TODO(chrsmith): Wire in ACL-related parameters.
            var bucket = new Google.Apis.Storage.v1.Data.Bucket();
            bucket.Name = Name;
            bucket.Location = Location;
            bucket.StorageClass = StorageClass;

            Bucket result = service.Buckets.Insert(bucket, Project).Execute();
            WriteObject(result);
        }
    }

    [Cmdlet(VerbsCommon.Remove, "GcsBucket", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public class RemoveGcsBucketCmdlet : GcsCmdlet
    {
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// Delete the objects too. By default, you cannot delete non-empty buckets.
        /// </summary>
        [Parameter]
        public SwitchParameter DeleteObjects { get; set; }

        [Parameter]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (!base.ConfirmAction(Force.IsPresent, Name, "Remove-GcsBucket (DeleteBucket)"))
            {
                return;
            }

            // TODO(chrsmith): What is the idiomatic way to support the WhatIf flag. Print
            // what objects would have been deleted?
            var service = GetStorageService();
            try
            {
                service.Buckets.Delete(Name).Execute();
            }
            // TODO(chrsmith): What is the specific exception type?
            catch (GoogleApiException re)
            {
                if (re.HttpStatusCode == HttpStatusCode.Conflict)
                {
                    WriteVerbose("Got RequestError[409]. Bucket not empty.");
                }
                if (!DeleteObjects.IsPresent)
                {
                    throw;
                }
                // TODO(chrsmith): Provide some progress output? Deleting thousands of GCS objects takes a while.
                // TODO(chrsmith): Multi-threaded delete? e.g. the -m parameter to gsutil?
                // TODO(chrsmith): What about buckets with TONS of objects, and paging?
                Objects bucketObjects = service.Objects.List(Name).Execute();
                foreach (Apis.Storage.v1.Data.Object bucketObject in bucketObjects.Items)
                {
                    service.Objects.Delete(Name, bucketObject.Name).Execute();
                }

                service.Buckets.Delete(Name).Execute();
            }
        }
    }

    /// <summary>
    /// Test-GcsBucket tests if a bucket with the given name already exists.
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "GcsBucket")]
    public class TestGcsBucketCmdlet : GcsCmdlet
    {
        /// <summary>
        /// The name of the bucket to test for.
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            try
            {
                service.Buckets.Get(Name).Execute();
                WriteObject(true);
            }
            catch (GoogleApiException ex)
            {
                if (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    WriteObject(false);
                }
                else
                {
                    WriteObject(true);
                }
            }
        }
    }
}
