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
using System.Text;
using static Google.Apis.Storage.v1.ObjectsResource.InsertMediaUpload;

namespace Google.PowerShell.CloudStorage
{
    // TODO(chrsmith): For all object-related cmdlets, provide an alternate ParameterSet that
    // supports just the object name, with the gs://<bucket>/<object> syntax.

    // TODO(chrsmith): Provide a way to upload an entire directory to Gcs. Reuse New-GcsObject?
    // Upload-GcsObject?

    // TODO(chrsmith): Provide a way to test if an object exists, a la Test-GcsObject.

    /// <summary>
    /// Base class for Cloud Storage Object cmdlets. Used to reuse common methods.
    /// </summary>
    public abstract class GcsObjectCmdlet : GcsCmdlet
    {
        /// <summary>
        /// Returns whether or not a storage object with the given name exists in the provided
        /// bucket. Will return false if the object exists but is not visible to the current
        /// user.
        /// </summary>
        protected bool TestObjectExists(StorageService service, string bucket, string objectName)
        {
            try
            {
                ObjectsResource.GetRequest getReq = service.Objects.Get(bucket, objectName);
                getReq.Execute();
                return true;
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                // Just swallow it. Ideally we wouldn't need to use an exception for
                // control flow, but alas the API doesn't seem to have a test method.
            }
            return false;
        }

        /// <summary>
        /// Uploads a local file to Google Cloud storage, overwriting any existing objects
        /// as applicable.
        /// </summary>
        protected Object UploadGcsObject(
            StorageService service, string bucket, string objectName,
            Stream contentStream, string contentType = OctetStreamMimeType,
            PredefinedAclEnum? predefinedAcl = null)
        {
            Object newGcsObject = new Object
            {
                Bucket = bucket,
                Name = objectName,
                ContentType = contentType
            };

            ObjectsResource.InsertMediaUpload insertReq = service.Objects.Insert(
                newGcsObject, bucket, contentStream, contentType);
            insertReq.PredefinedAcl = predefinedAcl;

            var finalProgress = insertReq.Upload();
            if (finalProgress.Exception != null)
            {
                throw finalProgress.Exception;
            }

            return insertReq.ResponseBody;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Uploads a local file into a Google Cloud Storage bucket.
    /// </para>
    /// <para type="description">
    /// Uploads a local file into a Google Cloud Storage bucket.
    /// </para>
    /// <example>
    ///   <para>Upload a local log file to GCS.</para>
    ///   <para><code>New-GcsObject -Bucket "widget-co-logs" -ObjectName "log-000.txt" `</code></para>
    ///   <para><code>    -File "C:\logs\log-000.txt"</code></para></code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcsObject")]
    public class NewGcsObjectCmdlet : GcsObjectCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket to upload to.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the created Cloud Storage object.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Text content to write to the Storage object. Ignored if File is specified.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = false, ValueFromPipeline = true, ParameterSetName = "ContentsFromString")]
        public string Contents { get; set; }

        /// <summary>
        /// <para type="description">
        /// Local path to the file to upload.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ParameterSetName = "ContentsFromFile")]
        public string File { get; set; }

        /// <summary>
        /// <para type="description">
        /// Content type of the Cloud Storage object. e.g. "image/png" or "text/plain".
        /// </para>
        /// <para type="description">
        /// Defaults to "application/octet-stream" if not set.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string ContentType { get; set; }

        // See: https://cloud.google.com/storage/docs/json_api/v1/objects/insert
        /// <summary>
        /// <para type="description">
        /// Provide a predefined ACL to the object. e.g. "publicRead" where the project owner gets
        /// OWNER access, and allUsers get READER access.
        /// </para>
        /// </summary>
        [ValidateSet(
            "authenticatedRead", "bucketOwnerFullControl", "bucketOwnerRead",
            "private", "projectPrivate", "publicRead", IgnoreCase = false)]
        [Parameter(Mandatory = false)]
        public string PredefinedAcl { get; set; }

        /// <summary>
        /// <para type="description">
        /// Force the operation to succeed, overwriting existing Storage objects if needed.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Force { get; set; }

        protected PredefinedAclEnum? GetPredefinedAcl()
        {
            switch (PredefinedAcl)
            {
                case "authenticatedRead": return PredefinedAclEnum.AuthenticatedRead;
                case "bucketOwnerFullControl": return PredefinedAclEnum.BucketOwnerFullControl;
                case "bucketOwnerRead": return PredefinedAclEnum.BucketOwnerRead;
                case "private": return PredefinedAclEnum.Private__;
                case "projectPrivate": return PredefinedAclEnum.ProjectPrivate;
                case "publicRead": return PredefinedAclEnum.PublicRead;

                case "":
                case null:
                    return null;
                default:
                    throw new PSInvalidOperationException(
                        string.Format("Invalid predefined ACL: {0}", PredefinedAcl));
            }

            return null;
        }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            if (string.IsNullOrEmpty(ContentType))
            {
                ContentType = OctetStreamMimeType;
            }

