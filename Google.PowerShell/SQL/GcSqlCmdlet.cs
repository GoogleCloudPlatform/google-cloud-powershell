// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

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
