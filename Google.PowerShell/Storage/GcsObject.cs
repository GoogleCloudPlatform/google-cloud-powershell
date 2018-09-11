// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Download;
using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.PowerShell.Common;
using Google.PowerShell.Provider;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Net;
using System.Text;
using static Google.Apis.Storage.v1.ObjectsResource.InsertMediaUpload;

namespace Google.PowerShell.CloudStorage
{
    /// <summary>
    /// Base class for Cloud Storage Object cmdlets. Used to reuse common methods.
    /// </summary>
    public abstract class GcsObjectCmdlet : GcsCmdlet
    {
        protected static readonly string ContentTypeKeyMetadata = "Content-Type";

        protected static readonly string ContentEncodingKeyMetadata = "Content-Encoding";

        protected static readonly string CacheControlKeyMetadata = "Cache-Control";

        protected static readonly string ContentDispositionKeyMetadata = "Content-Disposition";

        protected static readonly string ContentLanguageKeyMetadata = "Content-Language";

        private static string[] FixedKeysMetadata = new string[] {
            ContentTypeKeyMetadata, ContentEncodingKeyMetadata, CacheControlKeyMetadata,
            ContentDispositionKeyMetadata, ContentLanguageKeyMetadata
        };

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
        /// Uploads a local file to Google Cloud storage, overwriting any existing object and clobber existing metadata
        /// values.
        /// </summary>
        protected Object UploadGcsObject(
            StorageService service, string bucket, string objectName,
            Stream contentStream, string contentType,
            PredefinedAclEnum? predefinedAcl, Dictionary<string, string> metadata,
            string cacheControl = null, string contentDisposition = null,
            string contentEncoding = null, string contentLanguage = null)
        {
            Object newGcsObject = new Object
            {
                Bucket = bucket,
                Name = objectName,
                ContentType = contentType,
                ContentEncoding = contentEncoding,
                ContentDisposition = contentDisposition,
                CacheControl = cacheControl,
                ContentLanguage = contentLanguage
            };

            if (metadata != null)
            {
                // Handles fixed-key metadata. Removes them so there won't be duplicate. See:
                // https://cloud.google.com/storage/docs/metadata#mutable
                foreach (string fixedKeyMetadata in FixedKeysMetadata)
                {
                    if (metadata.ContainsKey(fixedKeyMetadata))
                    {
                        metadata.Remove(fixedKeyMetadata);
                    }
                }

                // Other metadata pairs will be custom metadata.
                if (metadata.Count != 0)
                {
                    newGcsObject.Metadata = metadata;
                }
            }

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

        /// <summary>
        /// Patch the GCS object with new metadata.
        /// </summary>
        protected Object UpdateObjectMetadata(
            StorageService service, Object storageObject, Dictionary<string, string> metadata)
        {
            storageObject.Metadata = metadata;

            ObjectsResource.PatchRequest patchReq = service.Objects.Patch(storageObject, storageObject.Bucket,
                storageObject.Name);
            patchReq.Projection = ObjectsResource.PatchRequest.ProjectionEnum.Full;

            return patchReq.Execute();
        }

        /// <summary>
        /// Gets and performs action on bucket and prefix name if the cmdlet is in Google Cloud Storage provider location.
        /// For example, if we are in gs:\my-bucket\my-folder\my-subfolder, the array returned will be { "my-bucket", "my-folder\my-subfolder" }
        /// </summary>
        protected void PerformActionOnGcsProviderBucketAndPrefix(System.Action<string> actionOnBucket, System.Action<string> actionOnPrefix)
        {
            // Check whether our current location is in gs:\ (i.e., we are in the Google Cloud Storage provider).
            if (SessionState?.Path?.CurrentLocation?.Provider?.ImplementingType == typeof(GoogleCloudStorageProvider))
            {
                string providerPath = SessionState.Path.CurrentLocation.ProviderPath;
                // Path is of the form <bucket-name>\prefix.
                if (!string.IsNullOrWhiteSpace(providerPath))
                {
                    string[] result = providerPath.Split(new char[] { Path.DirectorySeparatorChar }, 2);
                    actionOnBucket(result[0]);
                    if (result.Length == 2)
                    {
                        if (!result[1].EndsWith("/"))
                        {
                            result[1] += "/";
                        }
                        actionOnPrefix(result[1]);
                    }
                    return;
                }
            }
            return;
        }

        /// <summary>
        /// Replace \ with / in path to comply with GCS path
        /// </summary>
        protected static string ConvertLocalToGcsFolderPath(string localFilePath)
        {
            return localFilePath.Replace(Path.DirectorySeparatorChar, '/');
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Uploads a local file or folder into a Google Cloud Storage bucket.
    /// </para>
    /// <para type="description">
    /// Uploads a local file or folder into a Google Cloud Storage bucket. You can set the value of the new object
    /// directly with -Value, read it from a file with -File, or define neither to create an empty object. You
    /// can also upload an entire folder by giving the folder path to -Folder. However, you will not be able to
    /// use -ObjectName or -ContentType parameter in this case.
    /// Use this instead of Write-GcsObject when creating a new Google Cloud Storage object. You will get
    /// a warning if the object already exists.
    /// </para>
    /// <para type="description">
    /// If this cmdlet is used when PowerShell is in a Google Cloud Storage Provider location (i.e, the shell's location starts
    /// with gs:\), then you may not need to supply -Bucket. For example, if the location is gs:\my-bucket, the cmdlet will
    /// automatically fill out -Bucket with "my-bucket". If -Bucket is still used, however, whatever value given will override "my-bucket".
    /// If the location is inside a folder on Google Cloud Storage, then the cmdlet will prefix the folder name to the object name.
    /// For example, if the location is gs:\my-bucket\folder-1\folder-2, the cmdlet will prefix "folder-1/folder-2/" to the
    /// object name. If -ObjectNamePrefix is used, the automatically determined folder prefix will be appended to the front
    /// of the value of -ObjectNamePrefix.
    /// </para>
    /// <para type="description">
    /// Note: Most Google Cloud Storage utilities, including the PowerShell Provider and the Google Cloud
    /// Console treat '/' as a path separator. They do not, however, treat '\' the same. If you wish to create
    /// an empty object to treat as a folder, the name should end with '/'.
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GcsObject -Bucket "widget-co-logs" -File "C:\logs\log-000.txt"
    ///   </code>
    ///   <para>
    ///   Upload a local file to GCS. The -ObjectName parameter will default to the file name, "log-000.txt".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> "Hello, World!" | New-GcsObject -Bucket "widget-co-logs" -ObjectName "log-000.txt" `
    ///       -Metadata @{ "logsource" = $env:computername }
    ///   </code>
    ///   <para>Pipe a string to a file on GCS. Sets a custom metadata value.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> cd gs:\my-bucket\my-folder
    ///   PS gs:\my-bucket\my-folder> "Hello, World!" | New-GcsObject -ObjectName "log-000.txt"
    ///   </code>
    ///   <para>Pipe a string to a file on GCS while using the GCS Provider. Here, the object created will be "my-folder/log-000.txt".</para>
    /// </example>
    /// <example>
    ///  <code>PS C:\> New-GcsObject -Bucket "widget-co-logs" -Folder "$env:SystemDrive\inetpub\logs\LogFiles"</code>
    ///   <para>Upload a folder and its contents to GCS. The names of the
    ///   created objects will be relative to the folder.</para>
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
            public const string UploadFolder = "UploadFolder";
        }

        /// <summary>
        /// <para type="description">
        /// The name of the bucket to upload to. Will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = false)]
        [PropertyByTypeTransformation(Property = nameof(Apis.Storage.v1.Data.Bucket.Name),
            TypeToTransform = typeof(Bucket))]
        [ValidateNotNullOrEmpty]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the created Cloud Storage object.
        /// </para>
        /// <para type="description">
        /// If uploading a file, will default to the name of the file if not set.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = ParameterSetNames.ContentsFromString)]
        [Parameter(Position = 1, Mandatory = false, ParameterSetName = ParameterSetNames.ContentsFromFile)]
        public string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Text content to write to the Storage object. Ignored if File or Folder is specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ContentsFromString,
            Position = 2, ValueFromPipeline = true)]
        public string Value { get; set; } = "";

