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
    ///   <para><code>PS C:\> Get-GcdManagedZone -DnsProject "testing" </code></para>
    ///   <br></br>
    ///   <para>CreationTime     : 2016-06-29T15:30:50.667Z</para>
    ///   <para>Description   	 : testing description</para>
    ///   <para>DnsName          : gcloudexample1.com.</para>
    ///   <para>Id            	 : 4735311843662425164</para>
    ///   <para>Kind          	 : dns#managedZone</para>
    ///   <para>Name             : test1</para>
    ///   <para>NameServerSet    :</para>
    ///   <para>
    ///   NameServers      : {ns-cloud-e1.googledomains.com., ns-cloud-e2.googledomains.com., 
    ///   ns-cloud-e3.googledomains.com., ns-cloud-e4.googledomains.com.}
    ///   </para>
    ///   <para>ETag          	 :</para>
    ///   <br></br>
    ///   <para>CreationTime     : 2016-06-29T15:30:50.667Z</para>
    ///   <para>Description   	 :</para>
    ///   <para>DnsName          : gcloudexample2.com.</para>
    ///   <para>Id            	 : 4484350849440060468</para>
    ///   <para>Kind          	 : dns#managedZone</para>
    ///   <para>Name             : testZone2</para>
    ///   <para>NameServerSet    :</para>
    ///   <para>
    ///   NameServers      : {ns-cloud-e1.googledomains.com., ns-cloud-e2.googledomains.com., 
    ///   ns-cloud-e3.googledomains.com., ns-cloud-e4.googledomains.com.}
    ///   </para>
    ///   <para>ETag          	 :</para>
    /// </example>
    /// <example>
    ///   <para>Get the ManagedZone "test1" for the DnsProject "testing."</para>
    ///   <para><code>PS C:\> Get-GcdManagedZone -DnsProject "testing" -Zone "test1" </code></para>
    ///   <br></br>
    ///   <para>CreationTime     : 2016-06-29T15:30:50.667Z</para>
    ///   <para>Description   	 : testing description</para>
    ///   <para>DnsName          : gcloudexample1.com.</para>
    ///   <para>Id            	 : 4735311843662425164</para>
    ///   <para>Kind          	 : dns#managedZone</para>
    ///   <para>Name             : test1</para>
    ///   <para>NameServerSet    :</para>
    ///   <para>
    ///   NameServers      : {ns-cloud-e1.googledomains.com., ns-cloud-e2.googledomains.com., 
    ///   ns-cloud-e3.googledomains.com., ns-cloud-e4.googledomains.com.}
    ///   </para>
    ///   <para>ETag          	 :</para>
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

    /// <summary>
    /// <para type="synopsis">
    /// Add a new Google DNS ManagedZone to the DnsProject.
    /// </para>
    /// <para type="description">
    /// Creates a new ManagedZone.
    /// </para>
    /// <para type="description">
    /// If a DnsProject is specified, it will instead add the new ManagedZone to that project.
    /// </para>
    /// <example>
    ///   <para>
    ///   Create a new ManagedZone in the DNSProject "testing" with the name "test1," DNS name "gcloudexample.com.,"
    ///   and description "test description."
    ///   </para>
    ///   <para>
    ///     <code>
    ///     PS C:\> Add-GcdManagedZone -DnsProject "testing" -Name "testzone1" -DnsName "gcloudexample.com." 
    ///     -Description "test description"
    ///     </code>
    ///   </para>
    ///   <br></br>
    ///   <para>CreationTime     : 2016-06-29T15:30:50.667Z</para>
    ///   <para>Description   	 : testing description</para>
    ///   <para>DnsName          : gcloudexample.com.</para>
    ///   <para>Id            	 : 4735311843662425164</para>
    ///   <para>Kind          	 : dns#managedZone</para>
    ///   <para>Name             : test1</para>
    ///   <para>NameServerSet    :</para>
    ///   <para>
    ///   NameServers      : {ns-cloud-e1.googledomains.com., ns-cloud-e2.googledomains.com., 
    ///   ns-cloud-e3.googledomains.com., ns-cloud-e4.googledomains.com.}
    ///   </para>
    ///   <para>ETag          	 :</para>
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
        /// Get the DnsProject to create a new ManagedZone in.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string DnsProject { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the new ManagedZone to create.
        /// </para>
        /// <para type="description">
        /// The name must be 1-32 characters long, begin with a letter, end with a letter or digit, and only contain 
        /// lowercase letters, digits, and dashes.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
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
        [Parameter(Position = 2, Mandatory = true)]
        public string DnsName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the description of the new ManagedZone.
        /// </para>
        /// </summary>
        [Parameter(Position = 3, Mandatory = false)]
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
                Service.ManagedZones.Create(zoneContent, DnsProject);
            ManagedZone newZone = zoneCreateRequest.Execute();
            WriteObject(newZone);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes an existing Google DNS ManagedZone within a DnsProject.
    /// </para>
    /// <para type="description">
    /// Deletes the specified ManagedZone (and returns nothing).
    /// </para>
    /// <para type="description">
    /// If a DnsProject is specified, it will instead remove the specified ManagedZone from that project. The optional 
    /// switch -Force will force removal of even non-empty ManagedZones (e.g., zones with non-NS/SOA type records).
    /// </para>
    /// <example>
    ///   <para>Delete the (non-empty) ManagedZone "test1" from the DnsProject "testing."</para>
    ///   <para><code>PS C:\> Remove-GcdManagedZone -DnsProject "testing" -Zone "test1" -Force</code></para>
    ///   <br></br>
    ///   <para>(If successful, the command returns nothing.)</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/dns/zones/)">[Managing Zones]</para>
    /// <para type="link" uri="(https://cloud.google.com/dns/troubleshooting)">[Troubleshooting]</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcdManagedZone", SupportsShouldProcess = true)]
    public class RemoveGcdManagedZoneCmdlet : GcdCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Get the DnsProject to check.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string DnsProject { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the specific ManagedZone to delete (name or id permitted).
        /// </para>
        /// </summary>
        [Alias("Name", "Id", "ManagedZone")]
        [Parameter(Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Force removal of even non-empty ManagedZones (e.g., zones with non-NS/SOA type records).
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = false)]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            if (!ShouldProcess($"{DnsProject}/{Zone}", "Delete ManagedZone"))
            {
                return;
            }

            ResourceRecordSetsResource.ListRequest rrsetListRequest =
                    Service.ResourceRecordSets.List(DnsProject, Zone);
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
                    !ShouldContinue($"{DnsProject}/{Zone}", "Delete Non-Empty ManagedZone (with non-NS/SOA records)"))
                {
                    return;
                }

                Change changeContent = new Change
                {
                    Deletions = nonDefaultRrsets
                };

                ChangesResource.CreateRequest changeCreateRequest =
                    Service.Changes.Create(changeContent, DnsProject, Zone);
                changeCreateRequest.Execute();
            }

            ManagedZonesResource.DeleteRequest zoneDeleteRequest = Service.ManagedZones.Delete(DnsProject, Zone);
            zoneDeleteRequest.Execute();
        }
    }
}
