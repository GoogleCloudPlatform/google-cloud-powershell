// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Dns.v1;
using Google.PowerShell.Common;

namespace Google.PowerShell.Dns
{
    /// <summary>
    /// Base class for Google DNS-based cmdlets. 
    /// </summary>
    public abstract class GcdCmdlet : GCloudCmdlet
    {
        // The Service for the Google DNS API
        public DnsService Service { get; }

        protected GcdCmdlet() : this(null)
        {
        }

        protected GcdCmdlet(DnsService service)
        {
            Service = service ?? new DnsService(GetBaseClientServiceInitializer());
        }
    }
}
