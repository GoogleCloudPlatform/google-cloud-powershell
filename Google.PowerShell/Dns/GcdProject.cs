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
    /// Fetch the representation of an existing DnsProject.
    /// </para>
    /// <para type="description">
    /// Returns the DnsProject resource object.
    /// </para>
    /// <para type="description">
    /// If a DnsProject is specified, will instead return the representation of that project. 
    /// </para>
    /// <example>
    ///   <para>Get the representation of the DnsProject "testing"</para>
    ///   <para><code>Get-GcdProject -DnsProject "testing" </code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcdProject")]
    public class GetGcdProjectCmdlet : GcdCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Get the DnsProject to return the representation of.
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
            WriteObject(projectResponse);
        }
    }
}
