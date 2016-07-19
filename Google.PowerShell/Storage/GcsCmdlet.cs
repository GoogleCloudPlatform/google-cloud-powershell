// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Storage.v1;
using Google.PowerShell.Common;
using System;
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
    }
}
