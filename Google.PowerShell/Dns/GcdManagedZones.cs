// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Services; 
using Google.Apis.Dns.v1;
using Google.Apis.Dns.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Management.Automation;

namespace Google.PowerShell.Dns
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets the Google DNS ManagedZones that have been created but not yet deleted within a project.
    /// </para>
    /// <para type="description">
    /// Lists the project's ManagedZones.
    /// </para>
    /// <para type="description">
    /// If a project is specified, will instead return all ManagedZones governed by that project. 
    /// </para>
    /// <example>
    ///   <para>Get the managed zones for the project "testing."</para>
    ///   <para><code>Get-GcdManagedZones -Project "testing" </code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcdManagedZones")]
    public class GetGcdManagedZonesCmdlet : GCloudCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for managed zones.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            if (!String.IsNullOrEmpty(Project))
            {
                DnsService dnsService = new DnsService(GetBaseClientServiceInitializer());
                ManagedZonesResource zoneResource = new ManagedZonesResource(dnsService);
                ManagedZonesResource.ListRequest zonesListReq = zoneResource.List(Project);
                var zonesList = zonesListReq.Execute().ManagedZones;
                WriteObject(zonesList);
            }
        }
    }
}
