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
    /// Removes the website associated with a Google Cloud Storage bucket.
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsBucketWebsite")]
    public class RemoveGcsBucketWebsiteCmdlet : GcsCmdlet
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

            // TODO(chrsmith): Follow up. Is this a bug? How to remove an object?
            bucket.Website = new Bucket.WebsiteData();  // Keep uninitialized to clear fields.
            BucketsResource.PatchRequest req2 = service.Buckets.Patch(bucket, this.Name);
            req2.Projection = BucketsResource.PatchRequest.ProjectionEnum.Full;
            req2.Execute();

            this.WriteObject(bucket);
        }
    }

    /// <summary>
    /// Removes the website associated with a Google Cloud Storage bucket.
    /// </summary>
    [Cmdlet(VerbsCommunications.Write, "GcsBucketWebsite")]
    public class WriteGcsBucketWebsiteCmdlet : GcsCmdlet
    {
        [Parameter(Position = 0, Mandatory = true)]
        public string Name { get; set; }

        [Parameter(Position = 1)]
        public string MainPageSuffix { get; set; }

        [Parameter(Position = 2)]
        public string NotFoundPage { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            Bucket.WebsiteData website = new Google.Apis.Storage.v1.Data.Bucket.WebsiteData();
            website.MainPageSuffix = this.MainPageSuffix;
            website.NotFoundPage = this.NotFoundPage;

            Bucket bucket = service.Buckets.Get(this.Name).Execute();
            bucket.Website = website;
            var req = service.Buckets.Patch(bucket, this.Name);
            req.Projection = BucketsResource.PatchRequest.ProjectionEnum.Full;
            req.Execute();

            this.WriteObject(bucket);
        }
    }
}
