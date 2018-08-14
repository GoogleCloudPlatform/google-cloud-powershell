// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using Google.PowerShell.ComputeEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.Compute
{
    /// <summary>
    /// <para type="synopsis">
    /// Creates a new disk snapshot.
    /// </para>
    /// <para type="description">
    /// Creates a new disk snapshot to backup the data of the disk.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Add-GceSnapshot "my-disk" -Name "my-snapshot" </code>
    ///   <para>
    ///   Creates a new disk snapshot from the disk named "my-disk" in the default project and zone.
    ///   The name of the snapshot will be "my-snapshot".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceDisk "my-disk" | Add-GceSnapshot</code>
    ///   <para>
    ///   Creates a new disk snapshot from the disk named "my-disk". The name of the snapshot will start
    ///   with "my-disk" and end with the utc date and time the snapshot was taken.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/snapshots#resource)">
    /// [Snapshot resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceSnapshot")]
    [OutputType(typeof(Snapshot))]
    public class AddGceSnapshotCmdlet : GceConcurrentCmdlet
    {
        private static class ParamterSetNames
        {
            public const string FromDisk = "FromDisk";
            public const string FromDiskName = "FromDiskName";
        }

        /// <summary>
        /// <para type="description">
        /// The disk object to create the snapshot from.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParamterSetNames.FromDisk, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Disk Disk { get; set; }

        /// <summary>
        /// <para type="description">
        /// The project of the disk. Defaults to the gcloud config project.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParamterSetNames.FromDiskName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone the disk is in. Defaults to the gloud config zone.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParamterSetNames.FromDiskName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The map of labels (key/value pairs) to be applied to the snapshot.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public virtual Hashtable Label { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the disk to get a snapshot of.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParamterSetNames.FromDiskName, Mandatory = true, Position = 0)]
        public string DiskName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the snapshot. Defaults to &lt;DiskName&gt;-&lt;Timestamp&gt;
        /// </para>
        /// </summary>
        [Parameter]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// Human readable description of the snapshot.
        /// </para>
        /// </summary>
        [Parameter]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the snapshot created will be a Windows Volume Shadow Copy Service
        /// (VSS) snapshot. See:
        /// https://cloud.google.com/compute/docs/instances/windows/creating-windows-persistent-disk-snapshot?hl=en_US
        /// for more details.
        /// </para>
        /// </summary>
        [Parameter]
        [Alias("VSS")]
        public SwitchParameter GuestFlush { get; set; }

        protected override void ProcessRecord()
        {
            string diskName;
            string project;
            string zone;
            switch (ParameterSetName)
            {
                case ParamterSetNames.FromDisk:
                    project = GetProjectNameFromUri(Disk.SelfLink);
                    zone = GetZoneNameFromUri(Disk.SelfLink);
                    diskName = Disk.Name;
                    break;
                case ParamterSetNames.FromDiskName:
                    project = Project;
                    zone = Zone;
                    diskName = DiskName;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            var body = new Snapshot
            {
                Description = Description,
                Name = Name ?? $"{diskName}-{DateTime.UtcNow:yyyyMMddHHmmss\\z}",
                Labels = ConvertToDictionary<string, string>(Label)
            };

            DisksResource.CreateSnapshotRequest request = Service.Disks.CreateSnapshot(body, project, zone, diskName);
            if (GuestFlush)
            {
                request.GuestFlush = true;
            }
            Operation operation = request.Execute();
            AddZoneOperation(project, zone, operation, () =>
            {
                WriteObject(Service.Snapshots.Get(project, body.Name).Execute());
            });
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets information about a Google Compute Engine disk snapshots.
    /// </para>
    /// <para type="description">
    /// Gets information about a Google Compute Engine disk snapshots.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GceSnapshot</code>
    ///   <para>Lists all snapshot in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceSnapshot "my-snapshot"</code>
    ///   <para>Gets the snapshot in the default project named "my-snapshot".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/snapshots#resource)">
    /// [Snapshot resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceSnapshot", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(Snapshot))]
    public class GetGceSnapshot : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the snapshot. Defaults to the gcloud config project.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the snapshot to get.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.OfProject:
                    WriteObject(GetAllProjectSnapshots(), true);
                    break;
                case ParameterSetNames.ByName:
                    WriteObject(Service.Snapshots.Get(Project, Name).Execute());
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private IEnumerable<Snapshot> GetAllProjectSnapshots()
        {
            SnapshotsResource.ListRequest request = Service.Snapshots.List(Project);
            do
            {
                SnapshotList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (Snapshot snapshot in response.Items)
                    {
                        yield return snapshot;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes Google Compute Engine disk snapshots.
    /// </para>
    /// <para type="description">
    /// Deletes Google Compute Engine disk snapshots.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GceSnapshot "my-snapshot"</code>
    ///   <para>Deletes the snapshot named "my-snapshot" in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceSnapshot "my-snapshot" | Remove-GceSnapshot</code>
    ///   <para>Deletes the snapshot named "my-snapshot" in the default project.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/snapshots#resource)">
    /// [Snapshot resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceSnapshot", SupportsShouldProcess = true)]
    public class RemoveGceSnapshotCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByObject = "ByObject";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// The object that describes the snapshot to delete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Snapshot Object { get; set; }

        /// <summary>
        /// <para type="description">
        /// The project that owns the snapshot to delete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the snapshot to delete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            string project;
            string name;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByObject:
                    project = GetProjectNameFromUri(Object.SelfLink);
                    name = Object.Name;
                    break;
                case ParameterSetNames.ByName:
                    project = Project;
                    name = Name;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            if (ShouldProcess($"{project}/{name}", "Remove Snapshot"))
            {
                Operation operation = Service.Snapshots.Delete(project, name).Execute();
                AddGlobalOperation(project, operation);
            }
        }
    }
}