        /// <summary>
        /// <para type="description">
        /// Local path to the file to upload.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ParameterSetName = ParameterSetNames.ContentsFromFile)]
        [ValidateNotNullOrEmpty]
        public string File { get; set; }

        /// <summary>
        /// <para type="description">
        /// Local path to the folder to upload.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ParameterSetName = ParameterSetNames.UploadFolder)]
        [ValidateNotNullOrEmpty]
        public string Folder { get; set; }

        /// <summary>
        /// <para type="description">
        /// When uploading the contents of a directory into Google Cloud Storage, this is the prefix
        /// applied to every object which is uploaded.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.UploadFolder)]
        [ValidateNotNullOrEmpty]
        public string ObjectNamePrefix { get; set; }

        /// <summary>
        /// <para type="description">
        /// Content type of the Cloud Storage object. e.g. "image/png" or "text/plain".
        /// </para>
        /// <para type="description">
        /// For file uploads, the type will be inferred based on the file extension, defaulting to
        /// "application/octet-stream" if no match is found. When passing object content via the
        /// -Value parameter, the type will default to "text/plain; charset=utf-8".
        /// </para>
        /// <para type="description">
        /// If this parameter is specified, will take precedence over any "Content-Type" value
        /// specifed by the -Metadata parameter.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ContentsFromFile)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ContentsFromString)]
        [ValidateNotNullOrEmpty]
        public string ContentType { get; set; }

        /// <summary>
        /// <para type="description">
        /// Content encoding of the Cloud Storage object. e.g. "gzip".
        /// </para>
        /// <para type="description">
        /// This metadata can be used to indcate that an object is compressed, while still
        /// maitaining the object's underlying Content-Type. For example, a text file that
        /// is gazip compressed can have the fact that it's a text file indicated in ContentType
        /// and the fact that it's gzip compressed indicated in ContentEncoding.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string ContentEncoding { get; set; }

        /// <summary>
        /// <para type="description">
        /// Content language of the Cloud Storage object. e.g. "en".
        /// </para>
        /// <para type="description">
        /// This metadata indicates the language(s) that the object is intended for.
        /// Refer to https://www.loc.gov/standards/iso639-2/php/code_list.php
        /// for the supported values of this metadata.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string ContentLanguage { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specifies presentation information about the data being transmitted.
        /// </para>
        /// <para type="description">
        /// This metadata allows you to control presentation style of the content,
        /// for example determining whether an attachment should be automatically displayed
        /// or whether some form of actions from the user should be required to open it.
        /// See https://tools.ietf.org/html/rfc6266.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string ContentDisposition { get; set; }

        /// <summary>
        /// <para type="description">
        /// This metadata specifies two different aspects of how data is served
        /// from Cloud Storage: whether data can be cached and whether data can be transformed.
        /// </para>
        /// <para type="description">
        /// Sets the value to "no-cache" if you do not want the object to be cached.
        /// Sets the value to "max-age=[TIME_IN_SECONDS]" so the object can be cached up to
        /// the specified length of time.
        /// See https://cloud.google.com/storage/docs/metadata#cache-control for more information.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string CacheControl { get; set; }

        /// <summary>
        /// <para type="description">
        /// Set the object's ACL using PredefinedAcl.
        /// "Private__" gives the object owner "OWNER" permission. All other permissions are removed.
        /// "ProjectPrivate" gives permission to the project team based on their roles. Anyone who is part of the team has "READER" permission.
        /// Project owners and project editors have "OWNER" permission. All other permissions are removed.
        /// "AuthenticatedRead" gives the object owner "OWNER" permission and gives all authenticated Google account holders "READER" permission.
        /// All other permissions are removed.
        /// "PublicRead" gives the object owner "OWNER" permission and gives all users "READER" permission. All other permissions are removed.
        /// "BucketOwnerRead" gives the object owner "OWNER" permission and the bucket owner "READER" permission. All other permissions are removed.
        /// "BucketOwnerFullControl" gives the object and bucket owners "OWNER" permission. All other permissions are removed.
        /// </para>
        /// <para type="description">
        /// To set fine-grained (e.g. individual users or domains) ACLs using PowerShell, use Add-GcsObjectAcl cmdlets.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public PredefinedAclEnum? PredefinedAcl { get; set; }

        /// <summary>
        /// <para type="description">
        /// Provide metadata for the Cloud Storage object(s). Note that some values, such as "Content-Type", "Content-MD5",
        /// "ETag" have a special meaning to Cloud Storage.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public Hashtable Metadata { get; set; }

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

            Dictionary<string, string> metadataDict = ConvertToDictionary(Metadata);

            // Content type to use for the new object.
            string objContentType = null;
            Stream contentStream = null;

            // If we are in Google Cloud Storage Provider location, resolve the path to get possible bucket name and prefix.
            PerformActionOnGcsProviderBucketAndPrefix(
                bucket => Bucket = Bucket ?? bucket,
                prefix => ObjectNamePrefix = (ObjectNamePrefix == null) ? prefix : Path.Combine(prefix, ObjectNamePrefix));

