// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System.Management.Automation;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Services;
using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;

using Google.PowerShell.Common;

namespace Google.PowerShell.CloudStorage
{
    /// <summary>
    /// <para type="synopsis">
    /// Removes the logging data associated with a Cloud Storage Bucket.
    /// </para>
    /// <para type="description">
    /// Removes the logging data associated with a Cloud Storage Bucket.
    /// </para>
    /// <example>
    ///   <para>Stop generating logs data for access to bucket "widgetco".</para>
    ///   <para><code>PS C:\> Remove-GcsBucketLogging "widgetco"</code></para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/access-logs)">[Access Logs]</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsBucketLogging"), OutputType(typeof(Bucket))]
    public class RemoveGcsBucketLoggingCmdlet : GcsCmdlet
    {
        /// <summary>
        /// <para typedef="description">
        /// The name of the bucket to remove logging for. This parameter will also accept a Bucket
        /// object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformationAttribute(Property = "Name", TypeToTransform = typeof(Bucket))]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            BucketsResource.GetRequest req = service.Buckets.Get(this.Name);
            req.Projection = BucketsResource.GetRequest.ProjectionEnum.Full;
            Bucket bucket = req.Execute();

            // TODO(chrsmith): Follow up. Is this a bug? How can you remove an object?
            // Logging doesn't work for patch-with-empty-obj.
            bucket.Logging = null;

            BucketsResource.UpdateRequest req2 = service.Buckets.Update(bucket, this.Name);
            req2.Projection = BucketsResource.UpdateRequest.ProjectionEnum.Full;
            req2.Execute();

            this.WriteObject(bucket);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Updates the logging data associated with a Cloud Storage Bucket.
    /// </para>
    /// <para type="description">
    /// Updates the logging data associated with a Cloud Storage Bucket.
    /// </para>
    /// <example>
    ///   <para>Start generating logs data for access to bucket "widgetco".</para>
    ///   <para><code>PS C:\> Write-GcsBucketLogging "widgetco" -LogBucket "widgetco-logs" -LogObjectPrefix "log-output/bucket"</code></para>
    ///   <para>Logs should be accessible afterwards via, at gs://widgetco-logs/log-output/bucket_usage_&lt;timestamp&gt;_&lt;id&gt;_v0.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/access-logs)">[Access Logs]</para>
    /// </summary>
    [Cmdlet(VerbsCommunications.Write, "GcsBucketLogging"), OutputType(typeof(Bucket))]
    public class WriteGcsBucketLoggingCmdlet : GcsCmdlet
    {
        /// <summary>
        /// <para typedef="description">
        /// The name of the bucket to configure. This parameter will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformationAttribute(Property = "Name", TypeToTransform = typeof(Bucket))]
        public string Name { get; set; }

        [Parameter(Position = 1)]
        public string LogBucket { get; set; }

        [Parameter(Position = 2)]
        public string LogObjectPrefix { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            Bucket.LoggingData logging = new Bucket.LoggingData();
            logging.LogBucket = this.LogBucket;
            logging.LogObjectPrefix = this.LogObjectPrefix;

            Bucket bucket = service.Buckets.Get(this.Name).Execute();
            bucket.Logging = logging;
            var req = service.Buckets.Patch(bucket, this.Name);
            req.Projection = BucketsResource.PatchRequest.ProjectionEnum.Full;
            req.Execute();

            this.WriteObject(bucket);
        }
    }
}