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
    /// Gets the Change resources within a ManagedZone of a Project.
    /// </para>
    /// <para type="description">
    /// Lists the ManagedZone's Change resources.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, will instead return the Changes in the specified ManagedZone governed by that 
    /// project. The filter ChangeId can be provided to return that specific Change.
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> Get-GcdChange -Project "testing" -Zone "test1"
    ///   </code>
    ///   <para>Get the Change resources in the ManagedZone "test1" in the Project "testing."</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcdChange -Project "testing" -Zone "test1" -ChangeId "0"</code>
    ///   <para>
    ///     Get the Change resource with id "0" in the ManagedZone "test1" in the Project "testing."
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/dns/monitoring)">[Monitoring Changes]</para>
    /// <para type="link" uri="(https://cloud.google.com/dns/troubleshooting)">[Troubleshooting]</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcdChange")]
    [OutputType(typeof(Change))]
    public class GetGcdChangeCmdlet : GcdCmdlet
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
        /// Get the ManagedZone (name or id permitted) to check for changes.
        /// </para>
        /// </summary>
        [Alias("ManagedZone")]
        [Parameter(Position = 0, Mandatory = true)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the id of the specific change to return.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = false)]
        public string ChangeId { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            if (!String.IsNullOrEmpty(ChangeId))
            {
                ChangesResource.GetRequest changeGetRequest = Service.Changes.Get(Project, Zone, ChangeId);
                Change changeResponse = changeGetRequest.Execute();
                WriteObject(changeResponse);
            }
            else
            {
                WriteObject(GetGcdChange(Project, Zone), true);
            }
        }

        /// <summary>
        /// Returns all GCD changes within a zone of a project.
        /// </summary>
        private IEnumerable<Change> GetGcdChange(string project, string zone)
        {
            ChangesResource.ListRequest changeListRequest = Service.Changes.List(project, zone);
            do
            {
                ChangesListResponse changeListResponse = changeListRequest.Execute();
                IList<Change> changeList = changeListResponse.Changes;
                if (changeListResponse.Changes != null)
                {
                    foreach (Change change in changeListResponse.Changes)
                    {
                        yield return change;
                    }
                }
                changeListRequest.PageToken = changeListResponse.NextPageToken;
            }
            while (changeListRequest.PageToken != null);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Add a new Change to a ManagedZone of a Project.
    /// </para>
    /// <para type="description">
    /// Create, execute, and return a new Change request within a specified ManagedZone of a Project.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, will instead create the Change in the specified ManagedZone governed by that 
    /// project. Either a Change request or ResourceRecordSet[] to add/remove can be given as input.
    /// </para>
    /// <example>   
    ///   <code>
    ///     $newARecord = New-GcdResourceRecordSet -Name "gcloudexample1.com." `
    ///         -Rrdata "104.1.34.167"
    ///     $oldCNAMERecord = (Get-GcdResourceRecordSet -Zone "test1" -Filter "CNAME")[0]
    ///     Add-GcdChange -Project "proj" -Zone "test1" `
    ///         -Add $newARecord -Remove $oldCNAMERecord
    ///   </code>
    ///   <para> 
    ///   Add a new Change that adds a new A-type ResourceRecordSet, $newARecord, and removes an existing CNAME-type 
    ///   record, $oldCNAMERecord, from the ManagedZone "test1" (governing "gcloudexample1.com.") in the Project 
    ///   "testing."
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> $change2 = Get-GcdChange -Project "testing" -Zone "test1" -ChangeId 2
    ///   PS C:\> Add-GcdChange -Project "proj" -Zone "test1" -ChangeRequest $change2
    ///   </code>
    ///   <para>
    ///   Add the Change request $change2 to the ManagedZone "test1" in the Project "testing," where $change2 is a 
    ///   previously executed Change request in ManagedZone "test1" that we want to apply again.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/dns/monitoring)">[Monitoring Changes]</para>
    /// <para type="link" uri="(https://cloud.google.com/dns/troubleshooting)">[Troubleshooting]</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcdChange", DefaultParameterSetName = ParameterSetNames.ChangeRequest)]
    [OutputType(typeof(Change))]
    public class AddGcdChangeCmdlet : GcdCmdlet
    {
        private class ParameterSetNames
        {
            public const string ChangeRequest = "ChangeRequestSet";
            public const string AddRm = "AddRmSet";
        }

        private class LocalErrorMessages
        {
            public const string NeedChangeContent =
                "Must specify at least 1 non-null, non-empty value for Add or Remove.";
        }

        /// <summary>
        /// <para type="description">
        /// Get the Project to change.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the ManagedZone (name or id permitted) to change.
        /// </para>
        /// </summary>
        [Alias("ManagedZone")]
        [Parameter(Position = 0, Mandatory = true)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the Change request to execute.
        /// </para>
        /// </summary>
        [Alias("Change")]
        [Parameter(ParameterSetName = ParameterSetNames.ChangeRequest, Position = 1, Mandatory = true,
            ValueFromPipeline = true)]
        public Change ChangeRequest { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the ResourceRecordSet(s) to add for this Change.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AddRm, Mandatory = false)]
        public ResourceRecordSet[] Add { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the ResourceRecordSet(s) to remove (must exactly match existing ones) for this Change.
        /// </para>
        /// </summary>
        [Alias("Rm")]
        [Parameter(ParameterSetName = ParameterSetNames.AddRm, Mandatory = false)]
        public ResourceRecordSet[] Remove { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            Change changeContent;

            switch (ParameterSetName)
            {
                case ParameterSetNames.AddRm:
                    if ((Add == null || Add.Length == 0) && (Remove == null || Remove.Length == 0))
                    {
                        throw new System.ArgumentException(LocalErrorMessages.NeedChangeContent);
                    }
                    else
                    {
                        changeContent = new Change
                        {
                            Additions = Add,
                            Deletions = Remove
                        };
                    }
                    break;

                case ParameterSetNames.ChangeRequest:
                    changeContent = ChangeRequest;
                    break;

                default:
                    throw UnknownParameterSetException;
            }

            ChangesResource.CreateRequest changeCreateRequest =
                Service.Changes.Create(changeContent, Project, Zone);
            Change changeResponse = changeCreateRequest.Execute();
            WriteObject(changeResponse);
        }
    }
}
