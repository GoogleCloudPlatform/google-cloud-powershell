// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Bigquery.v2;
using Google.PowerShell.Common;

namespace Google.PowerShell.BigQuery
{
    /// <summary>
    /// Base class for Google Cloud BigQuery cmdlets.
    /// </summary>
    public class BqCmdlet : GCloudCmdlet
    {
        public BigqueryService Service { get; private set; }

        public BqCmdlet()
        {
            Service = new BigqueryService(GetBaseClientServiceInitializer());
        }
    }
}
