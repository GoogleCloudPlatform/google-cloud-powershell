﻿// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Services;
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
    /// Gets the Google Cloud Storage bucket with a given name, or all buckets associated with a
    /// project.
    /// </para>
    /// <para type="description">
    /// Returns the Google Cloud Storate bucket by name, if the current gcloud user has access.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, will instead return all buckets owned by that project. Again,
    /// restricted to those that the gcloud user has access to view.
    /// </para>
    /// <example>
    ///   <para>Get the bucket named "widget-co-logs".</para>
    ///   <para><code>Get-GcsBucket "widget-co-logs"</code></para>
    /// </example>
    /// <example>
    ///   <para>Get all buckets for project "widget-co".</para>
    ///   <para><code>Get-GcsBucket -Project "widget-co"</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcsBucket", DefaultParameterSetName = "SingleBucket")]
    public class GetGcsBucketCmdlet : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket to return.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "SingleBucket")]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The project to check for Storage buckets.
        /// </para>
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

    [Cmdlet(VerbsCommon.Remove, "GcsBucket", SupportsShouldProcess = true)]
    public class RemoveGcsBucketCmdlet : GcsCmdlet
    {
        /// <summary>
        /// Used for generating activity ids used by WriteProgress.
        /// </summary>
        private static readonly Random ActivityIdGenerator = new Random();

        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true)]
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
        /// Asks the user about deleting bucke objects, and starts asynchornis tasks to do so.
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
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
            } while (request.PageToken != null && !noAll);

            return deleteTasks;
        }

        /// <summary>
        /// Waits on the list of delete tasks to compelet, updating progress as it does so.
        /// </summary>
        /// <param name="deleteTasks">
        /// The list of delete tasks to wait on.
        /// </param>
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
