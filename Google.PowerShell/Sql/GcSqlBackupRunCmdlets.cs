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
    /// <example>
    ///   <code>PS C:\> Get-GcSqlBackupRun "myInstance"</code>
    ///   <para>Gets a list of backup runs for the instance "myInstance".</para>
    ///   <para>If successful, the command returns a list of backupruns the instance has.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcSqlBackupRun "myInstance" "1234"</code>
    ///   <para>Gets the resource for the backup run with ID "1234" from instance "myInstance".</para>
    ///   <para>If successful, the command returns the relevant backup run.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/sql/docs/backup-recovery/backups)">[Overview of Backups]</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlBackupRun", DefaultParameterSetName = ParameterSetNames.GetList)]
    [OutputType(typeof(BackupRun))]
    public class GetGcSqlBackupRunCmdlet : GcSqlCmdlet
    {
        internal class ParameterSetNames
        {
            public const string GetSingle = "Single";
            public const string GetList = "List";
        }
        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Cloud SQL instance name. 
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
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
    /// <example>
    ///   <code>PS C:\> Remove-GcSqlBackupRun "myInstance" "1234"</code>
    ///   <para>Removes the backup with ID "1234" from the instance "myInstance".</para>
    ///   <para>If successful, the command doesn't return anything.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcSqlBackupRun $myBackup</code>
    ///   <para>Removes the backup identified by the resource $myBackup.</para>
    ///   <para>If successful, the command doesn't return anything.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/tools/powershell/docs/sql/backup)">
    ///   [Managing Backups]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/sql/docs/backup-recovery/backups)">
    ///   [Overview of Backups]
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
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

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
            long id;
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
                    id = (long)Backup.Id;
                    instance = Backup.Instance;
                    project = GetProjectNameFromUri(Backup.SelfLink);
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            if (ShouldProcess($"{project}/{instance}/{id}", "Delete Backup Run"))
            {
                WriteVerbose($"Removing Backup Run '{id}' from the Instance '{instance}'.");
                BackupRunsResource.DeleteRequest request = Service.BackupRuns.Delete(project, instance, id);
                Operation result = request.Execute();
                WaitForSqlOperation(result);
            }
        }
    }
}
