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
    /// Fetch the DNS quota of an existing DnsProject.
    /// </para>
    /// <para type="description">
    /// Returns the DNS quota from the DnsProject resource object.
    /// </para>
    /// <para type="description">
    /// If a DnsProject is specified, will instead return the DNS quota for that project. 
    /// </para>
    /// <example>
    ///   <para>Get the DNS quota of the DnsProject "testing"</para>
    ///   <para><code>Get-GcdQuota -DnsProject "testing" </code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcdQuota")]
    public class GetGcdQuotaCmdlet : GcdCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Get the DnsProject to return the DNS quota of.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string DnsProject { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            ProjectsResource.GetRequest projectGetRequest = Service.Projects.Get(DnsProject);
            Project projectResponse = projectGetRequest.Execute();
            WriteObject(projectResponse.Quota);
        }
    }
}
