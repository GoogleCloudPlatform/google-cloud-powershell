// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Storage.v1;
using Google.PowerShell.Common;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Google.PowerShell.CloudStorage
{
    /// <summary>
    /// Base class for Google Cloud Storage-based cmdlets.
    /// </summary>
    public abstract class GcsCmdlet : GCloudCmdlet
    {
        /// <summary>
        /// MIME attachment for general binary data. (Octets of bits, commonly referred to as bytes.)
        /// </summary>
        protected const string OctetStreamMimeType = "application/octet-stream";

        /// <summary>
        /// MIME attachment for UTF-8 encoding text.
        /// </summary>
        public const string UTF8TextMimeType = "text/plain; charset=utf-8";

        /// <summary>
        /// The storage service.
        /// </summary>
        public StorageService Service { get; } = DefaultStorageService ?? new StorageService(GetBaseClientServiceInitializer());

        internal static StorageService DefaultStorageService { private get; set; }

        /// <summary>
        /// Constructs the media URL of an object from its bucket and name. This does not include the generation
        /// or any preconditions. The returned string will always have a query parameter, so later query parameters
        /// can unconditionally be appended with an "&amp;" prefix.
        /// </summary>
        protected string GetBaseUri(string bucket, string objectName)
        {
            return $"https://www.googleapis.com/download/storage/v1/b/{bucket}/o/{Uri.EscapeDataString(objectName)}?alt=media";
        }

        /// <summary>
        /// Convert a PowerShell HashTable object into a string/string Dictionary.
        /// </summary>
        protected Dictionary<string, string> ConvertToDictionary(Hashtable hashtable)
        {
            var metadataDictionary = new Dictionary<string, string>();
            if (hashtable == null)
            {
                return metadataDictionary;
            }

            foreach (DictionaryEntry kvp in hashtable)
            {
                metadataDictionary.Add(kvp.Key.ToString(), kvp.Value?.ToString());
            }
            return metadataDictionary;
        }

        /// <summary>
        /// Converts an IDictionary into a Dictionary instance. (This method is preferred over passing it to
        /// the constructor for Dictionary since this will handle the null case.)
        /// </summary>
        protected Dictionary<string, string> ConvertToDictionary(IDictionary<string, string> idict)
        {
            if (idict == null)
            {
                return new Dictionary<string, string>();
            }
            else
            {
                return new Dictionary<string, string>(idict);
            }
        }

        /// <summary>
        /// Infer the MIME type of a non-qualified file path.
        /// Returns octet stream mime type if no match is found.
        /// </summary>
        public static string InferContentType(string file)
        {
            if (file == null)
            {
                return OctetStreamMimeType;
            }

            int index = file.LastIndexOf('.');
            if (index == -1)
            {
                return OctetStreamMimeType;
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
            return OctetStreamMimeType;
        }

        /// <summary>
        /// Gets fixed type metadata from the cmdlet parameter if user provides that.
        /// If not, tries retrieving it from the metadata dictionary using keyNameInMetadataDictionary.
        /// If that also fails, returns the default content type.
        /// </summary>
        /// <returns></returns>
        protected string GetFixedTypeMetadata(
            string parameterName,
            IReadOnlyDictionary<string, string> metadataDictionary,
            string keyNameInMetadataDictionary,
            string defaultValue = null)
        {
            if (MyInvocation.BoundParameters.ContainsKey(parameterName))
            {
                return MyInvocation.BoundParameters[parameterName] as string;
            }

            if (metadataDictionary != null && metadataDictionary.ContainsKey(keyNameInMetadataDictionary))
            {
                return metadataDictionary[keyNameInMetadataDictionary];
            }

            return defaultValue;
        }
    }
}
