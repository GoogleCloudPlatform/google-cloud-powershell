using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using Google.PowerShell.ComputeEngine;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.Compute
{
    /// <summary>
    /// <para type="synopsis">
    /// Creates a new disk snapshot.
    /// </para>
    /// <para type="description">
    /// Creates a new disk snapshot.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceSnapshot")]
    public class AddGceSnapshotCmdlet : GceConcurrentCmdlet
    {
        private class ParamterSetNames
        {
            public const string FromDisk = "FromDisk";
            public const string FromDiskName = "FromDiskName";
        }

        /// <summary>
        /// <para type="description">
        /// The disk object to create the snapshot from
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
        public string Project { get; set; }

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

            Snapshot body = new Snapshot
            {
                Description = Description,
                Name = Name ?? $"{diskName}-{DateTime.UtcNow.ToString("u")}"
            };
            Operation operation = Service.Disks.CreateSnapshot(body, project, zone, diskName).Execute();
            AddZoneOperation(project, zone, operation, WriteSnapshotCallback(project, body.Name));
        }

        private Action WriteSnapshotCallback(string project, string name)
        {
            return () =>
                WriteObject(Service.Snapshots.Get(project, name).Execute());
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets information about a google compute engine disk snapshots.
    /// </para>
    /// <para type="description">
    /// Gets information about a google compute engine disk snapshots.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceSnapshot", DefaultParameterSetName = ParameterSetNames.OfProject)]
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
        public string Project { get; set; }

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
    /// Deletes google compute engine disk snapshots.
    /// </para>
    /// <para type="description">
    /// Deletes google compute engine disk snapshots.
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
        public string Project { get; set; }

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
