using Google.Apis.Compute.v1;
using Google.PowerShell.Common;

namespace Google.PowerShell.Compute
{
    /// <summary>
    /// Base class for Google Compute Engine-based cmdlets. 
    /// </summary>
    public abstract class GceCmdlet : GCloudCmdlet
    {

        // The Servcie for the Google Compute API
        public ComputeService Service { get; private set; }

        public GceCmdlet() : this(null)
        {
        }
        
        public GceCmdlet(ComputeService service)
        {
            if (service == null)
            {
                Service = new ComputeService(GetBaseClientServiceInitializer());
            }
            else
            {
                Service = service;
            }
        }

        /// <summary>
        /// Waits for the provided RegionOperation to complete. This way cmdlets can return newly
        /// created objects once they are finished being created, rather than returning thunks.
        /// 
        /// Will throw an exception if the operation fails for any reason.
        /// </summary>
        protected void WaitForZoneOperation(ComputeService service, string project, string zone, Operation op)
        {
            while (op.Status != "DONE")
            {
                Thread.Sleep(150);
                ZoneOperationsResource.GetRequest getReq = service.ZoneOperations.Get(project, zone, op.Name);
                op = getReq.Execute();
            }

            if (op.Error != null)
            {
                throw new GoogleApiException("Compute", "Error waiting for zone operation: " + op.Error.ToString());
            }
        }
    }
}