            if (string.IsNullOrWhiteSpace(Bucket))
            {
                throw new PSArgumentNullException(nameof(Bucket), "Bucket name cannot be determined.");
            }

            if (ParameterSetName == ParameterSetNames.UploadFolder)
            {
                // User gives us the path to a folder, we will resolve the path and upload the contents of that folder.
                // Have to take care of / and \ in the end of the directory path because Path.GetFileName will return
                // an empty string if that is not trimmed off.
                string resolvedFolderPath = GetFullFilePath(Folder).TrimEnd("/\\".ToCharArray());
                if (string.IsNullOrWhiteSpace(resolvedFolderPath) || !Directory.Exists(resolvedFolderPath))
                {
                    throw new DirectoryNotFoundException($"Directory '{resolvedFolderPath}' cannot be found.");
                }

                string gcsObjectNamePrefix = Path.GetFileName(resolvedFolderPath);
                if (!string.IsNullOrWhiteSpace(ObjectNamePrefix))
                {
                    gcsObjectNamePrefix = Path.Combine(ObjectNamePrefix, gcsObjectNamePrefix);
                }
                UploadDirectory(resolvedFolderPath, metadataDict, ConvertLocalToGcsFolderPath(gcsObjectNamePrefix));
                return;
            }

            // ContentsFromFile and ContentsFromString case.
            if (ParameterSetName == ParameterSetNames.ContentsFromFile)
            {
                objContentType = GetFixedTypeMetadata(
                    nameof(ContentType), metadataDict, ContentTypeKeyMetadata, InferContentType(File));
                string qualifiedPath = GetFullFilePath(File);
                if (!System.IO.File.Exists(qualifiedPath))
                {
                    throw new FileNotFoundException("File not found.", qualifiedPath);
                }
                ObjectName = ObjectName ?? Path.GetFileName(File);
                contentStream = new FileStream(qualifiedPath, FileMode.Open);
            }
            else
            {
                // We store string data as UTF-8, which is different from .NET's default encoding
                // (UTF-16). But this simplifies several other issues.
                objContentType = GetFixedTypeMetadata(
                    nameof(ContentType), metadataDict, ContentTypeKeyMetadata, UTF8TextMimeType);
                byte[] contentBuffer = Encoding.UTF8.GetBytes(Value);
                contentStream = new MemoryStream(contentBuffer);
            }
            // If we are in a GCS Provider location, then there may be a object name prefix.
            if (!string.IsNullOrWhiteSpace(ObjectNamePrefix))
            {
                ObjectName = ConvertLocalToGcsFolderPath(Path.Combine(ObjectNamePrefix, ObjectName));
            }

