using Google.Apis.Compute.v1;
using Google.PowerShell.Common;

namespace Google.PowerShell.Compute
{
    public class GceCmdlet : GCloudCmdlet
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
    }
}