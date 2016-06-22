using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.PowerShell.Common;
using Google.Apis.SQLAdmin.v1beta4;

namespace Google.PowerShell.SQL
{
    /// <summary>
    /// Base class for Google Cloud SQL-based cmdlets. 
    /// </summary>
    public abstract class GcSqlCmdlet : GCloudCmdlet
    {

        //The service for the Google Cloud SQL API
        public SQLAdminService Service { get; private set; }

        public GcSqlCmdlet()
        {
            Service = new SQLAdminService(GetBaseClientServiceInitializer());
        }
    }
}
