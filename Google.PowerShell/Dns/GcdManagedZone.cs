// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Dns.v1;
using Google.Apis.Dns.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.Dns
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets the Google DNS ManagedZones within a DnsProject.
    /// </para>
    /// <para type="description">
    /// Lists the DnsProject's ManagedZones.
    /// </para>
    /// <para type="description">
    /// If a DnsProject is specified, will instead return all ManagedZones governed by that project. 
    /// The filter ManagedZone can be provided to return that specific zone.
    /// </para>
    /// <example>
    ///   <para>Get the ManagedZones for the DnsProject "testing."</para>
    ///   <para><code>Get-GcdManagedZones -DnsProject "testing" </code></para>
    /// </example>
    /// <example>
    ///   <para>Get the ManagedZone "test1" for the DnsProject "testing."</para>
    ///   <para><code>Get-GcdManagedZones -DnsProject "testing" -Zone "test1" </code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcdManagedZone")]
    public class GetGcdManagedZoneCmdlet : GcdCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Get the project to check for ManagedZones.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string DnsProject { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the specific ManagedZone to return (name or id permitted).
        /// </para>
        /// </summary>
        [Alias("ManagedZone")]
        [Parameter(Position = 1, Mandatory = false)]
        public string Zone { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            if (!String.IsNullOrEmpty(Zone))
            {
                ManagedZonesResource.GetRequest zoneGetRequest = Service.ManagedZones.Get(DnsProject, Zone);
                ManagedZone zoneResponse = zoneGetRequest.Execute();
                WriteObject(zoneResponse);
            }
            else
            {
                ManagedZonesResource.ListRequest zoneListRequest = Service.ManagedZones.List(DnsProject);
                ManagedZonesListResponse zoneListResponse = zoneListRequest.Execute();
                IList<ManagedZone> zoneList = zoneListResponse.ManagedZones;
                WriteObject(zoneList, true);
            }
        }
    }
}
