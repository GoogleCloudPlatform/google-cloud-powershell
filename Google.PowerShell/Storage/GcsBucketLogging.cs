// Copyright 2015 Google Inc. All Rights Reserved.

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
    /// Removes the logging data associated with a Google Cloud Storage bucket.
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsBucketLogging")]
    public class RemoveGcsBucketLoggingCmdlet : GcsCmdlet
    {
        [Parameter(Position = 0, Mandatory = true)]
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
    /// Removes the logging data associated with a Google Cloud Storage bucket.
    /// </summary>
    [Cmdlet(VerbsCommunications.Write, "GcsBucketLogging")]
    public class WriteGcsBucketLoggingCmdlet : GcsCmdlet
    {
        [Parameter(Position = 0, Mandatory = true)]
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