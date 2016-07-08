// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.SQLAdmin.v1beta4;
using Google.Apis.SQLAdmin.v1beta4.Data;
using System.Management.Automation;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System;

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
                IEnumerable<BackupRun> backups = GetAllBackupRuns();
                WriteObject(backups, true);
            }
            else
            {
                BackupRunsResource.GetRequest request = Service.BackupRuns.Get(Project, Instance, Id);
                BackupRun result = request.Execute();
                WriteObject(result);
            }
        }

        private IEnumerable<BackupRun> GetAllBackupRuns()
        {
            BackupRunsResource.ListRequest request = Service.BackupRuns.List(Project, Instance);
            do
            {
                BackupRunsListResponse aggList = request.Execute();
                IList<BackupRun> backupRuns = aggList.Items;
                if (backupRuns == null)
                {
                    yield break;
                }
                foreach (BackupRun backupRun in backupRuns)
                {
                    yield return backupRun;
                }
                request.PageToken = aggList.NextPageToken;
            }
            while (request.PageToken != null);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes a specified backup from a Cloud SQL instance.
    /// </para>
    /// <para type="description">
    /// Deletes a specified backup from a Cloud SQL instance.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcSqlBackupRun", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.ByName)]
    public class RemoveGcSqlBackupRunCmdlet : GcSqlCmdlet
    {

        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// Project name of the project that contains an instance.
        /// Defaults to the cloud sdk config for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Cloud SQL instance name. 
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByName)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ID of the Backup Run we want to delete
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.ByName)]
        public long Id { get; set; }

        /// <summary>
        /// <para type="description">
        /// The BackupRun that describes the backup we want to delete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public BackupRun Backup { get; set; }

        protected override void ProcessRecord()
        {
            long? id;
            string instance;
            string project;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    id = Id;
                    instance = Instance;
                    project = Project;
                    break;
                case ParameterSetNames.ByObject:
                    id = Backup.Id;
                    instance = Backup.Instance;
                    project = GetProjectNameFromUri(Backup.SelfLink);
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            if (!ShouldProcess($"{project}/{instance}/{id}", "Delete Backup Run"))
            {
                return;
            }
            BackupRunsResource.DeleteRequest request = Service.BackupRuns.Delete(project, instance, (long)id);
            Operation result = request.Execute();
            WaitForSqlOperation(result);
        }
    }
}
