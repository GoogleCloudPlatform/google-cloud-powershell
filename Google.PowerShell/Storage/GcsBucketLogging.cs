// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.PowerShell.Common;
using System.Management.Automation;

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
    ///   <code>PS C:\> Remove-GcsBucketLogging "widgetco"</code>   
    ///   <para>Stop generating logs data for access to bucket "widgetco".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/access-logs)">[Access Logs]</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsBucketLogging"), OutputType(typeof(Bucket))]
    public class RemoveGcsBucketLoggingCmdlet : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
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

            BucketsResource.GetRequest req = Service.Buckets.Get(this.Name);
            req.Projection = BucketsResource.GetRequest.ProjectionEnum.Full;
            Bucket bucket = req.Execute();

            // TODO(chrsmith): Follow up. Is this a bug? How can you remove an object?
            // Logging doesn't work for patch-with-empty-obj.
            bucket.Logging = null;

            BucketsResource.UpdateRequest req2 = Service.Buckets.Update(bucket, this.Name);
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
    ///   <code>
    ///   Write-GcsBucketLogging "widgetco" -LogBucket "widgetco-logs" `
    ///       -LogObjectPrefix "log-output/bucket"
    ///   </code>
    ///   <para>Start generating logs data for access to bucket "widgetco".</para>
    ///   <para>Logs should be accessible afterwards via, at 
    ///   "gs://widgetco-logs/log-output/bucket_usage_&lt;timestamp&gt;_&lt;id&gt;_v0".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/access-logs)">[Access Logs]</para>
    /// </summary>
    [Cmdlet(VerbsCommunications.Write, "GcsBucketLogging"), OutputType(typeof(Bucket))]
    public class WriteGcsBucketLoggingCmdlet : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket to configure. This parameter will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformationAttribute(Property = "Name", TypeToTransform = typeof(Bucket))]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The destination bucket where the current bucket's logs should be placed.
        /// </para>
        /// </summary>
        [Parameter(Position = 1)]
        public string LogBucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Prefix for the log object's name.
        /// </para>
        /// </summary>
        [Parameter(Position = 2)]
        public string LogObjectPrefix { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            Bucket.LoggingData logging = new Bucket.LoggingData();
            logging.LogBucket = this.LogBucket;
            logging.LogObjectPrefix = this.LogObjectPrefix;

            Bucket bucket = Service.Buckets.Get(this.Name).Execute();
            bucket.Logging = logging;
            var req = Service.Buckets.Patch(bucket, this.Name);
            req.Projection = BucketsResource.PatchRequest.ProjectionEnum.Full;
            req.Execute();

            this.WriteObject(bucket);
        }
    }
}