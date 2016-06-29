// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.SQLAdmin.v1beta4;
using Google.Apis.SQLAdmin.v1beta4.Data;
using System.Management.Automation;
using Google.PowerShell.Common;

namespace Google.PowerShell.Sql
{
    /// <summary>
    /// <para type="synopsis">
    /// Retrieves a resource containing information about a backup run, or lists all backup runs for an instance.
    /// </para>
    /// <para type="description">
    /// Retrieves a resource containing information about a backup run, or lists all backup runs for an instance.
    /// This is decided by if the "Id" parameter is filled or not.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlBackupRun")]
    public class GetGcSqlBackupRunCmdlet : GcSqlCmdlet
    {
        internal class ParameterSetNames
        {
            public const string GetSingle = "Single";
            public const string GetList = "List";
        }
        /// <summary>
        /// <para type="description">
        /// Project name of the project that contains an instance.
        /// Defaults to the cloud sdk config for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.GetSingle)]
        [Parameter(ParameterSetName = ParameterSetNames.GetList)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Cloud SQL instance name. 
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.GetSingle)]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.GetList)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the Backup Run we want to get 
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.GetSingle)]
        public long Id { get; set; }

        protected override void ProcessRecord()
        {
            if (ParameterSetName == ParameterSetNames.GetList) 
            {
                BackupRunsResource.ListRequest request = Service.BackupRuns.List(Project, Instance);
                BackupRunsListResponse result = request.Execute();
                WriteObject(result.Items, true);
            }
            else
            {
                BackupRunsResource.GetRequest request = Service.BackupRuns.Get(Project, Instance, Id);
                BackupRun result = request.Execute();
                WriteObject(result);
            }
        }
    }
}
