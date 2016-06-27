// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Dns.v1;
using Google.Apis.Dns.v1.Data;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.Dns
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets the ResourceRecordSet resources within a ManagedZone of a Project.
    /// </para>
    /// <para type="description">
    /// Lists the ManagedZone's ResourceRecordSet resources.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, will instead return the ResourceRecordSets in the specified ManagedZone governed by that project. 
    /// </para>
    /// <example>
    ///   <para>Get the ResourceRecordSet resources in the ManagedZone "test1" in the Project "testing."</para>
    ///   <para><code>Get-GcdResourceRecordSet -Project "testing" -ManagedZone "test1"</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcdResourceRecordSet")]
    public class GetGcdResourceRecordSetCmdlet : GcdCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Get the project to check.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the ManagedZone (name or id permitted) to check for Resource Record Sets.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string ManagedZone { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            ResourceRecordSetsResource.ListRequest rrsetListRequest = Service.ResourceRecordSets.List(Project, ManagedZone);
            ResourceRecordSetsListResponse rrsetListResponse = rrsetListRequest.Execute();
            IList<ResourceRecordSet> rrsetList = rrsetListResponse.Rrsets;
            WriteObject(rrsetList, true);
        }
    }
}
