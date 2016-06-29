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
using Google.PowerShell.Common;

namespace Google.PowerShell.Sql
{
    /// <summary>
    /// <para type="synopsis">
    /// Lists all available service tiers for Google Cloud SQL, for example D1, D2. 
    /// </para>
    /// <para type="description">
    /// Lists all available service tiers for Google Cloud SQL, for example D1, D2. 
    /// Pricing information is available at https://cloud.google.com/sql/pricing.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlTiers")]
    public class GcSqlTiersCmdlet : GcSqlCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Project name of the project for which to list tiers.
        /// </para>
        /// </summary>
        [Parameter(Position = 0)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        protected override void ProcessRecord()
        {
            TiersResource.ListRequest request = Service.Tiers.List(Project);
            TiersListResponse tiers = request.Execute();
            WriteObject(tiers.Items, true);
        }
    }
}
