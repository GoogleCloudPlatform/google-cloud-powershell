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
}