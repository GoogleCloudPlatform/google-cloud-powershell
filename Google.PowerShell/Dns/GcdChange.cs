// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Dns.v1;
using Google.Apis.Dns.v1.Data;
using Google.PowerShell.Common;
using Google.PowerShell.Dns.ExtensionMethods;
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
    /// If a Project is specified, will instead return the changes in the specified ManagedZone governed by that project. 
    /// The filter ChangeId can be provided to return that specific change.
    /// </para>
    /// <example>
    ///   <para>Get the Change resources in the ManagedZone "test1" in the Project "testing."</para>
    ///   <para><code>Get-GcdChange -Project "testing" -ManagedZone "test1"</code></para>
    /// </example>
    /// <example>
    ///   <para>Get the Change resource with id "0" in the ManagedZone "test1" in the Project "testing."</para>
    ///   <para><code>Get-GcdChange -Project "testing" -ManagedZone "test1" -ChangeId "0"</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcdChange")]
    public class GetGcdChangeCmdlet : GcdCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Get the project to check.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the ManagedZone (name or id permitted) to check for changes.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string ManagedZone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the id of the specific change to return.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = false)]
        public string ChangeId { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            if (!String.IsNullOrEmpty(ChangeId))
            {
                ChangesResource.GetRequest changeGetRequest = Service.Changes.Get(Project, ManagedZone, ChangeId);
                Change change = changeGetRequest.Execute();
                WriteObject(change);
            }
            else
            {
                ChangesResource.ListRequest changeListRequest = Service.Changes.List(Project, ManagedZone);
                ChangesListResponse changeListResponse = changeListRequest.Execute();
                IList<Change> changeList = changeListResponse.Changes;
                WriteObject(changeList, true);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Add a new Change to a ManagedZone of a Project.
    /// </para>
    /// <para type="description">
    /// Create, execute, and return a new Change within a specified ManagedZone of a Project.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, will instead create the Change in the specified ManagedZone governed by that project. 
    /// Either a Change object or ResourceRecordSets array(s) for Additions and/or Deletions can be given as input.
    /// </para>
    /// <example>
    ///   <para>Add the Change $change1 to the ManagedZone "test1" in the Project "testing."</para>
    ///   <para><code>Add-GcdChange -Project "testing" -ManagedZone "test1" -ChangeObject $change1</code></para>
    /// </example>
    /// <example>
    ///   <para> 
    ///   Add a new Change that adds the ResourceRecordSets $addRrsets and deletes the ResourceRecordSets $delRrsets
    ///   in the ManagedZone "test1" in the Project "testing."
    ///   </para>
    ///   <para><code>Add-GcdChange -Project "testing" -ManagedZone "test1" -Add $addRrsets -Remove $delRrsets</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcdChange")]
    public class AddGcdChangeCmdlet : GcdCmdlet
    {
        private class ParameterSetNames
        {
            public const string ChangeRequest = "ChangeRequestSet";
            public const string AddRm = "AddRmSet";
        }

        /// <summary>
        /// <para type="description">
        /// Get the project to change.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the ManagedZone (name or id permitted) to change.
        /// </para>
        /// </summary>
        [Alias("ManagedZone")]
        [Parameter(Position = 1, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the Change to execute.
        /// </para>
        /// </summary>
        [Alias("Change")]
        [Parameter(ParameterSetName = ParameterSetNames.ChangeRequest, Position = 2, Mandatory = true, ValueFromPipeline = true)]
        public Change ChangeRequest { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the ResourceRecordSets to add for this Change.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AddRm, Mandatory = false)]
        public ResourceRecordSet[] Add { get; set; }

        /// <summary>
        /// <para type="description">
        /// Get the ResourceRecordSets to delete (must exactly match existing ones) in this Change.
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
                    if (Add.IsNullOrEmpty() && Remove.IsNullOrEmpty())
                    {
                        throw new System.ArgumentException("Must specify at least 1 Add or Remove, or provide a Change request, to execute.");
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
                    throw new InvalidOperationException($"{ParameterSetName} is not a valid ParameterSet.");
            }

            ChangesResource.CreateRequest changeCreateRequest = Service.Changes.Create(changeContent, Project, Zone);
            Change change = changeCreateRequest.Execute();
            WriteObject(change);
        }
    }
}
