// Copyright 2015 Google Inc. All Rights Reserved.
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
    /// Removes the website associated with a Cloud Storage Bucket.
    /// </para>
    /// <para type="description">
    /// Removes the website associated with a Cloud Storage Bucket.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsBucketWebsite")]
    public class RemoveGcsBucketWebsiteCmdlet : GcsCmdlet
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

            // TODO(chrsmith): Follow up. Is this a bug? How to remove an object?
            bucket.Website = new Bucket.WebsiteData();  // Keep uninitialized to clear fields.
            BucketsResource.PatchRequest req2 = service.Buckets.Patch(bucket, this.Name);
            req2.Projection = BucketsResource.PatchRequest.ProjectionEnum.Full;
            req2.Execute();

            this.WriteObject(bucket);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Updates the website associated with a Cloud Storage Bucket.
    /// </para>
    /// <para type="description">
    /// Updates the website associated with a Cloud Storage Bucket.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommunications.Write, "GcsBucketWebsite")]
    public class WriteGcsBucketWebsiteCmdlet : GcsCmdlet
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
