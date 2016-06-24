// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Dns.v1;
using Google.Apis.Dns.v1.Data;
using Google.Apis.Download;
using Google.Apis.Services;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using System.Threading;

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
