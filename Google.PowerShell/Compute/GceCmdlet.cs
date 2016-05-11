// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Threading;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// Base class for Google Compute Engine-based cmdlets. 
    /// </summary>
    public abstract class GceCmdlet : GCloudCmdlet
    {
        protected ComputeService GetComputeService()
        {
            return new ComputeService(GetBaseClientServiceInitializer());
        }

        /// <summary>
        /// Waits for the provided RegionOperation to complete. This way cmdlets can return newly
        /// created objects once they are finished being created, rather than returning thunks.
        /// 
        /// Will throw an exception if the operation fails for any reason.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="op"></param>
        protected void WaitForRegionOperation(ComputeService service, string project, Operation op)
        {
            while (op.Status != "DONE")
            {
                Thread.Sleep(150);
                RegionOperationsResource.GetRequest getReq = service.RegionOperations.Get(project, op.Region, op.Name);
                op = getReq.Execute();
            }
        }
    }
}