            Stream contentStream = null;
            if (!string.IsNullOrEmpty(File))
            {
                string qualifiedPath = Path.GetFullPath(File);
                if (!System.IO.File.Exists(qualifiedPath))
                {
                    throw new FileNotFoundException("File not found.", qualifiedPath);
                }
                contentStream = new FileStream(qualifiedPath, FileMode.Open);
            }
            else
            {
                // Get the underlying byte representation of the string using the same encoding (UTF-16).
                // So the data will be written in the same format it is passed, rather than converting to
                // UTF-8 or UTF-32 when writen to Cloud Storage.
                byte[] contentBuffer = Encoding.Unicode.GetBytes(Contents);
                contentStream = new MemoryStream(contentBuffer);
            }

            using (contentStream)
            {
                // We could potentially avoid this extra step by using a special request header.
                //     "If you set the x-goog-if-generation-match header to 0, Google Cloud Storage only
                //     performs the specified request if the object does not currently exist."
                // See https://cloud.google.com/storage/docs/reference-headers#xgoogifgenerationmatch
                bool objectExists = TestObjectExists(service, Bucket, ObjectName);
                if (objectExists && !Force.IsPresent)
                {
                    throw new PSArgumentException("Storage object already exists. Use -Force to overwrite.");
                }

                Object newGcsObject = UploadGcsObject(
                    service, Bucket, ObjectName, contentStream,
                    ContentType, GetPredefinedAcl());

                WriteObject(newGcsObject);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Get-GcsObject returns the Google Cloud Storage Object metadata with the given name. (Use
    /// Find-GcsObject to return multiple objects.)
    /// </para>
    /// <para type="description">
    /// Returns the give Storage object's metadata.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcsObject")]
    public class GetGcsObjectCmdlet : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the bucket to check.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to inspect.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public string ObjectName { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            ObjectsResource.GetRequest getReq = service.Objects.Get(Bucket, ObjectName);
            Object gcsObject = getReq.Execute();
            WriteObject(gcsObject);
        }
    }

    // TODO(chrsmith): Support iterating through the result prefixes as well as the items.
    // This is necessary to see the "subfolders" in Cloud Storage, even though the concept
    // does not exist.

    /// <summary>
    /// <para type="synopsis">
    /// Returns all Cloud Storage objects identified by the given prefix string.
    /// </para>
    /// <para type="description">
    /// Returns all Cloud Storage objects identified by the given prefix string.
    /// If no prefix string is provided, all objects in the bucket are returned.
    /// </para>
    /// <para type="description">
    /// An optional delimiter may be provided. If used, will return results in a
    /// directory-like mode, delimited by the given string. e.g. with objects "1,
    /// "2", "subdir/3" and delimited "/"; "subdir/3" would not be returned.
    /// (There is no way to just return "subdir" in the previous example.)
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Find, "GcsObject")]
    public class FindGcsObjectCmdlet : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the bucket to search.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Object prefix to use. e.g. "/logs/". If not specified all
        /// objects in the bucket will be returned.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = false)]
        public string Prefix { get; set; }

        /// <summary>
        /// <para type="description">
        /// Returns results in a directory-like mode, delimited by the given string. e.g.
        /// with objects "1, "2", "subdir/3" and delimited "/", "subdir/3" would not be
        /// returned.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string Delimiter { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            ObjectsResource.ListRequest listReq = service.Objects.List(Bucket);
            listReq.Delimiter = Delimiter;
            listReq.Prefix = Prefix;
            listReq.MaxResults = 100;

            // When used with WriteObject, expand the IEnumerable rather than
            // returning the IEnumerable itself. IEnumerable<T> vs. IEnumerable<IEnumerable<T>>.
            const bool enumerateCollection = true;

            // First page.
            Objects gcsObjects = listReq.Execute();
            WriteObject(gcsObjects.Items, enumerateCollection);

            // Keep paging through results as necessary.
            while (gcsObjects.NextPageToken != null)
            {
                listReq.PageToken = gcsObjects.NextPageToken;
                gcsObjects = listReq.Execute();
                WriteObject(gcsObjects.Items, enumerateCollection);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes a Cloud Storage object.
    /// </para>
    /// <para type="description">
    /// Deletes a Cloud Storage object.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsObject")]
    public class RemoveGcsObjectCmdlet : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the bucket containing the object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to delete.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public string ObjectName { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            ObjectsResource.DeleteRequest delReq = service.Objects.Delete(Bucket, ObjectName);
            string result = delReq.Execute();
            WriteObject(result);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Read the contents of a Cloud Storage object.
    /// </para>
    /// <para type="description">
    /// Reads the contents of a Cloud Storage object. By default the contents will be
    /// written to the pipeline. If the -OutFile parameter is set, it will be written
    /// to disk instead.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommunications.Read, "GcsObject")]
    public class ReadGcsObjectCmdlet : GcsObjectCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the bucket containing the object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to read.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Local file path to write the contents to.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = false)]
        public string OutFile { get; set; }

        // Consider adding a -PassThru parameter to enable writing the contents to the
        // pipeline AND saving to disk, like Invoke-WebRequest. See:
        // https://technet.microsoft.com/en-us/library/hh849901.aspx

        /// <summary>
        /// <para type="description">
        /// Force the operation to succeed, overwriting any local files.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            string uri = GetBaseUri(Bucket, ObjectName);
            var downloader = new MediaDownloader(service);

            // Write object contents to the pipeline if no -OutFile is specified.
            if (string.IsNullOrEmpty(OutFile))
            {
                // Start with a 1MiB buffer. We could get the object's metadata and use its exact
                // file size, but making a web request << just allocating more memory.
                using (var memStream = new MemoryStream(1024 * 1024))
                {
                    var result = downloader.Download(uri, memStream);
                    if (result.Status == DownloadStatus.Failed || result.Exception != null)
                    {
                        throw result.Exception;
                    }

                    // Stream cursor is at the end (data just written).
                    memStream.Position = 0;
                    using (var streamReader = new StreamReader(memStream))
                    {
                        string objectContents = streamReader.ReadToEnd();
                        WriteObject(objectContents);
                    }
                }

                return;
            }

            // Write object contents to disk. Fail if the local file exists, unless -Force is specified.
            string qualifiedPath = Path.GetFullPath(OutFile);
            bool fileExists = File.Exists(qualifiedPath);
            if (fileExists && !Force.IsPresent)
            {
                throw new PSArgumentException("File already exists. Use -Force to overwrite.");
            }


            using (var writer = new FileStream(qualifiedPath, FileMode.Create))
            {
                var result = downloader.Download(uri, writer);
                if (result.Status == DownloadStatus.Failed || result.Exception != null)
                {
                    throw result.Exception;
                }
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Replaces the contents of a Cloud Storage object.
    /// </para>
    /// <para type="description">
    /// Replaces the contents of a Cloud Storage object with data from the local disk or a value
    /// from the pipeline.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommunications.Write, "GcsObject")]
    public class WriteGcsObjectCmdlet : GcsObjectCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the bucket containing the object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to write to.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Text content to write to the Storage object. Ignored if File is specified.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = false, ValueFromPipeline = true, ParameterSetName = "ContentsFromString")]
        public string Contents { get; set; }

        /// <summary>
        /// <para type="description">
        /// Local file path to read, writing its contents into Cloud Storage.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = false, ParameterSetName = "ContentsFromFile")]
        public string File { get; set; }

        /// <summary>
        /// <para type="description">
        /// Force the operation to succeed, ignoring errors if no existing Storage object exists.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            Stream contentStream = null;
            if (!string.IsNullOrEmpty(File))
            {
                string qualifiedPath = Path.GetFullPath(File);
                if (!System.IO.File.Exists(qualifiedPath))
                {
                    throw new FileNotFoundException("File not found.", qualifiedPath);
                }
                contentStream = new FileStream(qualifiedPath, FileMode.Open);
            }
            else
            {
                // Get the underlying byte representation of the string using the same encoding (UTF-16).
                // So the data will be written in the same format it is passed, rather than converting to
                // UTF-8 or UTF-32 when writen to Cloud Storage.
                byte[] contentBuffer = Encoding.Unicode.GetBytes(Contents);
                contentStream = new MemoryStream(contentBuffer);
            }

            using (contentStream)
            {
                // Fail if the GCS Object does not exist. We don't use TestGcsObjectExists
                // so we can reuse the existing objects metadata when uploading a new file.
                string contentType = OctetStreamMimeType;
                try
                {
                    ObjectsResource.GetRequest getReq = service.Objects.Get(Bucket, ObjectName);
                    Object existingGcsObject = getReq.Execute();
                    contentType = existingGcsObject.ContentType;
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    if (!Force.IsPresent)
                    {
                        throw new PSArgumentException("Storage object does not exist. Use -Force to ignore.");
                    }
                }

                // Rewriting GCS objects is done by simply creating a new object with the
                // same name. (i.e. this is functionally identical to New-GcsObject.)
                //
                // We don't need to worry about data races and/or corrupting data mid-upload
                // because of the upload semantics of Cloud Storage.
                // See: https://cloud.google.com/storage/docs/consistency
                Object updatedGcsObject = UploadGcsObject(
                    service, Bucket, ObjectName, contentStream,
                    contentType);
            }
        }
    }
}
