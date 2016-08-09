// Copyright 2015 Google Inc. All Rights Reserved.
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
        protected const string UTF8TextMimeType = "text/plain; charset=utf-8";

        // TODO(chrsmith): Cache the storage service? Create it in OnProcessRecord every time? (So it does so once?)

        protected StorageService GetStorageService()
        {
            return new StorageService(GetBaseClientServiceInitializer());
        }

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
        /// Infer the MIME type of a non-qualified file path. Returns null if no match is found.
        /// </summary>
        protected string InferContentType(string file)
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

        /// <summary>
        /// Return the content type to use for a Cloud Storage object given existing values, defauts, etc. The order of
        /// precidence is:
        /// 1. New content type, e.g. a ContentType parameter.
        /// 2. New metadata, e.g. Metadata value specified via parameter.
        /// 3. Existing object, to keep its existing content-type
        /// 4. Default content type #1 to apply. (e.g. sniffing file content.)
        /// 5. Default content type #2 to apply. (e.g. a catch-all like octet-stream.)
        /// </summary>
        public string GetContentType(
            string newContentType,
            Dictionary<string, string> newMetadata,
            Google.Apis.Storage.v1.Data.Object existingObject,
            string defaultContentType1 = null,
            string defaultContentType2 = null)
        {
            if (!String.IsNullOrEmpty(newContentType))
            {
                return newContentType;
            }

            if (newMetadata != null && newMetadata.ContainsKey("Content-Type"))
            {
                return newMetadata["Content-Type"];
            }

            if (existingObject != null && !String.IsNullOrEmpty(existingObject.ContentType)) {
                return existingObject.ContentType;
            }

            if (!String.IsNullOrEmpty(defaultContentType1))
            {
                return defaultContentType1;
            }

            return defaultContentType2;
        }
    }
}
