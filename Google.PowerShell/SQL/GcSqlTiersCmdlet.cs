// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.SQLAdmin.v1beta4.Data;
using Google.Apis.SQLAdmin.v1beta4;
using System.Management.Automation;

namespace Google.PowerShell.SQL
{

    /// <summary>
    /// <para type="synopsis">
    /// Lists all available service tiers for Google Cloud SQL, for example D1, D2. 
    /// </para>
    /// <para type="description">
    /// Lists all available service tiers for Google Cloud SQL, for example D1, D2. Pricing information is available online.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlTiers")]
    public class GcSqlTiersCmdlet : GcSqlCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Project ID of the project for which to list tiers.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Project { get; set; }

        protected override void ProcessRecord()
        {
            TiersResource resource = new TiersResource(Service);
            TiersResource.ListRequest request = resource.List(Project);
            TiersListResponse tiers = request.Execute();
            WriteObject(tiers.Items);
        }
    }
}
