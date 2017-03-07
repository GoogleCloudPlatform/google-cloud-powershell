// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.PowerShell.Common;
using Google.Apis.Container.v1;

namespace Google.PowerShell.Container
{
    public class GkeCmdlet : GCloudCmdlet
    {
        // Service for Google Container API.
        public ContainerService Service { get; private set; }

        public GkeCmdlet()
        {
            Service = new ContainerService(GetBaseClientServiceInitializer());
        }
    }
}
