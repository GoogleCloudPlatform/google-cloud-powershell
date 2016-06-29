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
        /// Get the ManagedZone (name or id permitted) to check for ResourceRecordSets.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
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

    /// <summary>
    /// <para type="synopsis">
    /// Create an independent new ResourceRecordSet resource.
    /// </para>
    /// <para type="description">
    /// Creates and returns a new ResourceRecordSet resource.
    /// </para>
    /// <para type="description">
    /// The newly created ResourceRecordSet will be created and returned independently, not within any Project or ManagedZone. 
    /// </para>
    /// <example>
    ///   <para>Create a new ResourceRecordSet resource with name "rrset1example.com.", Rrdata [“7.5.7.8”], Ttl 300, and type "A".</para>
    ///   <para><code>New-GcdResourceRecordSet -Name "rrset1example.com." -Rrdata "7.6.7.8" -Ttl 300 -Type "A"</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcdResourceRecordSet")]
    public class NewGcdResourceRecordSetCmdlet : GcdCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Get the name of the new ResourceRecordSet (e.g., "example.com.").
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the resource record data for the ResourceRecordSet.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public string[] Rrdatas { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the ttl, which is the number of seconds the ResourceRecordSet can be cached by resolvers.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true)]
        public int Ttl { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the type of the ResourceRecordSet.
        /// </para>
        /// </summary>
        [Parameter(Position = 3, Mandatory = true)]
        [ValidateSet("A","AAAA","CNAME","MX","NAPTR","NS","PTR","SOA","SPF","SRV","TXT")]
        public string Type { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            ResourceRecordSet rrset = new ResourceRecordSet
            {
                Kind = "dns#resourceRecordSet",
                Name = Name,
                Rrdatas = (IList<string>) Rrdatas,
                Ttl = Ttl,
                Type = Type
            };

            WriteObject(rrset);
        }
    }
}
