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
    /// Removes the website associated with a Cloud Storage Bucket.
    /// </para>
    /// <para type="description">
    /// Removes the website associated with a Cloud Storage Bucket.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcsBucketWebsite $bucket</code>
    ///   <para>Remove the website data for $bucket.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/hosting-static-website)">[Static Website Hosting]</para>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/static-website)">[Static Website Troubleshooting]</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsBucketWebsite"), OutputType(typeof(Bucket))]
    public class RemoveGcsBucketWebsiteCmdlet : GcsCmdlet
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

            // TODO(chrsmith): Follow up. Is this a bug? How to remove an object?
            bucket.Website = new Bucket.WebsiteData();  // Keep uninitialized to clear fields.
            BucketsResource.PatchRequest req2 = Service.Buckets.Patch(bucket, this.Name);
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
    /// <example>
    ///   <code>Write-GcsBucketWebsite $bucket -MainPageSuffix "main.html" -NotFoundPage "error.html"</code>   
    ///   <para>Host http://example.com from the contents of $bucket.</para>
    ///   <para>Next, set the domains DNS records to point to Cloud Storage. See the "Static WebsiteHosting"
    ///   help topic for more information.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/hosting-static-website)">[Static Website Hosting]</para>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/static-website)">[Static Website Troubleshooting]</para>
    /// </summary>
    [Cmdlet(VerbsCommunications.Write, "GcsBucketWebsite"), OutputType(typeof(Bucket))]
    public class WriteGcsBucketWebsiteCmdlet : GcsCmdlet
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
        /// Storage object for the "main page" of the website, e.g. what is served from "http://example.com/".
        /// Defaults to "index.html".
        /// </para>
        /// </summary>
        [Parameter(Position = 1)]
        public string MainPageSuffix { get; set; } = "index.html";

        /// <summary>
        /// <para type="description">
        /// Storage object to render when no appropriate file is found, e.g. what is served from "http://example.com/sadjkffasugmd".
        /// Defaults to "404.html".
        /// </para>
        /// </summary>
        [Parameter(Position = 2)]
        public string NotFoundPage { get; set; } = "404.html";

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            Bucket.WebsiteData website = new Google.Apis.Storage.v1.Data.Bucket.WebsiteData();
            website.MainPageSuffix = this.MainPageSuffix;
            website.NotFoundPage = this.NotFoundPage;

            Bucket bucket = Service.Buckets.Get(this.Name).Execute();
            bucket.Website = website;
            var req = Service.Buckets.Patch(bucket, this.Name);
            req.Projection = BucketsResource.PatchRequest.ProjectionEnum.Full;
            req.Execute();

            this.WriteObject(bucket);
        }
    }
}
