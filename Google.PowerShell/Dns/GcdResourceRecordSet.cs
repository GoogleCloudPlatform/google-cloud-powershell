// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

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
    ///   <para>Get the ResourceRecordSet resources in the ManagedZone "test1" in the Project "testing."</para>
    ///   <para><code>PS C:\> Get-GcdResourceRecordSet -Project "testing" -Zone "test1"</code></para>
    ///   <br></br>
    ///   <para>Kind    : dns#resourceRecordSet</para>
    ///   <para>Name    : gcloudexample1.com.</para>
    ///   <para>
    ///   Rrdatas : {ns-cloud-e1.googledomains.com., ns-cloud-e2.googledomains.com., 
    ///   ns-cloud-e3.googledomains.com., ns-cloud-e4.googledomains.com.}
    ///   </para>
    ///   <para>Ttl     : 21600</para>
    ///   <para>Type    : NS</para>
    ///   <para>ETag    :</para>
    ///   <br></br>
    ///   <para>Kind    : dns#resourceRecordSet</para>
    ///   <para>Name    : gcloudexample1.com.</para>
    ///   <para>
    ///   Rrdatas : {ns-cloud-e1.googledomains.com.cloud-dns-hostmaster.google.com. 1 21600 3600 259200 300}
    ///   </para>
    ///   <para>Ttl     : 21600</para>
    ///   <para>Type    : SOA</para>
    ///   <para>ETag    :</para>
    /// </example>
    /// <example>
    ///   <para>
    ///   Get the ResourceRecordSets of type "NS" or "AAAA" in the ManagedZone "testZone2" in the Project "testing."
    ///   </para>
    ///   <para>
    ///     <code>PS C:\> Get-GcdResourceRecordSet -Project "testing" -Zone "testZone2" -Filter "NS","AAAA"</code>
    ///   </para>
    ///   <br></br>
    ///   <para>Kind    : dns#resourceRecordSet</para>
    ///   <para>Name    : gcloudexample1.com.</para>
    ///   <para>
    ///   Rrdatas : {ns-cloud-e1.googledomains.com., ns-cloud-e2.googledomains.com., 
    ///   ns-cloud-e3.googledomains.com., ns-cloud-e4.googledomains.com.}
    ///   </para>
    ///   <para>Ttl     : 21600</para>
    ///   <para>Type    : NS</para>
    ///   <para>ETag    :</para> 
    ///   <br></br>
    ///   <para>Kind    : dns#resourceRecordSet</para>
    ///   <para>Name    : gcloudexample1.com.</para>
    ///   <para>Rrdatas : {2001:db8:85a3::8a2e:370:7334}</para>
    ///   <para>Ttl     : 300</para>
    ///   <para>Type    : AAAA</para>
    ///   <para>ETag    :</para>
    /// </example>
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
        public string Project { get; set; }

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
        public string[] Filter { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            ResourceRecordSetsResource.ListRequest rrsetListRequest =
                Service.ResourceRecordSets.List(Project, Zone);
            ResourceRecordSetsListResponse rrsetListResponse = rrsetListRequest.Execute();
            IList<ResourceRecordSet> rrsetList = rrsetListResponse.Rrsets;

            if (!(Filter == null || Filter.Length == 0))
            {
                HashSet<string> TypeFilterHash = new HashSet<string>(Filter);
                HashSet<ResourceRecordSet> rrsetHash = new HashSet<ResourceRecordSet>(rrsetList);

                foreach (ResourceRecordSet rrset in rrsetList)
                {
                    if (!TypeFilterHash.Contains(rrset.Type))
                    {
                        rrsetHash.Remove(rrset);
                    }
                }

                WriteObject(rrsetHash, true);
            }
            else
            {
                WriteObject(rrsetList, true);
            }
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
    ///   <para>
    ///   Create a new ResourceRecordSet resource with name "gcloudexample.com.", Rrdata ["7.5.7.8"], type "A," and 
    ///   ttl 300.
    ///   </para>
    ///   <para>
    ///   <code>PS C:\> New-GcdResourceRecordSet -Name "gcloudexample.com." -Rrdata "7.5.7.8" -Type "A" -Ttl 300</code>
    ///   </para>
    ///   <br></br>
    ///   <para>Kind    : dns#resourceRecordSet</para>
    ///   <para>Name    : gcloudexample1.com.</para>
    ///   <para>Rrdatas : {7.5.7.8}</para>
    ///   <para>Ttl     : 300</para>
    ///   <para>Type    : A</para>
    ///   <para>ETag    :</para>
    /// </example>
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
