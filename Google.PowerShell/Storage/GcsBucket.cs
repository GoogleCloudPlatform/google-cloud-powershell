// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net;
using System.Threading.Tasks;

namespace Google.PowerShell.CloudStorage
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Cloud Storage buckets
    /// </para>
    /// <para type="description">
    /// If a name is specified, gets the Google Cloud Storage bucket with the given name. The gcloud user must
    /// have access to view the bucket.
    /// </para>
    /// <para type="description">
    /// If a name is not specified, gets all Google Cloud Storage buckets owned by a project. The project can
    /// be specifed. If it is not, the project in the active Cloud SDK configuration will be used. The gcloud
    /// user must have access to view the project.
    /// </para>
    /// <example>
    ///   <para>Get the bucket named "widget-co-logs".</para>
    ///   <para><code>Get-GcsBucket "widget-co-logs"</code></para>
    /// </example>
    /// <example>
    ///   <para>Get all buckets for project "widget-co".</para>
    ///   <para><code>Get-GcsBucket -Project "widget-co"</code></para>
    /// </example>
    /// <example>
    ///   <para>Get all buckets for current project in the active gcloud configuration.</para>
    ///   <para><code>Get-GcsBucket</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcsBucket", DefaultParameterSetName = ParameterSetNames.BucketsByProject)]
    [OutputType(typeof(Bucket))]
    public class GetGcsBucketCmdlet : GcsCmdlet
    {
        private class ParameterSetNames
        {
            public const string SingleBucket = "SingleBucket";
            public const string BucketsByProject = "BucketsByProject";
        }
        /// <summary>
        /// <para type="description">
        /// The name of the bucket to return.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.SingleBucket)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The project to check for Storage buckets. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.BucketsByProject)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            if (ParameterSetName == ParameterSetNames.SingleBucket)
            {
                BucketsResource.GetRequest req = service.Buckets.Get(Name);
                req.Projection = BucketsResource.GetRequest.ProjectionEnum.Full;
                Bucket bucket = req.Execute();
                WriteObject(bucket);
            }

            if (ParameterSetName == ParameterSetNames.BucketsByProject)
            {
                var req = service.Buckets.List(Project);
                req.Projection = BucketsResource.ListRequest.ProjectionEnum.Full;
                Buckets buckets = req.Execute();
                WriteObject(buckets.Items, true);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a new Google Cloud Storage bucket.
    /// </para>
    /// <para type="description">
    /// Creates a new Google Cloud Storage bucket. Bucket names must be globally unique. No two projects may
    /// have buckets with the same name.
    /// </para>
    /// <example>
    /// <code>PS C:\> New-Gcsbucket "unique-bucket-name"</code>
    /// <para>Creates a new bucket named "unique-bucket-name". </para>
    /// </example>
    /// <example>
    ///   <para>Creates a new bucket named "widget-co-logs".</para>
    ///   <para><code>New-GcsBucket "widget-co-logs"</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcsBucket"), OutputType(typeof(Bucket))]
    public class NewGcsBucketCmdlet : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the bucket.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the project associated with the command. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Storage class for the bucket. STANDARD, NEARLINE, or DURABLE_REDUCED_AVAILABILITY. See
        /// https://cloud.google.com/storage/docs/storage-classes for more information.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateSet("DURABLE_REDUCED_AVAILABILITY", "NEARLINE", "STANDARD", IgnoreCase = true)]
        public string StorageClass { get; set; }

        /// <summary>
        /// <para type="description">
        /// Location for the bucket. e.g. ASIA, EU, US.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateSet("ASIA", "EU", "US", IgnoreCase = false)]
        public string Location { get; set; }

        /// <summary>
        /// <para type="description">
        /// Default ACL for the bucket. e.g. "publicRead", "private", etc.
        /// </para>
        /// <para type="description">
        /// You cannot set fine-grained (e.g. individual users or domains) ACLs using PowerShell.
        /// Instead please use `gsutil`.
        /// </para>
        /// </summary>
        [Parameter]
        public BucketsResource.InsertRequest.PredefinedAclEnum? DefaultBucketAcl { get; set; }

        /// <summary>
        /// <para type="description">
        /// Default ACL for objects added to the bucket. e.g. "publicReadWrite", "authenticatedRead", etc.
        /// </para>
        /// <para type="description">
        /// You cannot set fine-grained (e.g. individual users or domains) ACLs using PowerShell.
        /// Instead please use `gsutil`.
        /// </para>
        /// </summary>

        [Parameter]
        public BucketsResource.InsertRequest.PredefinedDefaultObjectAclEnum? DefaultObjectAcl { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            var bucket = new Google.Apis.Storage.v1.Data.Bucket();
            bucket.Name = Name;
            bucket.Location = Location;
            bucket.StorageClass = StorageClass;

            BucketsResource.InsertRequest insertReq = service.Buckets.Insert(bucket, Project);
            insertReq.PredefinedAcl = DefaultBucketAcl;
            insertReq.PredefinedDefaultObjectAcl = DefaultObjectAcl;
            bucket = insertReq.Execute();

            WriteObject(bucket);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes a Google Cloud Storage Bucket.
    /// </para>
    /// <para type="description">
    /// Deletes a Google Cloud Storage Bucket.
    /// </para>
    /// <example>
    /// <code>PS C:\> Remove-GcsBucket "unique-bucket-name"</code>
    /// <para> Deletes the bucket "unique-bucket-name"</para>
    /// </example>
    /// <example>
    /// <code>PS C:\> Get-GcsBucket "bucket-with-files" | Remove-GcsBucket -Force</code>
    /// <para>Forces the deletion of "bucket-with-files, despite the bucket containing objects.</para>
    /// </example>
    /// <example>
    ///   <para>Check if bucket "foo" exists.</para>
    ///   <para><code>PS C:\> Remove-GcsBucket prod-database -WhatIf</code></para>
    ///   <para><code>What if: Performing the operation "Delete Bucket" on target "prod-database".</code></para>
    ///   <para>True</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsBucket", SupportsShouldProcess = true)]
    public class RemoveGcsBucketCmdlet : GcsCmdlet
    {
        /// <summary>
        /// Used for generating activity ids used by WriteProgress.
        /// </summary>
        private static readonly Random ActivityIdGenerator = new Random();

        /// <summary>
        /// <para typedef="description">
        /// The name of the bucket to remove. This parameter will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Bucket))]
        public string Name { get; set; }

        /// <summary>
        /// <para typedef="description">
        /// When deleting a bucket with objects still inside, use Force to proceed with the deletion without
        /// a prompt.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (!ShouldProcess($"{Name}", "Delete Bucket"))
            {
                return;
            }
            var service = GetStorageService();
            try
            {
                service.Buckets.Delete(Name).Execute();
            }
            catch (GoogleApiException re)
            {
                if (re.HttpStatusCode == HttpStatusCode.Conflict)
                {
                    WriteVerbose("Got RequestError[409]. Bucket not empty.");

                    List<Task<string>> deleteTasks = AskDeleteObjects(service);

                    WaitDeleteTasks(deleteTasks);

                    service.Buckets.Delete(Name).Execute();
                }
            }
        }

        /// <summary>
        /// Asks the user about deleting bucket objects, and starts async tasks to do so.
        /// </summary>
        private List<Task<string>> AskDeleteObjects(StorageService service)
        {
            List<Task<string>> deleteTasks = new List<Task<string>>();
            bool yesAll = false;
            bool noAll = false;

            ObjectsResource.ListRequest request = service.Objects.List(Name);
            do
            {
                Objects bucketObjects = request.Execute();
                string caption = $"Deleting {bucketObjects.Items.Count} bucket objects";
                if (bucketObjects.NextPageToken != null)
                {
                    caption = $"Deleting more than {bucketObjects.Items.Count} bucket objects";
                }
                foreach (var bucketObject in bucketObjects.Items)
                {
                    string query = $"Delete bucket object {bucketObject.Name}?";
                    if (Force || ShouldContinue(query, caption, ref yesAll, ref noAll))
                    {
                        deleteTasks.Add(service.Objects.Delete(Name, bucketObject.Name).ExecuteAsync());
                    }
                }
                request.PageToken = bucketObjects.NextPageToken;
            } while (request.PageToken != null && !noAll && !Stopping);

            return deleteTasks;
        }

        /// <summary>
        /// Waits on the list of delete tasks to compelet, updating progress as it does so.
        /// </summary>
        private void WaitDeleteTasks(List<Task<string>> deleteTasks)
        {
            int totalTasks = deleteTasks.Count;
            int finishedTasks = 0;
            int activityId = ActivityIdGenerator.Next();

            foreach (var deleteTask in deleteTasks)
            {
                deleteTask.Wait();
                finishedTasks++;
                WriteProgress(
                    new ProgressRecord(activityId, "Delete bucket objects", "Deleting objects")
                    {
                        PercentComplete = (finishedTasks * 100) / totalTasks,
                        RecordType = ProgressRecordType.Processing
                    });
            }

            WriteProgress(
                new ProgressRecord(activityId, "Delete bucket objects", "Objects deleted")
                {
                    PercentComplete = 100,
                    RecordType = ProgressRecordType.Completed
                });
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Tests if a bucket with the given name already exists.
    /// </para>
    /// <para type="description">
    /// Tests if a bucket with the given name already exists. Returns true if such a bucket already exists.
    /// </para>
    /// <example>
    /// <code>
    /// PS C:\> Test-GcsBucket "bucket-name"
    /// True
    /// </code>
    /// <para>Tests that a bucket named "bucket-name" does exist. A new bucket with this name may not be
    /// created.</para>
    /// </example>
    /// <example>
    ///   <para>Check if bucket "foo" exists.</para>
    ///   <para><code>PS C:\> Test-GcsBucket "foo"</code></para>
    ///   <para>True</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "GcsBucket"), OutputType(typeof(bool))]
    public class TestGcsBucketCmdlet : GcsCmdlet
    {
        /// <summary>
        /// <para typedef="description">
        /// The name of the bucket to test for. This parameter will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformationAttribute(Property = "Name", TypeToTransform = typeof(Bucket))]
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
