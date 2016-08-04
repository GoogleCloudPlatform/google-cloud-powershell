// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Dns.v1;
using Google.Apis.Dns.v1.Data;
using Google.PowerShell.Common;
using System.Management.Automation;

namespace Google.PowerShell.Dns
{
    /// <summary>
    /// <para type="synopsis">
    /// Fetch the DNS quota of an existing Project.
    /// </para>
    /// <para type="description">
    /// Returns the DNS quota from the Project resource object.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, will instead return the DNS quota for that project. 
    /// </para>
    /// <example>
    ///   <para>Get the DNS quota of the Project "testing"</para>
    ///   <para><code>PS C:\> Get-GcdQuota -Project "testing" </code></para>
    ///   <br></br>
    ///   <para>Kind                     : dns#quota</para>
    ///   <para>ManagedZones             : 100</para>
    ///   <para>ResourceRecordsPerRrset  : 100</para>
    ///   <para>RrsetAdditionsPerChange  : 100</para>
    ///   <para>RrsetDeletionsPerChange  : 100</para>
    ///   <para>RrsetsPerManagedZone     : 10000</para>
    ///   <para>TotalRrdataSizePerChange : 10000</para>
    ///   <para>ETag                     :</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/dns/quota)">[Quotas]</para>
    /// <para type="link" uri="(https://cloud.google.com/dns/troubleshooting)">[Troubleshooting]</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcdQuota")]
    [OutputType(typeof(Quota))]
    public class GetGcdQuotaCmdlet : GcdCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Get the Project to return the DNS quota of.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            ProjectsResource.GetRequest projectGetRequest = Service.Projects.Get(Project);
            Project projectResponse = projectGetRequest.Execute();
            WriteObject(projectResponse.Quota);
        }
    }
}
