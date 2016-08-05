// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Download;
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
            insertReq.Projection = ProjectionEnum.Full;

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
    ///   <para><code>PS C:\> New-GcsObject -Bucket "widget-co-logs" -ObjectName "log-000.txt" `</code></para>
    ///   <para><code>    -File "C:\logs\log-000.txt"</code></para>
    /// </example>
    /// <example>
    ///   <para>Pipe a string to a a file on GCS.</para>
    ///   <para><code>PS C:\> "Hello, World!" | New-GcsObject -Bucket "widget-co-logs" -ObjectName "log-000.txt"</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcsObject", DefaultParameterSetName = ParameterSetNames.ContentsFromString)]
    [OutputType(typeof(Object))]
    public class NewGcsObjectCmdlet : GcsObjectCmdlet
    {
        private class ParameterSetNames
        {
            public const string ContentsFromString = "ContentsFromString";
            public const string ContentsFromFile = "ContentsFromFile";
        }

        /// <summary>
        /// <para type="description">
        /// The name of the bucket to upload to. Will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = nameof(Apis.Storage.v1.Data.Bucket.Name),
            TypeToTransform = typeof(Bucket))]
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
        [Parameter(ParameterSetName = ParameterSetNames.ContentsFromString,
            Position = 2, ValueFromPipeline = true)]
        public string Contents { get; set; } = "";

        /// <summary>
        /// <para type="description">
        /// Local path to the file to upload.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ParameterSetName = ParameterSetNames.ContentsFromFile)]
        public string File { get; set; }

        /// <summary>
        /// <para type="description">
        /// Content type of the Cloud Storage object. e.g. "image/png" or "text/plain".
        /// </para>
        /// <para type="description">
        /// For file uploads, the type will be inferred based on the file extension, defaulting to
        /// "application/octet-stream" if no match is found. When passing object content via the
        /// -Contents parameter, the type will default to "text/plain; charset=utf-8".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string ContentType { get; set; }

        /// <summary>
        /// <para type="description">
        /// Provide a predefined ACL to the object. e.g. "publicRead" where the project owner gets
        /// OWNER access, and allUsers get READER access.
        /// </para>
        /// <para type="description">
        /// See: https://cloud.google.com/storage/docs/json_api/v1/objects/insert
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public PredefinedAclEnum? PredefinedAcl { get; set; }

        /// <summary>
        /// <para type="description">
        /// Force the operation to succeed, overwriting existing Storage objects if needed.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            string objContentType = null;
            Stream contentStream = null;
            if (!string.IsNullOrEmpty(File))
            {
                objContentType = ContentType ?? InferContentType(File) ?? OctetStreamMimeType;
                string qualifiedPath = GetFullPath(File);
                if (!System.IO.File.Exists(qualifiedPath))
                {
                    throw new FileNotFoundException("File not found.", qualifiedPath);
                }
                contentStream = new FileStream(qualifiedPath, FileMode.Open);
            }
            else
            {
                // We store string data as UTF-8, which is different from .NET's default encoding
                // (UTF-16). But this simplifies several other issues.
                objContentType = ContentType ?? UTF8TextMimeType;
                byte[] contentBuffer = Encoding.UTF8.GetBytes(Contents);
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
                    throw new PSArgumentException($"Storage object '{ObjectName}' already exists. Use -Force to overwrite.");
                }

                Object newGcsObject = UploadGcsObject(
                    service, Bucket, ObjectName, contentStream,
                    objContentType, PredefinedAcl);

                WriteObject(newGcsObject);
            }
        }

        /// <summary>
        /// Infer the MIME type of a non-qualified file path. Returns null if no match is found.
        /// </summary>
        private string InferContentType(string file)
        {
            int index = file.LastIndexOf('.');
            if (index == -1)
            {
                return null;
            }
            string extension = file.ToLowerInvariant().Substring(index);
            // http://www.freeformatter.com/mime-types-list.html
            switch (extension)
            {
                case ".htm":
                case ".html":
                    return "text/html";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".js":
                    return "application/javascript";
                case ".json":
                    return "application/json";
                case ".png":
                    return "image/png";
                case ".txt":
                    return "text/plain";
                case ".zip":
                    return "application/zip";
            }
            return null;
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
    /// <example>
    ///   <para>Get object metadata.</para>
    ///   <para><code>PS C:\> Get-GcsObject -Bucket "widget-co-logs" -ObjectName "log-000.txt"</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcsObject"), OutputType(typeof(Object))]
    public class GetGcsObjectCmdlet : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the bucket to check. Will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformationAttribute(Property = "Name", TypeToTransform = typeof(Bucket))]
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
            getReq.Projection = ObjectsResource.GetRequest.ProjectionEnum.Full;
            Object gcsObject = getReq.Execute();
            WriteObject(gcsObject);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Set-GcsObject updates metadata associated with a Cloud Storage Object.
    /// </para>
    /// <para type="description">
    /// Updates the metadata associated with a Cloud Storage Object, such as ACLs.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GcsObject")]
    public class SetGcsObjectCmdlet : GcsCmdlet
    {
        private class ParameterSetNames
        {
            public const string FromBucketAndObjName = "FromBucketAndObjName";
            public const string FromObject = "FromObjectObject";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the bucket to check. Will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.FromBucketAndObjName)]
        [PropertyByTypeTransformationAttribute(Property = "Name", TypeToTransform = typeof(Bucket))]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to update.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = ParameterSetNames.FromBucketAndObjName)]
        public string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Storage object instance to update.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true,
            ValueFromPipeline = true, ParameterSetName = ParameterSetNames.FromObject)]
        public Object Object { get; set; }

        /// <summary>
        /// <para type="description">
        /// Provide a predefined ACL to the object. e.g. "publicRead" where the project owner gets
        /// OWNER access, and allUsers get READER access.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public ObjectsResource.UpdateRequest.PredefinedAclEnum? PredefinedAcl { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            string bucket = null;
            string objectName = null;
            switch (ParameterSetName)
            {
                case ParameterSetNames.FromBucketAndObjName:
                    bucket = Bucket;
                    objectName = ObjectName;
                    break;
                case ParameterSetNames.FromObject:
                    bucket = Object.Bucket;
                    objectName = Object.Name;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            // You cannot specify both an ACL list and a predefined ACL using the API. (b/30358979?)
            // We issue a GET + Update. Since we aren't using ETags, there is a potential for a
            // race condition.
            var getReq = service.Objects.Get(bucket, objectName);
            getReq.Projection = ObjectsResource.GetRequest.ProjectionEnum.Full;
            Object objectInsert = getReq.Execute();
            // The API doesn't allow both predefinedAcl and access controls. So drop existing ACLs.
            objectInsert.Acl = null;

            ObjectsResource.UpdateRequest updateReq = service.Objects.Update(objectInsert, bucket, objectName);
            updateReq.PredefinedAcl = PredefinedAcl;

            Object gcsObject = updateReq.Execute();
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
    /// <example>
    ///   <para>Get all objects in a Storage Bucket</para>
    ///   <para><code>PS C:\> Find-GcsObject -Bucket "widget-co-logs"</code></para>
    /// </example>
    /// <example>
    ///   <para>Get all objects in a specific folder Storage Bucket.</para>
    ///   <para><code>PS C:\> Find-GcsObject -Bucket "widget-co-logs" -Prefix "pictures/winter" -Delimiter "/"</code></para>
    ///   <para>Because the Delimiter parameter was set, will not return objects under "pictures/winter/2016/". The search will omit any objects matching the prefix containing the delimiter.</para>
    /// </example>
    /// <example>
    ///   <para>Get all objects in a specific folder Storage Bucket. Will return objects in pictures/winter/2016/.</para>
    ///   <para><code>PS C:\> Find-GcsObject -Bucket "widget-co-logs" -Prefix "pictures/winter"</code></para>
    ///   <para>Because the Delimiter parameter was not set, will return objects under "pictures/winter/2016/".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Find, "GcsObject"), OutputType(typeof(Object))]
    public class FindGcsObjectCmdlet : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the bucket to search. Will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true)]
        [PropertyByTypeTransformationAttribute(Property = "Name", TypeToTransform = typeof(Bucket))]
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
            listReq.Projection = ObjectsResource.ListRequest.ProjectionEnum.Full;
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
    /// <example>
    ///   <para><code>PS C:\> Remove-GcsObject ppiper-prod text-files/14683615 -WhatIf</code></para>
    ///   <para><code>What if: Performing the operation "Delete Object" on target "[ppiper-prod] text-files/14683615".</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsObject",
        DefaultParameterSetName = ParameterSetNames.FromName, SupportsShouldProcess = true)]
    public class RemoveGcsObjectCmdlet : GcsCmdlet
    {
        private class ParameterSetNames
        {
            public const string FromName = "FromObjectName";
            public const string FromObject = "FromObjectObject";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the bucket containing the object. Will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.FromName)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Bucket))]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to delete.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = ParameterSetNames.FromName)]
        public string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to delete.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.FromObject)]
        public Object Object { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            switch (ParameterSetName)
            {
                case ParameterSetNames.FromName:
                    // We just use Bucket and ObjectName.
                    break;
                case ParameterSetNames.FromObject:
                    Bucket = Object.Bucket;
                    ObjectName = Object.Name;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            if (!ShouldProcess($"[{Bucket}] {ObjectName}", "Delete Object"))
            {
                return;
            }

            ObjectsResource.DeleteRequest delReq = service.Objects.Delete(Bucket, ObjectName);
            string result = delReq.Execute();
            if (!string.IsNullOrWhiteSpace(result))
            {
                WriteObject(result);
            }
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
    /// <example>
    ///   <para>Write the objects of a Storage Object to disk.</para>
    ///   <para><code>PS C:\> Read-GcsObject -Bucket "widget-co-logs" -ObjectName "log-000.txt" `</code></para>
    ///   <para><code>    -OutFile "C:\logs\log-000.txt"</code></para>
    /// </example>
    /// <example>
    ///   <para>Read the Storage Object as a string.</para>
    ///   <para><code>PS C:\> Read-GcsObject -Bucket "widget-co-logs" -ObjectName "log-000.txt" | Write-Host</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommunications.Read, "GcsObject", DefaultParameterSetName = ParameterSetNames.ByName)]
    [OutputType(typeof(string))]  // Not 100% correct, cmdlet will output nothing if -OutFile is specified.
    public class ReadGcsObjectCmdlet : GcsObjectCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the bucket containing the object. Will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.ByName)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Bucket))]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to read.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = ParameterSetNames.ByName)]
        public string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Google Cloud Storage object to read.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true, ValueFromPipeline = true)]
        public Object InputObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// Local file path to write the contents to.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Position = 2)]
        [Parameter(ParameterSetName = ParameterSetNames.ByObject)]
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

            if (InputObject != null)
            {
                Bucket = InputObject.Bucket;
                ObjectName = InputObject.Name;
            }

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
            string qualifiedPath = GetFullPath(OutFile);
            bool fileExists = File.Exists(qualifiedPath);
            if (fileExists && !Force.IsPresent)
            {
                throw new PSArgumentException($"File '{qualifiedPath}' already exists. Use -Force to overwrite.");
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
    /// <example>
    ///   <para>Update the contents of the Storage Object with the string "OK".</para>
    ///   <para><code>PS C:\> "OK" | Write-GcsObject -Bucket "widget-co-logs" -ObjectName "status.txt"</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommunications.Write, "GcsObject")]
    public class WriteGcsObjectCmdlet : GcsObjectCmdlet
    {
        private class ParameterSetNames
        {
            public const string FromString = "FromString";
            public const string FromFile = "FromFile";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the bucket containing the object. Will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Bucket))]
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
        [Parameter(ParameterSetName = ParameterSetNames.FromString,
            Position = 2, ValueFromPipeline = true)]
        public string Contents { get; set; }

        /// <summary>
        /// <para type="description">
        /// Local file path to read, writing its contents into Cloud Storage.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.FromFile)]
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

            Stream contentStream;
            if (!string.IsNullOrEmpty(File))
            {
                string qualifiedPath = GetFullPath(File);
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
                byte[] contentBuffer = Encoding.Unicode.GetBytes(Contents ?? "");
                contentStream = new MemoryStream(contentBuffer);
            }

            using (contentStream)
            {
                // Fail if the GCS Object does not exist. We don't use TestGcsObjectExists
                // so we can reuse the existing objects metadata when uploading a new file.
                string contentType = null;
                try
                {
                    ObjectsResource.GetRequest getReq = service.Objects.Get(Bucket, ObjectName);
                    getReq.Projection = ObjectsResource.GetRequest.ProjectionEnum.Full;
                    Object existingGcsObject = getReq.Execute();
                    contentType = existingGcsObject.ContentType;
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    if (!Force.IsPresent)
                    {
                        throw new PSArgumentException($"Storage object '{ObjectName}' does not exist. Use -Force to ignore.");
                    }
                }
                // TODO(chrsmith): In the -Force case we are using the default octet-stream MIME type. Guess
                // the correct type based on file extension.

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

    /// <summary>
    /// <para type="synopsis">
    /// Verify the existence of a Cloud Storage Object.
    /// </para>
    /// <para type="description">
    /// Verify the existence of a Cloud Storage Object.
    /// </para>
    /// <example>
    ///   <para>Test if an object named "status.txt" exists in the bucket "widget-co-logs".</para>
    ///   <para><code>PS C:\> Test-GcsObject -Bucket "widget-co-logs" -ObjectName "status.txt"</code></para>
    ///   <para><code>True</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "GcsObject"), OutputType(typeof(bool))]
    public class TestGcsObjectCmdlet : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the containing bucket. Will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformationAttribute(Property = "Name", TypeToTransform = typeof(Bucket))]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to check for.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public string ObjectName { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetStorageService();

            // Unfortunately there is no way to test if an object exists on the API, so we
            // just issue a GET and intercept the 404 case.
            try
            {
                ObjectsResource.GetRequest objGetReq = service.Objects.Get(Bucket, ObjectName);
                objGetReq.Projection = ObjectsResource.GetRequest.ProjectionEnum.Full;
                objGetReq.Execute();

                WriteObject(true);
            }
            catch (GoogleApiException ex) when (ex.Error.Code == 404)
            {
                WriteObject(false);
            }
        }
    }
}
