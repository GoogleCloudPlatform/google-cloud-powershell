// Copyright 2015-2016 Google Inc. All Rights Reserved.
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
    /// Gets the Google DNS ManagedZones within a Project.
    /// </para>
    /// <para type="description">
    /// Lists the Project's ManagedZones.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, will instead return all ManagedZones governed by that project. 
    /// The filter ManagedZone can be provided to return that specific zone.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcdManagedZone -Project "testing" </code>
    ///   <para>Get the ManagedZones for the Project "testing."</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcdManagedZone -Project "testing" -Zone "test1" </code>
    ///   <para>Get the ManagedZone "test1" for the Project "testing."</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/dns/zones/)">[Managing Zones]</para>
    /// <para type="link" uri="(https://cloud.google.com/dns/troubleshooting)">[Troubleshooting]</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcdManagedZone")]
    [OutputType(typeof(ManagedZone))]
    public class GetGcdManagedZoneCmdlet : GcdCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Get the project to check for ManagedZones.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the specific ManagedZone to return (name or id permitted).
        /// </para>
        /// </summary>
        [Alias("ManagedZone")]
        [Parameter(Position = 0, Mandatory = false)]
        public string Zone { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            if (!String.IsNullOrEmpty(Zone))
            {
                ManagedZonesResource.GetRequest zoneGetRequest = Service.ManagedZones.Get(Project, Zone);
                ManagedZone zoneResponse = zoneGetRequest.Execute();
                WriteObject(zoneResponse);
            }
            else
            {
                ManagedZonesResource.ListRequest zoneListRequest = Service.ManagedZones.List(Project);
                ManagedZonesListResponse zoneListResponse = zoneListRequest.Execute();
                IList<ManagedZone> zoneList = zoneListResponse.ManagedZones;
                WriteObject(zoneList, true);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Add a new Google DNS ManagedZone to the Project.
    /// </para>
    /// <para type="description">
    /// Creates a new ManagedZone.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, it will instead add the new ManagedZone to that project.
    /// </para>
    /// <example>   
    ///   <code>
    ///     PS C:\> Add-GcdManagedZone -Project "testing" -Name "testzone1" `
    ///         -DnsName "gcloudexample.com." -Description "test description"     
    ///   </code>
    ///   <para>
    ///   Create a new ManagedZone in the DNSProject "testing" with the name "test1," DNS name "gcloudexample.com.,"
    ///   and description "test description."
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/dns/zones/)">[Managing Zones]</para>
    /// <para type="link" uri="(https://cloud.google.com/dns/troubleshooting)">[Troubleshooting]</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcdManagedZone")]
    [OutputType(typeof(ManagedZone))]
    public class AddGcdManagedZoneCmdlet : GcdCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Get the Project to create a new ManagedZone in.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the new ManagedZone to create.
        /// </para>
        /// <para type="description">
        /// The name must be 1-32 characters long, begin with a letter, end with a letter or digit, and only contain 
        /// lowercase letters, digits, and dashes.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The DNS name of the new ManagedZone.
        /// </para>
        /// <para type="description">
        /// The DnsName must be a valid absolute zone and end in a period. If it does not, the cmdlet will 
        /// automatically add a period before attempting zone creation.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public string DnsName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the description of the new ManagedZone.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = false)]
        public string Description { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            ManagedZone zoneContent = new ManagedZone();
            zoneContent.Name = Name;

            if (DnsName[DnsName.Length - 1] != '.')
            {
                zoneContent.DnsName = DnsName + '.';
            }
            else
            {
                zoneContent.DnsName = DnsName;
            }

            zoneContent.Description = Description ?? "";


            ManagedZonesResource.CreateRequest zoneCreateRequest =
                Service.ManagedZones.Create(zoneContent, Project);
            ManagedZone newZone = zoneCreateRequest.Execute();
            WriteObject(newZone);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes an existing Google DNS ManagedZone within a Project.
    /// </para>
    /// <para type="description">
    /// Deletes the specified ManagedZone (and returns nothing).
    /// </para>
    /// <para type="description">
    /// If a Project is specified, it will instead remove the specified ManagedZone from that project. The optional 
    /// switch -Force will force removal of even non-empty ManagedZones (e.g., zones with non-NS/SOA type records).
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcdManagedZone -Project "testing" -Zone "test1" -Force</code>
    ///   <para>Delete the (non-empty) ManagedZone "test1" from the Project "testing."</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/dns/zones/)">[Managing Zones]</para>
    /// <para type="link" uri="(https://cloud.google.com/dns/troubleshooting)">[Troubleshooting]</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcdManagedZone", SupportsShouldProcess = true)]
    public class RemoveGcdManagedZoneCmdlet : GcdCmdlet
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
        /// Get the specific ManagedZone to delete (name or id permitted).
        /// </para>
        /// </summary>
        [Alias("Name", "Id", "ManagedZone")]
        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Force removal of even non-empty ManagedZones (e.g., zones with non-NS/SOA type records).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            if (!ShouldProcess($"{Project}/{Zone}", "Delete ManagedZone"))
            {
                return;
            }

            ResourceRecordSetsResource.ListRequest rrsetListRequest =
                    Service.ResourceRecordSets.List(Project, Zone);
            ResourceRecordSetsListResponse rrsetListResponse = rrsetListRequest.Execute();
            IList<ResourceRecordSet> rrsetList = rrsetListResponse.Rrsets;

            IList<ResourceRecordSet> nonDefaultRrsets = new List<ResourceRecordSet>();
            foreach (ResourceRecordSet rrset in rrsetList)
            {
                if (!rrset.Type.Equals("NS") && !rrset.Type.Equals("SOA"))
                {
                    nonDefaultRrsets.Add(rrset);
                }
            }

            if (nonDefaultRrsets.Count > 0)
            {
                if (!Force &&
                    !ShouldContinue($"{Project}/{Zone}", "Delete Non-Empty ManagedZone (with non-NS/SOA records)"))
                {
                    return;
                }

                Change changeContent = new Change
                {
                    Deletions = nonDefaultRrsets
                };

                ChangesResource.CreateRequest changeCreateRequest =
                    Service.Changes.Create(changeContent, Project, Zone);
                changeCreateRequest.Execute();
            }

            ManagedZonesResource.DeleteRequest zoneDeleteRequest = Service.ManagedZones.Delete(Project, Zone);
            zoneDeleteRequest.Execute();
        }
    }
}
