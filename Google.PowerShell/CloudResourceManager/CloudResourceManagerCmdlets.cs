// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.CloudResourceManager.v1;
using Google.PowerShell.Common;

namespace Google.PowerShell.CloudResourceManager
{
    /// <summary>
    /// Base class for Cloud Resource Manager cmdlet.
    /// </summary>
    public class CloudResourceManagerCmdlet : GCloudCmdlet
    {
        public CloudResourceManagerService Service { get; private set; }

        public CloudResourceManagerCmdlet()
        {
            Service = new CloudResourceManagerService(GetBaseClientServiceInitializer());
        }
    }
}
