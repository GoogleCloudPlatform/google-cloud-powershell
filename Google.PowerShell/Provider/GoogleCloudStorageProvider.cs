// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.CloudResourceManager.v1;
using Google.Apis.CloudResourceManager.v1.Data;
using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.Apis.Upload;
using Google.PowerShell.Common;
using Google.PowerShell.Provider;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace Google.PowerShell.CloudStorage
{
    /// <summary>
    /// A powershell provider that connects to Google Cloud Storage.
    /// </summary>
    [CmdletProvider(ProviderName, ProviderCapabilities.ShouldProcess)]
    public class GoogleCloudStorageProvider : NavigationCmdletProvider, IContentCmdletProvider
    {
        /// <summary>
        /// Dynamic parameters for "Set-Content".
        /// </summary>
        public class GcsGetContentWriterDynamicParameters
        {
            [Parameter]
            public string ContentType { get; set; }
        }

        /// <summary>
        /// Dynamic paramters for Copy-Item.
        /// </summary>
        public class GcsCopyItemDynamicParameters
        {
            [Parameter]
            public ObjectsResource.CopyRequest.DestinationPredefinedAclEnum? DestinationAcl { get; set; }

            [Parameter]
            public long? SourceGeneration { get; set; }
        }

        /// <summary>
        /// Dynamic paramters for New-Item with an object path.
        /// </summary>
        public class NewGcsObjectDynamicParameters
        {

            /// <summary>
            /// <para type="description">
            /// Local path to the file to upload.
            /// </para>
            /// </summary>
            [Parameter]
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
            [Parameter]
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
            public ObjectsResource.InsertMediaUpload.PredefinedAclEnum? PredefinedAcl { get; set; }
        }

        /// <summary>
        /// Dynamic paramters for New-Item with a bucket path.
        /// </summary>
        public class NewGcsBucketDynamicParameters
        {
            /// <summary>
            /// <para type="description">
            /// The name of the project associated with the command. If not set via PowerShell parameter processing, will
            /// default to the Cloud SDK's DefaultProject property.
            /// </para>
            /// </summary>
            [Parameter]
            [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
            public string Project { get; set; }

            /// <summary>
            /// <para type="description">
            /// Storage class for the bucket. STANDARD, NEARLINE, or DURABLE_REDUCED_AVAILABILITY. See
            /// https://cloud.google.com/storage/docs/storage-classes for more information.
            /// </para>
            /// </summary>
            [Parameter]
            [ValidateSet("DURABLE_REDUCED_AVAILABILITY", "NEARLINE", "STANDARD", IgnoreCase = true)]
            public string StorageClass { get; set; }

            /// <summary>
            /// <para type="description">
            /// Location for the bucket. e.g. ASIA, EU, US.
            /// </para>
            /// </summary>
            [Parameter]
            [ValidateSet("ASIA", "EU", "US", IgnoreCase = false)]
            public string Location { get; set; }

            /// <summary>
            /// <para type="description">
            /// Default ACL for the bucket. e.g. "publicRead", "private", etc.
            /// </para>
            /// <para type="description">
            /// You cannot set fine-grained (e.g. individual users or domains) ACLs using PowerShell.
            /// Instead please use `gsutil`.
            /// </para>
            /// </summary>
            [Parameter]
            public BucketsResource.InsertRequest.PredefinedAclEnum? DefaultBucketAcl { get; set; }

            /// <summary>
            /// <para type="description">
            /// Default ACL for objects added to the bucket. e.g. "publicReadWrite", "authenticatedRead", etc.
            /// </para>
            /// <para type="description">
            /// You cannot set fine-grained (e.g. individual users or domains) ACLs using PowerShell.
            /// Instead please use `gsutil`.
            /// </para>
            /// </summary>
            [Parameter]
            public BucketsResource.InsertRequest.PredefinedDefaultObjectAclEnum? DefaultObjectAcl { get; set; }
        }

        /// <summary>
        /// The parsed structure of a path.
        /// </summary>
        private class GcsPath
        {
            public enum GcsPathType
            {
                Drive,
                Bucket,
                Object
            }

            public string Bucket { get; } = null;
            public string ObjectPath { get; } = null;

            private GcsPath(string bucket, string objectPath)
            {
                Bucket = bucket;
                ObjectPath = objectPath;
            }

            public GcsPath(Object input) : this(input.Bucket, input.Name) { }

            public static GcsPath Parse(string path)
            {
                string bucket;
                string objectPath;
                if (string.IsNullOrEmpty(path))
                {
                    bucket = null;
                    objectPath = null;
                }
                else
                {
                    int bucketLength = path.IndexOfAny(new[] { '/', '\\' });
                    if (bucketLength < 0)
                    {
                        bucket = path;
                        objectPath = null;
                    }
                    else
                    {
                        bucket = path.Substring(0, bucketLength);
                        objectPath = path.Substring(bucketLength + 1).Replace("\\", "/");
                    }
                }
                return new GcsPath(bucket, objectPath);
            }

            public GcsPathType Type
            {
                get
                {
                    if (string.IsNullOrEmpty(Bucket))
                    {
                        return GcsPathType.Drive;
                    }
                    else if (string.IsNullOrEmpty(ObjectPath))
                    {
                        return GcsPathType.Bucket;
                    }
                    else
                    {
                        return GcsPathType.Object;
                    }
                }
            }

            public override string ToString()
            {
                return $"{Bucket}/{ObjectPath}";
            }

            public string RelativePathToChild(string childObjectPath)
            {
                if (!childObjectPath.StartsWith(ObjectPath ?? ""))
                {
                    throw new InvalidOperationException($"{childObjectPath} does not start with {ObjectPath}");
                }
                return childObjectPath.Substring(ObjectPath?.Length ?? 0);
            }
        }

        /// <summary>
        /// The Google Cloud Storage service.
        /// </summary>
        private static StorageService Service { get; } = GetNewService();

        /// <summary>
        /// This service is used to get all the accessible projects.
        /// </summary>
        private static CloudResourceManagerService ResourceService { get; } =
            new CloudResourceManagerService(GCloudCmdlet.GetBaseClientServiceInitializer());

        /// <summary>
        /// Maps the name of a bucket to a cache of data about the objects in that bucket.
        /// </summary>
        private static Dictionary<string, CacheItem<BucketModel>> BucketModels { get; } =
            new Dictionary<string, CacheItem<BucketModel>>();

        /// <summary>
        /// Maps the name of a bucket to a cahced object describing that bucket.
        /// </summary>
        private static CacheItem<Dictionary<string, Bucket>> BucketCache { get; } =
            new CacheItem<Dictionary<string, Bucket>>(UpdateBucketCache);

        /// <summary>
        /// Reports on the usage of the provider.
        /// </summary>
        private static IReportCmdletResults TelemetryReporter = NewTelemetryReporter();

        private const string ProviderName = "GoogleCloudStorage";

        /// <summary>
        /// A random number generator for progress bar ids.
        /// </summary>
        private static Random ActivityIdGenerator { get; } = new Random();

        /// <summary>
        /// This methods returns a new storage service.
        /// </summary>
        private static StorageService GetNewService()
        {
            return new StorageService(GCloudCmdlet.GetBaseClientServiceInitializer());
        }

        private static IReportCmdletResults NewTelemetryReporter()
        {
            if (CloudSdkSettings.GetOptIntoUsageReporting())
            {
                string clientID = CloudSdkSettings.GetAnoymousClientID();
                return new GoogleAnalyticsCmdletReporter(clientID, AnalyticsEventCategory.ProviderInvocation);
            }
            else
            {
                return new InMemoryCmdletResultReporter();
            }
        }

        /// <summary>
        /// Creates a default Google Cloud Storage drive named gs.
        /// </summary>
        /// <returns>A single drive named gs.</returns>
        protected override Collection<PSDriveInfo> InitializeDefaultDrives()
        {
            return new Collection<PSDriveInfo>
            {
                new PSDriveInfo("gs", ProviderInfo, "", ProviderName, PSCredential.Empty)
            };
        }

        /// <summary>
        /// Dispose the resources used by the provider. Specifically the services.
        /// </summary>
        protected override void Stop()
        {
            Service.Dispose();
            ResourceService.Dispose();
            base.Stop();
        }

        /// <summary>
        /// Checks if a path is a legal string of characters. Shoudl pretty much always return null.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>True if GcsPath.Parse() can parse it.</returns>
        protected override bool IsValidPath(string path)
        {
            return true;
        }

        /// <summary>
        /// PowerShell uses this to check if items exist.
        /// </summary>
        protected override bool ItemExists(string path)
        {
            var gcsPath = GcsPath.Parse(path);
            switch (gcsPath.Type)
            {
                case GcsPath.GcsPathType.Drive:
                    return true;
                case GcsPath.GcsPathType.Bucket:
                    var bucketCache = BucketCache.Value;
                    if (bucketCache.ContainsKey(gcsPath.Bucket))
                    {
                        return true;
                    }
                    try
                    {
                        var bucket = Service.Buckets.Get(gcsPath.Bucket).Execute();
                        bucketCache[bucket.Name] = bucket;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case GcsPath.GcsPathType.Object:
                    BucketModel model = GetBucketModel(gcsPath.Bucket);
                    bool objectExists = model.ObjectExists(gcsPath.ObjectPath);
                    return objectExists;
                default:
                    throw new InvalidOperationException($"Unknown Path Type {gcsPath.Type}");
            }
        }

        /// <summary>
        /// PowerShell uses this to check if an item is a container. All drives, all buckets, objects that end
        /// with "/", and prefixes to objects are containers.
        /// </summary>
        /// <param name="path">The path of the item to check.</param>
        /// <returns>True if the item at the path is a container.</returns>
        protected override bool IsItemContainer(string path)
        {
            var gcsPath = GcsPath.Parse(path);
            switch (gcsPath.Type)
            {
                case GcsPath.GcsPathType.Drive:
                case GcsPath.GcsPathType.Bucket:
                    return true;
                case GcsPath.GcsPathType.Object:
                    return GetBucketModel(gcsPath.Bucket).IsContainer(gcsPath.ObjectPath);
                default:
                    throw new InvalidOperationException($"Unknown GcsPathType {gcsPath.Type}");
            }
        }

        /// <summary>
        /// Checks if a container actually contains items.
        /// </summary>
        /// <param name="path">The path to the container.</param>
        /// <returns>True if the container contains items.</returns>
        protected override bool HasChildItems(string path)
        {
            var gcsPath = GcsPath.Parse(path);
            switch (gcsPath.Type)
            {
                case GcsPath.GcsPathType.Drive:
                    return true;
                case GcsPath.GcsPathType.Bucket:
                case GcsPath.GcsPathType.Object:
                    return GetBucketModel(gcsPath.Bucket).HasChildren(gcsPath.ObjectPath);
                default:
                    throw new InvalidOperationException($"Unknown Path Type {gcsPath.Type}");
            }
        }

        /// <summary>
        /// Writes the object describing the item to the output. Used by Get-Item.
        /// </summary>
        /// <param name="path">The path of the item to get.</param>
        protected override void GetItem(string path)
        {
            var gcsPath = GcsPath.Parse(path);
            switch (gcsPath.Type)
            {
                case GcsPath.GcsPathType.Drive:
                    WriteItemObject(PSDriveInfo, path, true);
                    break;
                case GcsPath.GcsPathType.Bucket:
                    Bucket bucket;
                    var bucketCache = BucketCache.Value;
                    if (bucketCache.ContainsKey(gcsPath.Bucket))
                    {
                        bucket = bucketCache[gcsPath.Bucket];
                    }
                    else
                    {
                        bucket = Service.Buckets.Get(gcsPath.Bucket).Execute();
                    }
                    WriteItemObject(bucket, path, true);
                    break;
                case GcsPath.GcsPathType.Object:
                    Object gcsObject = GetBucketModel(gcsPath.Bucket).GetGcsObject(gcsPath.ObjectPath);
                    WriteItemObject(gcsObject, path, IsItemContainer(path));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown Path Type {gcsPath.Type}");
            }
            TelemetryReporter.ReportSuccess(nameof(GoogleCloudStorageProvider), nameof(GetItem));
        }

        /// <summary>
        /// Writes the names of the children of the container to the output. Used for tab-completion.
        /// </summary>
        /// <param name="path">The path to the container to get the children of.</param>
        /// <param name="returnContainers">The names of the children of the container.</param>
        protected override void GetChildNames(string path, ReturnContainers returnContainers)
        {
            var gcsPath = GcsPath.Parse(path);
            if (gcsPath.Type == GcsPath.GcsPathType.Drive)
            {
                foreach (var bucket in ListAllBuckets())
                {

                    WriteItemObject(GetChildName(bucket.Name), bucket.Name, true);
                }
            }
            else
            {
                foreach (Object child in ListChildren(gcsPath, false, false))
                {
                    var childGcsPath = new GcsPath(child);
                    bool isContainer = IsItemContainer(childGcsPath.ToString());
                    string childName = GetChildName(childGcsPath.ToString());
                    WriteItemObject(childName, childGcsPath.ToString().TrimEnd('/'), isContainer);
                }
            }
            TelemetryReporter.ReportSuccess(nameof(GoogleCloudStorageProvider), nameof(GetChildNames));
        }

        /// <summary>
        /// Writes the object descriptions of the items in the container to the output. Used by Get-ChildItem.
        /// </summary>
        /// <param name="path">The path of the container.</param>
        /// <param name="recurse">If true, get all descendents of the container, not just immediate children.</param>
        protected override void GetChildItems(string path, bool recurse)
        {
            var gcsPath = GcsPath.Parse(path);
            switch (gcsPath.Type)
            {
                case GcsPath.GcsPathType.Drive:
                    foreach (Bucket bucket in ListAllBuckets())
                    {
                        WriteItemObject(bucket, bucket.Name, true);
                        if (recurse)
                        {
                            try
                            {
                                GetChildItems(bucket.Name, true);
                            }
                            // It is possible to not have access to ojbects even if we have access to the bucket.
                            // We ignore those objects as if they did not exist.
                            catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.Forbidden) { }
                            catch (AggregateException e)
                            {
                                foreach (Exception innerException in e.InnerExceptions)
                                {
                                    WriteError(new ErrorRecord(
                                        innerException, null, ErrorCategory.NotSpecified, bucket.Name));
                                }
                            }
                            catch (Exception e)
                            {
                                WriteError(new ErrorRecord(e, null, ErrorCategory.NotSpecified, bucket.Name));
                            }
                        }
                    }
                    break;
                case GcsPath.GcsPathType.Bucket:
                case GcsPath.GcsPathType.Object:
                    if (IsItemContainer(path))
                    {
                        foreach (Object gcsObject in ListChildren(gcsPath, recurse))
                        {
                            string gcsObjectPath = new GcsPath(gcsObject).ToString();
                            bool isContainer = IsItemContainer(gcsObjectPath);
                            WriteItemObject(gcsObject, gcsObjectPath, isContainer);
                        }
                    }
                    else
                    {
                        GetItem(path);
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unknown Path Type {gcsPath.Type}");
            }
            TelemetryReporter.ReportSuccess(nameof(GoogleCloudStorageProvider), nameof(GetChildItems));
        }

        /// <summary>
        /// Creates a new item at the given path.
        /// </summary>
        /// <param name="path">The path of the item ot create.</param>
        /// <param name="itemTypeName">The type of item to create. "Directory" is the only special one.
        /// That will create an object with a name ending in "/".</param>
        /// <param name="newItemValue">The value of the item to create. We assume it is a string.</param>
        protected override void NewItem(string path, string itemTypeName, object newItemValue)
        {
            if (!ShouldProcess(path, "New-Item"))
            {
                return;
            }
            bool newFolder = itemTypeName == "Directory";
            if (newFolder && !path.EndsWith("/"))
            {
                path += "/";
            }
            var gcsPath = GcsPath.Parse(path);
            switch (gcsPath.Type)
            {
                case GcsPath.GcsPathType.Drive:
                    throw new InvalidOperationException("Use New-PSDrive to create a new drive.");
                case GcsPath.GcsPathType.Bucket:
                    Bucket newBucket = NewBucket(gcsPath, (NewGcsBucketDynamicParameters)DynamicParameters);
                    WriteItemObject(newBucket, path, true);
                    break;
                case GcsPath.GcsPathType.Object:
                    var dynamicParameters = (NewGcsObjectDynamicParameters)DynamicParameters;
                    Stream contentStream = GetContentStream(newItemValue, dynamicParameters);
                    Object newObject = NewObject(gcsPath, dynamicParameters, contentStream);
                    WriteItemObject(newObject, path, newFolder);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown Path Type {gcsPath.Type}");
            }
            BucketModels.Clear();
            TelemetryReporter.ReportSuccess(nameof(GoogleCloudStorageProvider), nameof(NewItem));
        }

        protected override object NewItemDynamicParameters(string path, string itemTypeName, object newItemValue)
        {
            var gcsPath = GcsPath.Parse(path);
            switch (gcsPath.Type)
            {
                case GcsPath.GcsPathType.Drive:
                    return null;
                case GcsPath.GcsPathType.Bucket:
                    return new NewGcsBucketDynamicParameters();
                case GcsPath.GcsPathType.Object:
                    return new NewGcsObjectDynamicParameters();
                default:
                    return null;
            }
        }

        /// <summary>
        /// Copies a Google Cloud Storage object or folder to another object or folder. Used by Copy-Item.
        /// </summary>
        /// <param name="path">The path to copy from.</param>
        /// <param name="copyPath">The path to copy to.</param>
        /// <param name="recurse">If true, will copy all decendent objects as well.</param>
        protected override void CopyItem(string path, string copyPath, bool recurse)
        {
            if (!ShouldProcess($"Copy-Item from {path} to {copyPath}"))
            {
                return;
            }
            var dyanmicParameters = (GcsCopyItemDynamicParameters)DynamicParameters;
            if (recurse)
            {
                path = path.TrimEnd('\\') + "\\";
                copyPath = copyPath.TrimEnd('\\') + "\\";
            }
            var gcsPath = GcsPath.Parse(path);
            var gcsCopyPath = GcsPath.Parse(copyPath);
            if (recurse)
            {
                IEnumerable<Object> children = ListChildren(gcsPath, true);
                foreach (Object child in children)
                {
                    string objectSubPath = gcsPath.RelativePathToChild(child.Name);
                    string destinationObject = GcsPath.Parse(MakePath(copyPath, objectSubPath)).ObjectPath;
                    ObjectsResource.CopyRequest childRequest = Service.Objects.Copy(null, child.Bucket,
                        child.Name, gcsCopyPath.Bucket, destinationObject);
                    childRequest.SourceGeneration = dyanmicParameters.SourceGeneration;
                    childRequest.DestinationPredefinedAcl = dyanmicParameters.DestinationAcl;
                    childRequest.Projection = ObjectsResource.CopyRequest.ProjectionEnum.Full;
                    Object childObject = childRequest.Execute();
                    bool isContainer = (new GcsPath(childObject).Type != GcsPath.GcsPathType.Object);
                    WriteItemObject(childObject, copyPath, isContainer);
                }
            }

            if (!recurse || GetBucketModel(gcsPath.Bucket).IsReal(gcsPath.ObjectPath))
            {
                ObjectsResource.CopyRequest request =
                    Service.Objects.Copy(null, gcsPath.Bucket, gcsPath.ObjectPath, gcsCopyPath.Bucket,
                        gcsCopyPath.ObjectPath);
                request.SourceGeneration = dyanmicParameters.SourceGeneration;
                request.DestinationPredefinedAcl = dyanmicParameters.DestinationAcl;
                request.Projection = ObjectsResource.CopyRequest.ProjectionEnum.Full;
                Object response = request.Execute();
                WriteItemObject(response, copyPath, gcsCopyPath.Type != GcsPath.GcsPathType.Object);
            }
            BucketModels.Clear();
            TelemetryReporter.ReportSuccess(nameof(GoogleCloudStorageProvider), nameof(CopyItem));
        }

        protected override object CopyItemDynamicParameters(string path, string destination, bool recurse)
        {
            return new GcsCopyItemDynamicParameters();
        }

        /// <summary>
        /// Gets a content reader to read the contents of a downloaded Google Cloud Storage object.
        /// Used by Get-Contents.
        /// </summary>
        /// <param name="path">The path to the object to read.</param>
        /// <returns>A content reader of the contents of a given object.</returns>
        public IContentReader GetContentReader(string path)
        {
            var gcsPath = GcsPath.Parse(path);
            if (gcsPath.ObjectPath == null)
            {
                throw new InvalidOperationException($"Can not get the contents of a {gcsPath.Type}");
            }

            Object gcsObject = Service.Objects.Get(gcsPath.Bucket, gcsPath.ObjectPath).Execute();

            var stream = Service.HttpClient.GetStreamAsync(gcsObject.MediaLink).Result;
            IContentReader contentReader = new GcsStringReader(stream);

            TelemetryReporter.ReportSuccess(nameof(GoogleCloudStorageProvider), nameof(GetContentReader));
            return contentReader;
        }

        /// <summary>
        /// Required by IContentCmdletProvider, along with GetContentReader(string). Returns null because we
        /// have no need for dynamic parameters on Get-Content.
        /// </summary>
        public object GetContentReaderDynamicParameters(string path)
        {
            return null;
        }

        /// <summary>
        /// Gets a writer used to upload data to a Google Cloud Storage object. Used by Set-Content.
        /// </summary>
        /// <param name="path">The path of the object to upload to.</param>
        /// <returns>The writer.</returns>
        public IContentWriter GetContentWriter(string path)
        {
            var gcsPath = GcsPath.Parse(path);
            Object body = new Object
            {
                Name = gcsPath.ObjectPath,
                Bucket = gcsPath.Bucket
            };
            var inputStream = new AnonymousPipeServerStream(PipeDirection.Out);
            var outputStream = new AnonymousPipeClientStream(PipeDirection.In, inputStream.ClientSafePipeHandle);
            var contentType = ((GcsGetContentWriterDynamicParameters)DynamicParameters).ContentType ?? GcsCmdlet.UTF8TextMimeType;
            ObjectsResource.InsertMediaUpload request =
                Service.Objects.Insert(body, gcsPath.Bucket, outputStream, contentType);
            request.UploadAsync();
            IContentWriter contentWriter = new GcsContentWriter(inputStream);
            // Force the bucket models to refresh with the potentially new object.
            BucketModels.Clear();
            TelemetryReporter.ReportSuccess(nameof(GoogleCloudStorageProvider), nameof(GetContentWriter));
            return contentWriter;
        }

        public object GetContentWriterDynamicParameters(string path)
        {
            return new GcsGetContentWriterDynamicParameters();
        }

        /// <summary>
        /// Clears the content of an object. Used by Clear-Content.
        /// </summary>
        /// <param name="path">The path of the object to clear.</param>
        public void ClearContent(string path)
        {
            if (!ShouldProcess(path, "Clear-Content"))
            {
                return;
            }
            var gcsPath = GcsPath.Parse(path);
            Object body = new Object
            {
                Name = gcsPath.ObjectPath,
                Bucket = gcsPath.Bucket
            };
            var memoryStream = new MemoryStream();
            var contentType = GcsCmdlet.UTF8TextMimeType;
            ObjectsResource.InsertMediaUpload request =
                Service.Objects.Insert(body, gcsPath.Bucket, memoryStream, contentType);
            IUploadProgress response = request.Upload();
            if (response.Exception != null)
            {
                throw response.Exception;
            }
            TelemetryReporter.ReportSuccess(nameof(GoogleCloudStorageProvider), nameof(ClearContent));
        }

        public object ClearContentDynamicParameters(string path)
        {
            return null;
        }

        /// <summary>
        /// Deletes a Google Cloud Storage object or bucket. Used by Remove-Item.
        /// </summary>
        /// <param name="path">The path to the object or bucket to remove.</param>
        /// <param name="recurse">If true, will remove the desendants of the item as well. Required for a
        /// non-empty bucket.</param>
        protected override void RemoveItem(string path, bool recurse)
        {
            if (!ShouldProcess(path, "Remove-Item"))
            {
                return;
            }
            var gcsPath = GcsPath.Parse(path);
            switch (gcsPath.Type)
            {
                case GcsPath.GcsPathType.Drive:
                    throw new InvalidOperationException("Use Remove-PSDrive to remove a drive.");
                case GcsPath.GcsPathType.Bucket:
                    RemoveBucket(gcsPath, recurse);
                    BucketCache.ForceRefresh();
                    break;
                case GcsPath.GcsPathType.Object:
                    if (IsItemContainer(path))
                    {
                        RemoveFolder(GcsPath.Parse(path + "/"), recurse);
                    }
                    else
                    {
                        Service.Objects.Delete(gcsPath.Bucket, gcsPath.ObjectPath).Execute();
                    }
                    BucketModels.Clear();
                    break;
                default:
                    throw new InvalidOperationException($"Unknown Path Type {gcsPath.Type}");
            }
            TelemetryReporter.ReportSuccess(nameof(GoogleCloudStorageProvider), nameof(RemoveItem));
        }

        protected override object RemoveItemDynamicParameters(string path, bool recurse)
        {
            return null;
        }

        private void RemoveFolder(GcsPath gcsPath, bool recurse)
        {
            if (GetBucketModel(gcsPath.Bucket).IsReal(gcsPath.ObjectPath))
            {
                Service.Objects.Delete(gcsPath.Bucket, gcsPath.ObjectPath).Execute();
            }
            if (recurse)
            {
                foreach (var childObject in ListChildren(gcsPath, true))
                {
                    Service.Objects.Delete(childObject.Bucket, childObject.Name).Execute();
                }
            }
        }

        private void RemoveBucket(GcsPath gcsPath, bool removeObjects)
        {
            if (removeObjects)
            {
                DeleteObjects(gcsPath);
            }
            try
            {
                Service.Buckets.Delete(gcsPath.Bucket).Execute();
            }
            catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.Conflict)
            {
                // The objects my not have been cleared yet.
                Service.Buckets.Delete(gcsPath.Bucket).Execute();
            }
        }

        private void DeleteObjects(GcsPath gcsPath)
        {
            string bucketName = gcsPath.Bucket;
            List<Task<string>> deleteTasks = new List<Task<string>>();

            ObjectsResource.ListRequest request = Service.Objects.List(bucketName);
            do
            {
                Objects gcsObjects = request.Execute();
                foreach (Object gcsObject in gcsObjects.Items ?? Enumerable.Empty<Object>())
                {
                    deleteTasks.Add(Service.Objects.Delete(bucketName, gcsObject.Name).ExecuteAsync());
                }
                request.PageToken = gcsObjects.NextPageToken;
            } while (request.PageToken != null && !Stopping);
            WaitDeleteTasks(deleteTasks);
        }

        /// <summary>
        /// Waits on the list of delete tasks to compelete, updating progress as it does so.
        /// </summary>
        private void WaitDeleteTasks(List<Task<string>> deleteTasks)
        {
            int totalTasks = deleteTasks.Count;
            int activityId = ActivityIdGenerator.Next();
            while (deleteTasks.Count > 0)
            {
                Task<string> deleteTask = Task.WhenAny(deleteTasks).Result;
                deleteTasks.Remove(deleteTask);
                WriteProgress(
                    new ProgressRecord(activityId, "Delete bucket objects", "Deleting objects")
                    {
                        PercentComplete = ((totalTasks - deleteTasks.Count) * 100) / totalTasks,
                        RecordType = ProgressRecordType.Processing
                    });
            }

            WriteProgress(
                new ProgressRecord(activityId, "Delete bucket objects", "Objects deleted")
                {
                    PercentComplete = 100,
                    RecordType = ProgressRecordType.Completed
                });
        }

        private Stream GetContentStream(object newItemValue, NewGcsObjectDynamicParameters dynamicParameters)
        {
            if (dynamicParameters.File != null)
            {
                dynamicParameters.ContentType =
                    dynamicParameters.ContentType ?? GcsCmdlet.InferContentType(dynamicParameters.File);
                return new FileStream(dynamicParameters.File, FileMode.Open);
            }
            else
            {
                dynamicParameters.ContentType = dynamicParameters.ContentType ?? GcsCmdlet.UTF8TextMimeType;
                return new MemoryStream(Encoding.UTF8.GetBytes(newItemValue?.ToString() ?? ""));

            }
        }

        private BucketModel GetBucketModel(string bucket)
        {
            if (!BucketModels.ContainsKey(bucket))
            {
                BucketModels.Add(bucket, new CacheItem<BucketModel>(() => new BucketModel(bucket, Service)));
            }
            return BucketModels[bucket].Value;
        }

        private Object NewObject(GcsPath gcsPath, NewGcsObjectDynamicParameters dynamicParameters, Stream contentStream)
        {

            Object newGcsObject = new Object
            {
                Bucket = gcsPath.Bucket,
                Name = gcsPath.ObjectPath,
                ContentType = dynamicParameters.ContentType
            };

            ObjectsResource.InsertMediaUpload insertReq = Service.Objects.Insert(
                newGcsObject, newGcsObject.Bucket, contentStream, newGcsObject.ContentType);
            insertReq.PredefinedAcl = dynamicParameters.PredefinedAcl;
            insertReq.Projection = ObjectsResource.InsertMediaUpload.ProjectionEnum.Full;

            IUploadProgress finalProgress = insertReq.Upload();
            if (finalProgress.Exception != null)
            {
                throw finalProgress.Exception;
            }

            return insertReq.ResponseBody;
        }

        private Bucket NewBucket(GcsPath gcsPath, NewGcsBucketDynamicParameters dynamicParams)
        {
            if (dynamicParams.Project == null)
            {
                var property = dynamicParams.GetType().GetProperty(nameof(Project));
                ConfigPropertyNameAttribute configPropertyName =
                    (ConfigPropertyNameAttribute)Attribute.GetCustomAttribute(
                        property, typeof(ConfigPropertyNameAttribute));
                configPropertyName.SetObjectConfigDefault(property, dynamicParams);
            }

            var bucket = new Bucket
            {
                Name = gcsPath.Bucket,
                Location = dynamicParams.Location,
                StorageClass = dynamicParams.StorageClass
            };

            BucketsResource.InsertRequest insertReq = Service.Buckets.Insert(bucket, dynamicParams.Project);
            insertReq.PredefinedAcl = dynamicParams.DefaultBucketAcl;
            insertReq.PredefinedDefaultObjectAcl = dynamicParams.DefaultObjectAcl;
            Bucket newBucket = insertReq.Execute();
            BucketCache.ForceRefresh();
            return newBucket;
        }

        private IEnumerable<Object> ListChildren(GcsPath gcsPath, bool recurse, bool allPages = true)
        {
            ObjectsResource.ListRequest request = Service.Objects.List(gcsPath.Bucket);
            request.Projection = ObjectsResource.ListRequest.ProjectionEnum.Full;
            request.Prefix = gcsPath.ObjectPath;
            if (!string.IsNullOrEmpty(request.Prefix) && !request.Prefix.EndsWith("/"))
            {
                request.Prefix = request.Prefix + "/";
            }
            if (!recurse)
            {
                request.Delimiter = "/";
            }

            do
            {
                Objects response = request.Execute();
                foreach (Object gcsObject in response.Items ?? Enumerable.Empty<Object>())
                {
                    if (gcsObject.Name != request.Prefix)
                    {
                        GetBucketModel(gcsPath.Bucket).AddObject(gcsObject);
                        yield return gcsObject;
                    }
                }
                foreach (string prefix in response.Prefixes ?? Enumerable.Empty<string>())
                {
                    yield return new Object { Name = $"{prefix}", Bucket = gcsPath.Bucket };
                }
                request.PageToken = response.NextPageToken;
            } while (allPages && !Stopping && request.PageToken != null);
        }

        private static async Task<IEnumerable<Bucket>> ListBucketsAsync(Project project)
        {
            // Using a new service on every request here ensures they can all be handled at the same time.
            BucketsResource.ListRequest request = GetNewService().Buckets.List(project.ProjectId);
            var allBuckets = new List<Bucket>();
            try
            {
                do
                {
                    Buckets buckets = await request.ExecuteAsync();
                    allBuckets.AddRange(buckets.Items ?? Enumerable.Empty<Bucket>());
                    request.PageToken = buckets.NextPageToken;
                } while (request.PageToken != null);
            }
            catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.Forbidden) { }
            return allBuckets;
        }

        private static IEnumerable<Project> ListAllProjects()
        {
            ProjectsResource.ListRequest request = ResourceService.Projects.List();
            do
            {
                ListProjectsResponse projects = request.Execute();
                foreach (Project project in projects.Projects ?? Enumerable.Empty<Project>())
                {
                    // The Storage Service considers invactive projects to not exist.
                    if (project.LifecycleState == "ACTIVE")
                    {
                        yield return project;
                    }
                }
                request.PageToken = projects.NextPageToken;
            } while (request.PageToken != null);
        }

        private IEnumerable<Bucket> ListAllBuckets()
        {
            return BucketCache.Value.Values;
        }

        private static Dictionary<string, Bucket> UpdateBucketCache()
        {
            List<Project> projects = ListAllProjects().ToList();
            // Use ToList() to start all the tasks.
            List<Task<IEnumerable<Bucket>>> tasks = projects.Select(ListBucketsAsync).ToList();
            IEnumerable<Bucket> buckets = tasks.SelectMany(task => task.Result);
            return buckets.ToDictionary(bucket => bucket.Name);
        }
    }
}
