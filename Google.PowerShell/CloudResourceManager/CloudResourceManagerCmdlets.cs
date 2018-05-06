// Copyright 2015-2018 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.CloudResourceManager.v1;
using Google.PowerShell.Common;
using System;

namespace Google.PowerShell.CloudResourceManager
{
    /// <summary>
    /// Base class for Cloud Resource Manager cmdlet.
    /// </summary>
    public class CloudResourceManagerCmdlet : GCloudCmdlet
    {
        private Lazy<CloudResourceManagerService> _cloudResourceManagerServiceLazy;

        public CloudResourceManagerService Service => ServiceOverride ?? _cloudResourceManagerServiceLazy.Value;
        internal static CloudResourceManagerService ServiceOverride { private get; set; }

        public CloudResourceManagerCmdlet()
        {
            _cloudResourceManagerServiceLazy = new Lazy<CloudResourceManagerService>(() => new CloudResourceManagerService(GetBaseClientServiceInitializer()));
        }
    }
}
