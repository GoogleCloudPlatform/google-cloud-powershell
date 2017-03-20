// Copyright 2015-2016 Google Inc. All Rights Reserved.
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
    /// Lists the ManagedZone's ResourceRecordSets.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, the cmdlet will instead return the ResourceRecordSets in the specified 
    /// ManagedZone governed by that project. The optional -Filter can be provided to restrict the ResourceRecordSet 
    /// types returned.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcdResourceRecordSet -Project "testing" -Zone "test1"</code>
    ///   <para>Get the ResourceRecordSet resources in the ManagedZone "test1" in the Project "testing."</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcdResourceRecordSet -Project "testing" -Zone "testZone2" -Filter "NS","AAAA"</code>
    ///   <para>
    ///   Get the ResourceRecordSets of type "NS" or "AAAA" in the ManagedZone "testZone2" in the Project "testing."
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/dns/records/json-record)">
    /// [Supported Resource Record Formats]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/dns/records/)">[Managing Records]</para>
    /// <para type="link" uri="(https://cloud.google.com/dns/troubleshooting)">[Troubleshooting]</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcdResourceRecordSet")]
    [OutputType(typeof(ResourceRecordSet))]
    public class GetGcdResourceRecordSetCmdlet : GcdCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Get the Project to check.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the ManagedZone (name or id permitted) to check for ResourceRecordSets.
        /// </para>
        /// </summary>
        [Alias("ManagedZone")]
        [Parameter(Position = 0, Mandatory = true)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Filter the type(s) of ResourceRecordSets to return (e.g., -Filter "CNAME","NS")
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = false)]
        [ValidateSet("A", "AAAA", "CNAME", "MX", "NAPTR", "NS", "PTR", "SOA", "SPF", "SRV", "TXT")]
        public string[] Filter { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            WriteObject(GetResourceRecordSet(Project, Zone, Filter), true);
        }

        /// <summary>
        /// Returns all resource record sets in zone 'zone' in project 'project'.
        /// Apply filters if neccessary.
        /// </summary>
        private IEnumerable<ResourceRecordSet> GetResourceRecordSet(string project, string zone, string[] filters)
        {
            ResourceRecordSetsResource.ListRequest rrsetListRequest =
                Service.ResourceRecordSets.List(project, zone);
            do
            {
                ResourceRecordSetsListResponse rrsetListResponse = rrsetListRequest.Execute();
                IList<ResourceRecordSet> rrsetList = rrsetListResponse.Rrsets;
                if (rrsetList != null)
                {
                    if (filters != null && filters.Length != 0)
                    {
                        HashSet<string> TypeFilterHash = new HashSet<string>(filters);
                        HashSet<ResourceRecordSet> rrsetHash = new HashSet<ResourceRecordSet>(rrsetList);

                        foreach (ResourceRecordSet rrset in rrsetList)
                        {
                            if (!TypeFilterHash.Contains(rrset.Type))
                            {
                                rrsetHash.Remove(rrset);
                            }
                        }
                        foreach (ResourceRecordSet record in rrsetHash)
                        {
                            yield return record;
                        }
                    }
                    else
                    {
                        foreach (ResourceRecordSet record in rrsetList)
                        {
                            yield return record;
                        }
                    }
                }

                rrsetListRequest.PageToken = rrsetListResponse.NextPageToken;
            }
            while (rrsetListRequest.PageToken != null);
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
    /// The newly created ResourceRecordSet will be created and returned independently, not within any Project or 
    /// ManagedZone. 
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GcdResourceRecordSet -Name "gcloudexample.com." -Rrdata "7.5.7.8" -Type "A" -Ttl 300</code>
    ///   <para>
    ///   Create a new ResourceRecordSet resource with name "gcloudexample.com.", Rrdata ["7.5.7.8"], type "A," and 
    ///   ttl 300.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/dns/records/json-record)">
    /// [Supported Resource Record Formats]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/dns/records/)">[Managing Records]</para>
    /// <para type="link" uri="(https://cloud.google.com/dns/troubleshooting)">[Troubleshooting]</para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcdResourceRecordSet")]
    [OutputType(typeof(ResourceRecordSet))]
    public class NewGcdResourceRecordSetCmdlet : GcdCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Get the name of the new ResourceRecordSet (e.g., "gcloudexample.com.").
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the resource record data for the ResourceRecordSet.
        /// </para>
        /// </summary>
        [Alias("Data")]
        [Parameter(Position = 1, Mandatory = true)]
        public string[] Rrdata { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the type of the ResourceRecordSet.
        /// </para>
        /// <para type="description">
        /// The supported types are A, AAAA, CNAME, MX, NAPTR, NS, PTR, SOA, SPF, SRV, and TXT.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true)]
        [ValidateSet("A", "AAAA", "CNAME", "MX", "NAPTR", "NS", "PTR", "SOA", "SPF", "SRV", "TXT")]
        public string Type { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the ttl, which is the number of seconds the ResourceRecordSet can be cached by resolvers.
        /// </para>
        /// </summary>
        [Parameter(Position = 3, Mandatory = false)]
        public int Ttl = 3600;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            ResourceRecordSet rrset = new ResourceRecordSet
            {
                Kind = "dns#resourceRecordSet",
                Name = Name,
                Rrdatas = Rrdata,
                Ttl = Ttl,
                Type = Type
            };

            WriteObject(rrset);
        }
    }
}
