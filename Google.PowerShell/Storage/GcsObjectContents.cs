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
    /// Get-GcsBucketContents downloads the contents of a Google Cloud Storage Object to disk.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcsObjectContents")]
    public class GetGcsObjectContentsCmdlet : GcsCmdlet
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

        /// <summary>
        /// Destination on disk to download the contents to.
        /// </summary>
        [Parameter(Position = 2, Mandatory = true)]
        public string DestinationPath { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter Overwrite { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            // TODO(chrsmith): Pop a confirmation? Overwrite (y/N)?
            string qualifiedPath = Path.GetFullPath(DestinationPath);
            bool fileExists = File.Exists(qualifiedPath);
            if (fileExists && !Overwrite.IsPresent)
            {
                throw new PSArgumentException("File Already Exists. Use -Overwrite to overwrite.");
            }

            string uri = GetBaseUri(Bucket, ObjectName);
            var downloader = new MediaDownloader(service);

            // Confirm the file exists. MediaDownloader doesn't throw or
            // report an error if the GCS Object does not exist.
            //
            // Just get the object, and let any exceptions bubble up.
            // TODO(chrsmith): Log the bug.
            service.Objects.Get(Bucket, ObjectName).Execute();

            using (var writer = new FileStream(qualifiedPath, FileMode.Create))
            {
                var result = downloader.Download(uri, writer);
                if (result.Status == DownloadStatus.Failed
                    || result.Exception != null)
                {
                    throw result.Exception;
                }
            }
        }
    }
}