            UploadStreamToGcsObject(contentStream, objContentType, metadataDict, ObjectName);
        }

        /// <summary>
        /// Upload a directory to a GCS bucket, aiming to maintain that directory structure as well.
        /// For example, if we are uploading folder A with file C.txt and subfolder B with file D.txt,
        /// then the bucket should have A\C.txt and A\B\D.txt
        /// </summary>
        private void UploadDirectory(string directory, Dictionary<string, string> metadataDict, string gcsObjectNamePrefix)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return;
            }

            // Confirm that gcsObjectNamePrefix is a GCS folder.
            if (!gcsObjectNamePrefix.EndsWith("/"))
            {
                gcsObjectNamePrefix += "/";
            }

            if (TestObjectExists(Service, Bucket, gcsObjectNamePrefix) && !Force.IsPresent)
            {
                throw new PSArgumentException(
                    $"Storage object '{gcsObjectNamePrefix}' already exists. Use -Force to overwrite.");
            }

            // Create a directory on the cloud.
            string objContentType = GetFixedTypeMetadata(
                string.Empty, metadataDict, ContentTypeKeyMetadata, UTF8TextMimeType);
            Stream contentStream = new MemoryStream();
            UploadStreamToGcsObject(contentStream, objContentType, metadataDict, gcsObjectNamePrefix);

            // TODO(quoct): Add a progress indicator if there are too many files.
            foreach (string enumeratedFile in Directory.EnumerateFiles(directory))
            {
                string fileName = Path.GetFileName(enumeratedFile);
                string fileWithGcsObjectNamePrefix = Path.Combine(gcsObjectNamePrefix, fileName);
                // We have to replace \ with / so it will be created with correct folder structure.
                fileWithGcsObjectNamePrefix = ConvertLocalToGcsFolderPath(fileWithGcsObjectNamePrefix);
                UploadStreamToGcsObject(
                    new FileStream(enumeratedFile, FileMode.Open),
                    GetFixedTypeMetadata(
                        nameof(ContentType), metadataDict, ContentTypeKeyMetadata, InferContentType(enumeratedFile)),
                    metadataDict,
                    ConvertLocalToGcsFolderPath(fileWithGcsObjectNamePrefix));
            }

            // Recursively upload subfolder.
            foreach (string subDirectory in Directory.EnumerateDirectories(directory))
            {
                string subDirectoryName = Path.GetFileName(subDirectory);
                string subDirectoryWithGcsObjectNamePrefix = Path.Combine(gcsObjectNamePrefix, subDirectoryName);
                UploadDirectory(
                    subDirectory,
                    metadataDict,
                    ConvertLocalToGcsFolderPath(subDirectoryWithGcsObjectNamePrefix));
            }
        }

        /// <summary>
        /// Upload a GCS object using a stream.
        /// </summary>
        private void UploadStreamToGcsObject(Stream contentStream, string objContentType, Dictionary<string, string> metadataDict, string objectName)
        {
            if (contentStream == null)
            {
                contentStream = new MemoryStream();
            }

            using (contentStream)
            {
                // We could potentially avoid this extra step by using a special request header.
                //     "If you set the x-goog-if-generation-match header to 0, Google Cloud Storage only
                //     performs the specified request if the object does not currently exist."
                // See https://cloud.google.com/storage/docs/reference-headers#xgoogifgenerationmatch
                bool objectExists = TestObjectExists(Service, Bucket, objectName);
                if (objectExists && !Force.IsPresent)
                {
                    throw new PSArgumentException(
                        $"Storage object '{ObjectName}' already exists. Use -Force to overwrite.");
                }

                string cacheControl =
                    GetFixedTypeMetadata(nameof(CacheControl), metadataDict, CacheControlKeyMetadata);
                string contentDisposition =
                    GetFixedTypeMetadata(nameof(ContentDisposition), metadataDict, ContentDispositionKeyMetadata);
                string contentEncoding =
                    GetFixedTypeMetadata(nameof(ContentEncoding), metadataDict, ContentEncodingKeyMetadata);
                string contentLanguage =
                    GetFixedTypeMetadata(nameof(ContentLanguage), metadataDict, ContentLanguageKeyMetadata);

                Object newGcsObject = UploadGcsObject(
                    Service, Bucket, objectName, contentStream,
                    objContentType, PredefinedAcl,
                    metadataDict, cacheControl, contentDisposition,
                    contentEncoding, contentLanguage);

                WriteObject(newGcsObject);
            }
        }
    }

    // TODO(chrsmith): Support iterating through the result prefixes as well as the items.
    // This is necessary to see the "subfolders" in Cloud Storage, even though the concept
    // does not exist.

    /// <summary>
    /// <para type="synopsis">
    /// Get-GcsObject returns Google Cloud Storage Objects and their metadata.
    /// (Use Read-GcsObject to get its contents.)
    /// </para>
    /// <para type="description">
    /// Given a Google Cloud Storage Bucket, returns Google Cloud Storage Objects and their metadata.
    /// </para>
    /// <para type="description">
    /// If no parameter besides -Bucket is provided, all objects in the bucket are returned.
    /// If a given prefix string is provided, returns all Cloud Storage objects identified
    /// by the prefix string.
    /// </para>
    /// <para type="description">
    /// An optional delimiter may be provided. If used, will return results in a
    /// directory-like mode, delimited by the given string. This means that the names
    /// of all objects returned will not, aside from the prefix, contain the delimiter.
    /// For example, with objects "1", "2", "subdir/3", "subdir/subdir/4",
    /// if the delimiter is "/", only "1" and "2" will be returned.
    /// If the delimiter is "/" and the prefix is "subdir/", only "subdir/3"
    /// will be returned.
    /// </para>
    /// <para type="description">
    /// To gets a specific Cloud Storage Object by name, use the -ObjectName parameter.
    /// This parameter cannot be used together with -Prefix and -Delimiter parameters.
    /// </para>
    /// <para type="description">
    /// If this cmdlet is used when PowerShell is in a Google Cloud Storage Provider location (i.e, the shell's location starts
    /// with gs:\), then you may not need to supply -Bucket. For example, if the location is gs:\my-bucket, the cmdlet will
    /// automatically fill out -Bucket with "my-bucket". If -Bucket is still used, however, whatever value given will override "my-bucket".
    /// If the location is inside a folder on Google Cloud Storage, then the cmdlet will prefix the name of the folder to -ObjectName
    /// if -ObjectName is used. If -ObjectName is not used, the cmdlet will use the name of the folder as a prefix by default if -Prefix
    /// is not used or prefix the folder name to -Prefix if -Prefix is used.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcsObject -Bucket "widget-co-logs" -ObjectName "log-000.txt"</code>
    ///   <para>Get the object name "log-000.txt" and their metadata.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcsObject -Bucket "widget-co-logs"</code>
    ///   <para>Get all objects in a storage bucket.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcsObject -Bucket "widget-co-logs" -Prefix "pictures/winter" -Delimiter "/"</code>
    ///   <para>Get all objects in a specific folder Storage Bucket.</para>
    ///   <para>Because the Delimiter parameter was set, will not return objects under "pictures/winter/2016/".
    ///   The search will omit any objects matching the prefix containing the delimiter.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcsObject -Bucket "widget-co-logs" -Prefix "pictures/winter"</code>
    ///   <para>Get all objects in a specific folder Storage Bucket. Will return objects in pictures/winter/2016/.</para>
    ///   <para>Because the Delimiter parameter was not set, will return objects under "pictures/winter/2016/".</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> cd gs:\my-bucket\my-folder
    ///   PS gs:\my-bucket\my-folder> Get-GcsObject -ObjectName "Blah.txt"
    ///   </code>
    ///   <para>
    ///   Get the object Blah.txt in folder "my-folder" in bucket "my-bucket".
    ///   This has the same effect as "Get-GcsObject -Bucket my-bucket -ObjectName "my-folder/Blah.txt"
    ///   </para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcsObject"), OutputType(typeof(Object))]
    public class GetGcsObjectCmdlet : GcsObjectCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the bucket to check. Will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = false, ValueFromPipeline = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Bucket))]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to inspect.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Object prefix to use. e.g. "/logs/". If not specified all
        /// objects in the bucket will be returned.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Prefix { get; set; }

        /// <summary>
        /// <para type="description">
        /// Returns results in a directory-like mode, delimited by the given string. e.g.
        /// with objects "1, "2", "subdir/3" and delimited "/", "subdir/3" would not be
        /// returned.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Delimiter { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            // If we are in Google Cloud Storage Provider location, resolve the path to get possible bucket name and prefix.
            string gcsProviderPrefix = null;
            PerformActionOnGcsProviderBucketAndPrefix(
                bucket => Bucket = Bucket ?? bucket,
                prefix => gcsProviderPrefix = prefix);

            if (string.IsNullOrWhiteSpace(Bucket))
            {
                throw new PSArgumentNullException(nameof(Bucket), "Bucket name cannot be determined.");
            }

            if (ObjectName != null)
            {
                if (Delimiter != null || Prefix != null)
                {
                    WriteWarning("-Delimiter and -Prefix parameters will be ignored since -ObjectName is given.");
                }

                // Don't ignore the prefix that we get from Google Cloud Storage Provider location.
                // So in this case, if user is in gs:/my-bucket/my-folder and the user runs "Get-GcsObject -ObjectName blah.txt",
                // user will still get the object blah.txt inside the folder.
                if (!string.IsNullOrWhiteSpace(gcsProviderPrefix))
                {
                    ObjectName = ConvertLocalToGcsFolderPath(Path.Combine(gcsProviderPrefix, ObjectName));
                }

                ObjectsResource.GetRequest getReq = Service.Objects.Get(Bucket, ObjectName);
                getReq.Projection = ObjectsResource.GetRequest.ProjectionEnum.Full;
                try
                {
                    Object gcsObject = getReq.Execute();
                    WriteObject(gcsObject);
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    string message = $"Storage object '{ObjectName}' does not exist.";
                    WriteResourceMissingError(message, "ObjectNotFound", ObjectName);
                }
            }
            else
            {
                ObjectsResource.ListRequest listReq = Service.Objects.List(Bucket);
                listReq.Projection = ObjectsResource.ListRequest.ProjectionEnum.Full;
                listReq.Delimiter = Delimiter;
                if (gcsProviderPrefix != null)
                {
                    Prefix = (Prefix == null) ? gcsProviderPrefix : Path.Combine(gcsProviderPrefix, Prefix);
                    Prefix = ConvertLocalToGcsFolderPath(Prefix);
                }
                listReq.Prefix = Prefix;
                listReq.MaxResults = 100;

                // When used with WriteObject, expand the IEnumerable rather than
                // returning the IEnumerable itself. IEnumerable<T> vs. IEnumerable<IEnumerable<T>>.
                const bool enumerateCollection = true;

                // First page.
                Objects gcsObjects = listReq.Execute();
                WriteObject(gcsObjects.Items, enumerateCollection);

                // Keep paging through results as necessary.
                while (!Stopping && gcsObjects.NextPageToken != null)
                {
                    listReq.PageToken = gcsObjects.NextPageToken;
                    gcsObjects = listReq.Execute();
                    WriteObject(gcsObjects.Items, enumerateCollection);
                }
            }
        }
    }

    /// <summary>
    /// Base class for cmdlet that uses either ObjectName and Bucket OR InputObject to access a Google Cloud Storage object.
    /// This class also takes into account whether the cmdlet is in a Google Cloud Storage Provider location or not.
    /// If so, it will try to resolve bucket name and prefix from the current location.
    /// </summary>
    public class GcsObjectWithBucketAndPrefixValidationCmdlet : GcsObjectCmdlet
    {
        public virtual string Bucket { get; set; }
        public virtual string ObjectName { get; set; }
        public virtual Object InputObject { get; set; }

        protected override void ProcessRecord()
        {
            if (InputObject != null)
            {
                Bucket = InputObject.Bucket;
                ObjectName = InputObject.Name;
            }
            else
            {
                // If we are in Google Cloud Storage Provider location, resolve the path to get possible bucket name and prefix.
                PerformActionOnGcsProviderBucketAndPrefix(
                    bucket => Bucket = Bucket ?? bucket,
                    prefix => ObjectName = ConvertLocalToGcsFolderPath(Path.Combine(prefix, ObjectName)));
            }

            if (string.IsNullOrWhiteSpace(Bucket))
            {
                throw new PSArgumentNullException(nameof(Bucket), "Bucket name cannot be determined.");
            }

            if (string.IsNullOrWhiteSpace(ObjectName))
            {
                throw new PSArgumentNullException(nameof(Bucket), "Bucket name cannot be determined.");
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Set-GcsObject updates metadata associated with a Cloud Storage Object.
    /// </para>
    /// <para type="description">
    /// Updates the metadata associated with a Cloud Storage Object, such as ACLs.
    /// </para>
    /// <para type="description">
    /// If this cmdlet is used when PowerShell is in a Google Cloud Storage Provider location (i.e, the shell's location starts
    /// with gs:\), then you may not need to supply -Bucket. For example, if the location is gs:\my-bucket, the cmdlet will
    /// automatically fill out -Bucket with "my-bucket". If -Bucket is still used, however, whatever value given will override "my-bucket".
    /// If the location is inside a folder on Google Cloud Storage, then the cmdlet will prefix the folder name to the object name.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Set-GcsObject -Bucket "widget-co-logs" -ObjectName "my-object" -PredefinedAcl PublicRead</code>
    ///   <para>Sets the ACL on object "my-object" in bucket "widget-co-logs" to PublicRead.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> cd gs:\my-bucket
    ///   PS gs:\my-bucket> Set-GcsObject -ObjectName "my-object" -PredefinedAcl PublicRead
    ///   </code>
    ///   <para>Sets the ACL on object "my-object" in bucket "my-bucket" to PublicRead.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GcsObject")]
    [OutputType(typeof(Object))]
    public class SetGcsObjectCmdlet : GcsObjectWithBucketAndPrefixValidationCmdlet
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
        [Parameter(Position = 0, Mandatory = false, ParameterSetName = ParameterSetNames.FromBucketAndObjName)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Bucket))]
        public override string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to update.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = ParameterSetNames.FromBucketAndObjName)]
        public override string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Storage object instance to update.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true,
            ValueFromPipeline = true, ParameterSetName = ParameterSetNames.FromObject)]
        [Alias("Object")]
        public override Object InputObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// Set the object's ACL using PredefinedAcl.
        /// "Private__" gives the object owner "OWNER" permission. All other permissions are removed.
        /// "ProjectPrivate" gives permission to the project team based on their roles. Anyone who is part of the team has "READER" permission.
        /// Project owners and project editors have "OWNER" permission. All other permissions are removed.
        /// "AuthenticatedRead" gives the object owner "OWNER" permission and gives all authenticated Google account holders "READER" permission.
        /// All other permissions are removed.
        /// "PublicRead" gives the object owner "OWNER" permission and gives all users "READER" permission. All other permissions are removed.
        /// "BucketOwnerRead" gives the object owner "OWNER" permission and the bucket owner "READER" permission. All other permissions are removed.
        /// "BucketOwnerFullControl" gives the object and bucket owners "OWNER" permission. All other permissions are removed.
        /// </para>
        /// <para type="description">
        /// To set fine-grained (e.g. individual users or domains) ACLs using PowerShell, use Add-GcsObjectAcl cmdlets.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public ObjectsResource.UpdateRequest.PredefinedAclEnum? PredefinedAcl { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            // You cannot specify both an ACL list and a predefined ACL using the API. (b/30358979?)
            // We issue a GET + Update. Since we aren't using ETags, there is a potential for a
            // race condition.
            var getReq = Service.Objects.Get(Bucket, ObjectName);
            getReq.Projection = ObjectsResource.GetRequest.ProjectionEnum.Full;
            Object objectInsert = getReq.Execute();
            // The API doesn't allow both predefinedAcl and access controls. So drop existing ACLs.
            objectInsert.Acl = null;

            ObjectsResource.UpdateRequest updateReq = Service.Objects.Update(objectInsert, Bucket, ObjectName);
            updateReq.PredefinedAcl = PredefinedAcl;

            Object gcsObject = updateReq.Execute();
            WriteObject(gcsObject);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes a Cloud Storage object.
    /// </para>
    /// <para type="description">
    /// Deletes a Cloud Storage object.
    /// </para>
    /// <para type="description">
    /// If this cmdlet is used when PowerShell is in a Google Cloud Storage Provider location (i.e, the shell's location starts
    /// with gs:\), then you may not need to supply -Bucket. For example, if the location is gs:\my-bucket, the cmdlet will
    /// automatically fill out -Bucket with "my-bucket". If -Bucket is still used, however, whatever value given will override "my-bucket".
    /// If the location is inside a folder on Google Cloud Storage, then the cmdlet will prefix the folder name to the object name.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcsObject ppiper-prod text-files/14683615 -WhatIf</code>
    ///   <code>What if: Performing the operation "Delete Object" on target "[ppiper-prod]" text-files/14683615".</code>
    ///   <para>Delete storage object named "text-files/14683615".</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> cd gs:\my-bucket
    ///   PS gs:\my-bucket> Remove-GcsObject -ObjectName "my-object"
    ///   </code>
    ///   <para>Removes the storage object "my-object" in bucket "my-bucket".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsObject",
        DefaultParameterSetName = ParameterSetNames.FromName, SupportsShouldProcess = true)]
    public class RemoveGcsObjectCmdlet : GcsObjectWithBucketAndPrefixValidationCmdlet
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
        [Parameter(Position = 0, Mandatory = false, ParameterSetName = ParameterSetNames.FromName)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Bucket))]
        public override string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to delete.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = ParameterSetNames.FromName)]
        public override string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to delete.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.FromObject)]
        [Alias("Object")]
        public override Object InputObject { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            if (!ShouldProcess($"[{Bucket}] {ObjectName}", "Delete Object"))
            {
                return;
            }

            try
            {
                ObjectsResource.DeleteRequest delReq = Service.Objects.Delete(Bucket, ObjectName);
                string result = delReq.Execute();
                if (!string.IsNullOrWhiteSpace(result))
                {
                    WriteObject(result);
                }
            }
            catch (GoogleApiException apiEx) when (apiEx.HttpStatusCode == HttpStatusCode.NotFound)
            {
                ErrorRecord errorRecord = new ErrorRecord(
                    new ItemNotFoundException($"Storage object '{ObjectName}' does not exist."),
                    "ObjectNotFound",
                    ErrorCategory.ObjectNotFound,
                    ObjectName);
                ThrowTerminatingError(errorRecord);
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
    /// <para type="description">
    /// If this cmdlet is used when PowerShell is in a Google Cloud Storage Provider location (i.e, the shell's location starts
    /// with gs:\), then you may not need to supply -Bucket. For example, if the location is gs:\my-bucket, the cmdlet will
    /// automatically fill out -Bucket with "my-bucket". If -Bucket is still used, however, whatever value given will override "my-bucket".
    /// If the location is inside a folder on Google Cloud Storage, then the cmdlet will prefix the folder name to the object name.
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> Read-GcsObject -Bucket "widget-co-logs" -ObjectName "log-000.txt" `
    ///   >>    -OutFile "C:\logs\log-000.txt"
    ///   </code>
    ///   <para>Write the objects of a Storage Object to local disk at "C:\logs\log-000.txt".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Read-GcsObject -Bucket "widget-co-logs" -ObjectName "log-000.txt" | Write-Host</code>
    ///   <para>Returns the storage object's contents as a string.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> cd gs:\my-bucket
    ///   PS gs:\my-bucket> Read-GcsObject -ObjectName "log-000.txt" | Write-Host
    ///   </code>
    ///   <para>Returns contents of the storage object "log-000.txt" in bucket "my-bucket" as a string.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommunications.Read, "GcsObject", DefaultParameterSetName = ParameterSetNames.ByName)]
    [OutputType(typeof(string))] // Not 100% correct, cmdlet will output nothing if -OutFile is specified.
    public class ReadGcsObjectCmdlet : GcsObjectWithBucketAndPrefixValidationCmdlet
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
        [Parameter(Position = 0, Mandatory = false, ParameterSetName = ParameterSetNames.ByName)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Bucket))]
        public override string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to read.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = ParameterSetNames.ByName)]
        public override string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Google Cloud Storage object to read.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true, ValueFromPipeline = true)]
        [Alias("Object")]
        public override Object InputObject { get; set; }

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

            string uri = GetBaseUri(Bucket, ObjectName);
            var downloader = new MediaDownloader(Service);

            // Write object contents to the pipeline if no -OutFile is specified.
            if (string.IsNullOrEmpty(OutFile))
            {
                // Start with a 1MiB buffer. We could get the object's metadata and use its exact
                // file size, but making a web request << just allocating more memory.
                using (var memStream = new MemoryStream(1024 * 1024))
                {
                    var result = downloader.Download(uri, memStream);
                    CheckForError(result);

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
            string qualifiedPath = GetFullFilePath(OutFile);
            bool fileExists = File.Exists(qualifiedPath);
            if (fileExists && !Force.IsPresent)
            {
                throw new PSArgumentException($"File '{qualifiedPath}' already exists. Use -Force to overwrite.");
            }


            using (var writer = new FileStream(qualifiedPath, FileMode.Create))
            {
                var result = downloader.Download(uri, writer);
                CheckForError(result);
            }
        }

        private void CheckForError(IDownloadProgress downloadProgress)
        {
            if (downloadProgress.Status == DownloadStatus.Failed || downloadProgress.Exception != null)
            {
                GoogleApiException googleApiException = downloadProgress.Exception as GoogleApiException;
                if (googleApiException != null && googleApiException.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    ErrorRecord errorRecord = new ErrorRecord(
                        new ItemNotFoundException($"Storage object '{ObjectName}' does not exist."),
                        "ObjectNotFound",
                        ErrorCategory.ObjectNotFound,
                        ObjectName);
                    ThrowTerminatingError(errorRecord);
                }
                // Default to just throwing the exception we get.
                throw downloadProgress.Exception;
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Replaces the contents of a Cloud Storage object.
    /// </para>
    /// <para type="description">
    /// Replaces the contents of a Cloud Storage object with data from the local disk or a value
    /// from the pipeline. Use this instead of New-GcsObject to set the contents of a Google Cloud Storage
    /// object that already exists. You will get a warning if the object does not exist.
    /// </para>
    /// <para type="description">
    /// If this cmdlet is used when PowerShell is in a Google Cloud Storage Provider location (i.e, the shell's location starts
    /// with gs:\), then you may not need to supply -Bucket. For example, if the location is gs:\my-bucket, the cmdlet will
    /// automatically fill out -Bucket with "my-bucket". If -Bucket is still used, however, whatever value given will override "my-bucket".
    /// If the location is inside a folder on Google Cloud Storage, then the cmdlet will prefix the folder name to the object name.
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> Get-GcsObject -Bucket "widget-co-logs" -ObjectName "status.txt" | Write-GcsObject -Value "OK"
    ///   </code>
    ///   <para>Update the contents of the Storage Object piped from Get-GcsObject.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> cd gs:\my-bucket
    ///   PS gs:\my-bucket> Write-GcsObject -ObjectName "log-000.txt" -Value "OK"
    ///   </code>
    ///   <para>Updates the contents of the storage object "log-000.txt" in bucket "my-bucket".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommunications.Write, "GcsObject"), OutputType(typeof(Object))]
    public class WriteGcsObjectCmdlet : GcsObjectWithBucketAndPrefixValidationCmdlet
    {
        private class ParameterSetNames
        {
            // Write the content of a string to a GCS Object supplied directory to the cmdlet.
            public const string ByObjectFromString = "ByObjectFromString";
            // Write the content of a file to a GCS Object supplied directory to the cmdlet.
            public const string ByObjectFromFile = "ByObjectFromFile";
            // Write the content of a string to a GCS Object found using Bucket and ObjectName parameter.
            public const string ByNameFromString = "ByNameFromString";
            // Write the content of a file to a GCS Object found using Bucket and ObjectName parameter.
            public const string ByNameFromFile = "ByNameFromFile";
        }

        /// <summary>
        /// <para type="description">
        /// The Google Cloud Storage object to write to.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObjectFromString,
            Position = 0, Mandatory = true, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByObjectFromFile,
            Position = 0, Mandatory = true, ValueFromPipeline = true)]
        [ValidateNotNull]
        [Alias("Object")]
        public override Object InputObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the bucket containing the object. Will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameFromString, Position = 0, Mandatory = false)]
        [Parameter(ParameterSetName = ParameterSetNames.ByNameFromFile, Position = 0, Mandatory = false)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Bucket))]
        public override string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to write to.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameFromString, Position = 1, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByNameFromFile, Position = 1, Mandatory = true)]
        public override string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Text content to write to the Storage object. Ignored if File is specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameFromString, ValueFromPipeline = true, Mandatory = false)]
        [Parameter(ParameterSetName = ParameterSetNames.ByObjectFromString, ValueFromPipeline = false, Mandatory = false)]
        public string Value { get; set; }

        /// <summary>
        /// <para type="description">
        /// Local file path to read, writing its contents into Cloud Storage.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByNameFromFile, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByObjectFromFile, Mandatory = true)]
        public string File { get; set; }

        /// <summary>
        /// <para type="description">
        /// Content type of the Cloud Storage object. e.g. "image/png" or "text/plain".
        /// </para>
        /// <para type="description">
        /// For file uploads, the type will be inferred based on the file extension, defaulting to
        /// "application/octet-stream" if no match is found. When passing object content via the
        /// -Value parameter, the type will default to "text/plain; charset=utf-8".
        /// </para>
        /// <para>
        /// If this parameter is specified, will take precedence over any "Content-Type" value
        /// specifed by the Metadata parameter.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string ContentType { get; set; }

        /// <summary>
        /// <para type="description">
        /// Content encoding of the Cloud Storage object. e.g. "gzip".
        /// </para>
        /// <para type="description">
        /// This metadata can be used to indcate that an object is compressed, while still
        /// maitaining the object's underlying Content-Type. For example, a text file that
        /// is gazip compressed can have the fact that it's a text file indicated in ContentType
        /// and the fact that it's gzip compressed indicated in ContentEncoding.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string ContentEncoding { get; set; }

        /// <summary>
        /// <para type="description">
        /// Content language of the Cloud Storage object. e.g. "en".
        /// </para>
        /// <para type="description">
        /// This metadata indicates the language(s) that the object is intended for.
        /// Refer to https://www.loc.gov/standards/iso639-2/php/code_list.php
        /// for the supported values of this metadata.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string ContentLanguage { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specifies presentation information about the data being transmitted.
        /// </para>
        /// <para type="description">
        /// This metadata allows you to control presentation style of the content,
        /// for example determining whether an attachment should be automatically displayed
        /// or whether some form of actions from the user should be required to open it.
        /// See https://tools.ietf.org/html/rfc6266.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string ContentDisposition { get; set; }

        /// <summary>
        /// <para type="description">
        /// This metadata specifies two different aspects of how data is served
        /// from Cloud Storage: whether data can be cached and whether data can be transformed.
        /// </para>
        /// <para type="description">
        /// Sets the value to "no-cache" if you do not want the object to be cached.
        /// Sets the value to "max-age=[TIME_IN_SECONDS]" so the object can be cached up to
        /// the specified length of time.
        /// See https://cloud.google.com/storage/docs/metadata#cache-control for more information.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string CacheControl { get; set; }

        // TODO(chrsmith): Support updating an existing object's ACLs. Currently we don't do this because we only
        // support setting canned, default ACLs; which is only allowed by the API when creating new objects.

        /// <summary>
        /// <para type="description">
        /// Metadata for the Cloud Storage object. Values will be merged into the existing object.
        /// To delete a Metadata value, provide an empty string for its value.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public Hashtable Metadata { get; set; }

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

            Stream contentStream;
            if (!string.IsNullOrEmpty(File))
            {
                string qualifiedPath = GetFullFilePath(File);
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
                byte[] contentBuffer = Encoding.Unicode.GetBytes(Value ?? "");
                contentStream = new MemoryStream(contentBuffer);
            }

            // Get the existing storage object so we can use its metadata. (If it does not exist, we will fall back to
            // default values.)
            Object existingGcsObject = InputObject;
            Dictionary<string, string> existingObjectMetadata = null;

            using (contentStream)
            {
                try
                {
                    if (existingGcsObject == null)
                    {
                        ObjectsResource.GetRequest getReq = Service.Objects.Get(Bucket, ObjectName);
                        getReq.Projection = ObjectsResource.GetRequest.ProjectionEnum.Full;

                        existingGcsObject = getReq.Execute();
                    }
                    else
                    {
                        // Set these variables so the call to UploadGcsObject at the end of the function will succeed
                        // when -Force is present and object does not exist.
                        Bucket = existingGcsObject.Bucket;
                        ObjectName = existingGcsObject.Name;
                    }

                    existingObjectMetadata = ConvertToDictionary(existingGcsObject.Metadata);
                    // If the object already has metadata associated with it, we first PATCH the new metadata into the
                    // existing object. Otherwise we would reimplement "metadata merging" logic, and probably get it wrong.
                    if (Metadata != null)
                    {
                        Object existingGcsObjectUpdatedMetadata = UpdateObjectMetadata(
                            Service, existingGcsObject, ConvertToDictionary(Metadata));
                        existingObjectMetadata = ConvertToDictionary(existingGcsObjectUpdatedMetadata.Metadata);
                    }
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    if (!Force.IsPresent)
                    {
                        throw new PSArgumentException(
                            $"Storage object '{ObjectName}' does not exist. Use -Force to ignore.");
                    }
                }

                string contentType = GetFixedTypeMetadata(
                    nameof(ContentType), existingObjectMetadata, ContentTypeKeyMetadata, existingGcsObject?.ContentType ?? OctetStreamMimeType);
                string cacheControl =
                    GetFixedTypeMetadata(nameof(CacheControl), existingObjectMetadata, "Cache-Control", existingGcsObject?.CacheControl);
                string contentDisposition =
                    GetFixedTypeMetadata(nameof(ContentDisposition), existingObjectMetadata, "Content-Disposition", existingGcsObject?.ContentDisposition);
                string contentEncoding =
                    GetFixedTypeMetadata(nameof(ContentEncoding), existingObjectMetadata, "Content-Encoding", existingGcsObject?.ContentEncoding);
                string contentLanguage =
                    GetFixedTypeMetadata(nameof(ContentLanguage), existingObjectMetadata, "Content-Language", existingGcsObject?.ContentLanguage);

                // Rewriting GCS objects is done by simply creating a new object with the
                // same name. (i.e. this is functionally identical to New-GcsObject.)
                //
                // We don't need to worry about data races and/or corrupting data mid-upload
                // because of the upload semantics of Cloud Storage.
                // See: https://cloud.google.com/storage/docs/consistency
                Object updatedGcsObject = UploadGcsObject(
                    Service, Bucket, ObjectName, contentStream,
                    contentType, null /* predefinedAcl */,
                    existingObjectMetadata, cacheControl,
                    contentDisposition, contentEncoding,
                    contentLanguage);

                WriteObject(updatedGcsObject);
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
    /// <para type="description">
    /// If this cmdlet is used when PowerShell is in a Google Cloud Storage Provider location (i.e, the shell's location starts
    /// with gs:\), then you may not need to supply -Bucket. For example, if the location is gs:\my-bucket, the cmdlet will
    /// automatically fill out -Bucket with "my-bucket". If -Bucket is still used, however, whatever value given will override "my-bucket".
    /// If the location is inside a folder on Google Cloud Storage, then the cmdlet will prefix the folder name to the object name.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Test-GcsObject -Bucket "widget-co-logs" -ObjectName "status.txt"</code>
    ///   <para>Test if an object named "status.txt" exists in the bucket "widget-co-logs".</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> cd gs:\my-bucket
    ///   PS gs:\my-bucket> Test-GcsObject -ObjectName "status.txt"
    ///   </code>
    ///   <para>Test if an object named "status.txt" exists in the bucket "my-bucket".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "GcsObject"), OutputType(typeof(bool))]
    public class TestGcsObjectCmdlet : GcsObjectWithBucketAndPrefixValidationCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the containing bucket. Will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = false)]
        [PropertyByTypeTransformationAttribute(Property = "Name", TypeToTransform = typeof(Bucket))]
        public override string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to check for.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public override string ObjectName { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            // Unfortunately there is no way to test if an object exists on the API, so we
            // just issue a GET and intercept the 404 case.
            try
            {
                ObjectsResource.GetRequest objGetReq = Service.Objects.Get(Bucket, ObjectName);
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

    /// <summary>
    /// <para type="synopsis">
    /// Copies a Google Cloud Storage object to another location.
    /// </para>
    /// <para type="description">
    /// Copies a Google Cloud Storage object to another location The target location may be in the same bucket
    /// with a different name or a different bucket with any name.
    /// </para>
    /// <para type="description">
    /// If this cmdlet is used when PowerShell is in a Google Cloud Storage Provider location (i.e, the shell's location starts
    /// with gs:\), then you may not need to supply -Bucket. For example, if the location is gs:\my-bucket, the cmdlet will
    /// automatically fill out -Bucket with "my-bucket". If -Bucket is still used, however, whatever value given will override "my-bucket".
    /// If the location is inside a folder on Google Cloud Storage, then the cmdlet will prefix the folder name to the object name.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Copy-GcsObject -Bucket "widget-co-logs" -ObjectName "status.txt" -DestinationBucket "another-bucket"</code>
    ///   <para>Copy object "status.txt" in bucket "widget-co-logs" to bucket "another-bucket".</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> cd gs:\my-bucket
    ///   PS gs:\my-bucket> Copy-GcsObject -ObjectName "status.txt" -DestinationBucket "another-bucket" -DestinationObjectName "new-name.txt"
    ///   </code>
    ///   <para>Copy object "status.txt" in bucket "my-bucket" to bucket "another-bucket" as "new-name.txt".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Copy, "GcsObject", DefaultParameterSetName = ParameterSetNames.ByObject)]
    [OutputType(typeof(Object))]
    public class CopyGcsObject : GcsObjectWithBucketAndPrefixValidationCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// A Google Cloud Storage object to read from. Can be obtained with Get-GcsObject.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true, ValueFromPipeline = true)]
        public override Object InputObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the bucket containing the object to read from. Will also accept a Bucket object.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = false)]
        [PropertyByTypeTransformation(Property = nameof(Apis.Storage.v1.Data.Bucket.Name), TypeToTransform = typeof(Bucket))]
        [Alias("SourceBucket")]
        public override string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the object to read from.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true)]
        [Alias("SourceObjectName")]
        public override string ObjectName { get; set; }


        /// <summary>
        /// <para type="description">
        /// Name of the bucket in which the copy will reside. Defaults to the source bucket.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0)]
        [PropertyByTypeTransformation(Property = nameof(Apis.Storage.v1.Data.Bucket.Name), TypeToTransform = typeof(Bucket))]
        public string DestinationBucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the copy. Defaults to the name of the source object.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 1)]
        public string DestinationObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will overwrite existing objects without prompt.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            Object gcsObject = InputObject ?? Service.Objects.Get(Bucket, ObjectName).Execute();

            string destinationBucket = DestinationBucket ?? gcsObject.Bucket;
            string destinationObject = DestinationObjectName ?? gcsObject.Name;

            if (!Force)
            {
                try
                {
                    ObjectsResource.GetRequest objGetReq =
                        Service.Objects.Get(destinationBucket, destinationObject);
                    objGetReq.Execute();
                    // If destination does not exist, jump to catch statment.
                    if (!ShouldContinue(
                        "Object exists. Overwrite?", $"{destinationBucket}/{destinationObject}"))
                    {
                        return;
                    }
                }
                catch (GoogleApiException ex) when (ex.Error.Code == 404) { }
            }

            ObjectsResource.CopyRequest request = Service.Objects.Copy(gcsObject,
                gcsObject.Bucket, gcsObject.Name,
                destinationBucket, destinationObject);
            Object response = request.Execute();
            WriteObject(response);
        }
    }
}
