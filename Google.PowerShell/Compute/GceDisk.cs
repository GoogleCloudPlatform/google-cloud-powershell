// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets the Google Compute Engine disks associated with a project.
    /// </para>
    /// <para type="description">
    /// Returns the project's Google Compute Engine disk objects.
    /// </para>
    /// <para type="description">
    /// If a Project is specified, will instead return all disks owned by that project. Filters,
    /// such as Zone or Name, can be provided to restrict the objects returned.
    /// </para>
    /// <example>
    ///   <para>Get the disk named "ppiper-frontend".</para>
    ///   <para><code>Get-GceDisk -Project "ppiper-prod" "ppiper-frontend"</code></para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceDisk")]
    public class GetGceDiskCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for Compute Engine disks.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specific zone to lookup disks in, e.g. "us-central1-a". Partial names
        /// like "us-" or "us-central1" will also work.
        /// </para>
        [Parameter(Position = 1, Mandatory = false)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the disk you want to have returned.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = false)]
        public string DiskName { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            // Special case. If you specify the Project, Zone, and DiskName we use the Get command to
            // get the specific disk. This will throw a 404 if it does not exist.
            if (!String.IsNullOrEmpty(Project) && !String.IsNullOrEmpty(Zone) && !String.IsNullOrEmpty(DiskName))
            {
                DisksResource.GetRequest getReq = Service.Disks.Get(Project, Zone, DiskName);
                Disk disk = getReq.Execute();
                WriteObject(disk);
                return;
            }

            DisksResource.AggregatedListRequest listReq = Service.Disks.AggregatedList(Project);
            // The v1 version of the API only supports one filter at a time. So we need to
            // specify a filter here and manually filter results later. Also, since the only
            // operations are "eq" and "ne", we don't use the filter for zone so that we can
            // can allow filtering by regions.
            if (!String.IsNullOrEmpty(DiskName))
            {
                listReq.Filter = $"name eq \"{DiskName}\"";
            }

            // First page. AggregatedList.Items is a dictionary of zone to disks.
            DiskAggregatedList disks = listReq.Execute();
            foreach (DisksScopedList diskList in disks.Items.Values)
            {
                WriteDiskObjects(diskList.Disks);
            }

            // Keep paging through results as necessary.
            while (disks.NextPageToken != null)
            {
                listReq.PageToken = disks.NextPageToken;
                disks = listReq.Execute();
                foreach (DisksScopedList diskList in disks.Items.Values)
                {
                    WriteDiskObjects(diskList.Disks);
                }
            }
        }

        /// <summary>
        /// Writes the collection of disks to the cmdlet's output pipeline, but filtering results
        /// based on the cmdlets parameters.
        /// </summary>
        protected void WriteDiskObjects(IEnumerable<Disk> disks)
        {
            if (disks == null)
            {
                return;
            }

            foreach (Disk disk in disks)
            {
                if (!String.IsNullOrEmpty(DiskName) && disk.Name != DiskName)
                {
                    continue;
                }

                if (!String.IsNullOrEmpty(Zone) && !disk.Zone.Contains(Zone))
                {
                    continue;
                }

                WriteObject(disk);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a new Google Compute Engine disk object.
    /// </para>
    /// <para type="description">
    /// Creates a new Google Compute Engine disk object.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GceDisk", DefaultParameterSetName = ParameterSetNames.EmptyDisk)]
    public class NewGceDiskCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string EmptyDisk = "EmptyDisk";
            public const string FromImage = "FromImage";
            public const string FromSnapshot = "FromSnapshot";
        }

        /// <summary>
        /// <para type="description">
        /// The project to associate the new Compute Engine disk.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specific zone to create the disk in, e.g. "us-central1-a".
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the disk.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.EmptyDisk, Mandatory = true, Position = 0)]
        [Parameter(ParameterSetName = ParameterSetNames.FromImage, Mandatory = true, Position = 0)]
        [Parameter(ParameterSetName = ParameterSetNames.FromSnapshot, Mandatory = true, Position = 0)]
        // Mention all three paramter sets so help documentation will know about EmptyDisk.
        public string DiskName { get; set; }

        /// <summary>
        /// <paratype="description">
        /// Optional description of the disk.
        /// </paratype>
        /// </summary>
        [Parameter]
        public string Description { get; set; }

        /// <summary>
        /// <paratype="description">
        /// Specify the size of the disk in GiB.
        /// </paratype>
        /// </summary>
        [Parameter]
        public long? SizeGb { get; set; }

        /// <summary>
        /// <paratype="description">
        /// Type of disk, e.g. pd-ssd or pd-standard.
        /// </paratype>
        /// </summary>
        [Parameter, ValidateSet("pd-ssd", "pd-standard")]
        public string DiskType { get; set; }

        /// <summary>
        /// <para type="description">
        /// Source image to apply to the disk.
        /// </para>
        /// <para type="description">
        /// Use Get-GceImage to get the image to apply. For instance, to get the latest windows instance, use
        /// <code>Get-GceImage -Family "windows-2012-r2" -Project "windows-cloud"</code>.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromImage, Mandatory = true,
            Position = 1, ValueFromPipeline = true)]
        public Image Image { get; set; }

        /// <summary>
        /// <para type="description">
        /// Source snapshot to apply to the disk.
        /// </para>
        /// <para type="description">
        /// Use Get-GceSnapshot to get a previously made backup snapshot to apply to this disk.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromSnapshot, Mandatory = true,
            Position = 1, ValueFromPipeline = true)]
        public Snapshot Snapshot { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            // The GCE API requires disk types to be specified as URI-like things.
            // If null will default to "pd-standard"
            string diskTypeResource = DiskType;
            if (diskTypeResource != null)
            {
                diskTypeResource = $"zones/{Zone}/diskTypes/{DiskType}";
            }

            Disk newDisk = new Disk
            {
                Name = DiskName,
                Type = diskTypeResource,
                SourceSnapshot = Snapshot?.SelfLink,
                SourceImage = Image?.SelfLink,
                // Optional fields. OK if null.
                Description = Description,
                SizeGb = SizeGb
            };

            DisksResource.InsertRequest insertReq = Service.Disks.Insert(newDisk, Project, Zone);

            Operation op = insertReq.Execute();
            AddZoneOperation(Project, Zone, op, () =>
            {
                // Return the newly created disk.
                DisksResource.GetRequest getReq = Service.Disks.Get(Project, Zone, DiskName);
                Disk disk = getReq.Execute();
                WriteObject(disk);
            });

        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Resize a Compute Engine disk object.
    /// </para>
    /// <para type="description">
    /// Resize a Compute Engine disk object.
    /// </para>
    /// </summary>
    [Cmdlet("Resize", "GceDisk")]
    public class ResizeGceDiskCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to associate the new Compute Engine disk.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specific zone to create the disk in, e.g. "us-central1-a".
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the disk.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true), ValidatePattern("[a-z]([-a-z0-9]*[a-z0-9])?")]
        public string DiskName { get; set; }

        /// <summary>
        /// <paratype="description">
        /// Specify the new size of the disk in GiB. Must be larger than the current disk size.
        /// </paratype>
        /// </summary>
        [Parameter(Position = 3, Mandatory = true)]
        public long NewSizeGb { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            DisksResizeRequest diskResizeReq = new DisksResizeRequest();
            diskResizeReq.SizeGb = NewSizeGb;

            DisksResource.ResizeRequest resizeReq = Service.Disks.Resize(diskResizeReq, Project, Zone, DiskName);

            Operation op = resizeReq.Execute();
            WaitForZoneOperation(Project, Zone, op);

            // Return the updated disk.
            DisksResource.GetRequest getReq = Service.Disks.Get(Project, Zone, DiskName);
            Disk disk = getReq.Execute();

            WriteObject(disk);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes a Compute Engine disk.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceDisk", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.ByName)]
    public class RemoveGceDiskCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }
        /// <summary>
        /// <para type="description">
        /// The project to associate the new Compute Engine disk.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specific zone to create the disk in, e.g. "us-central1-a".
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the disk.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Position = 0, Mandatory = true)]
        public string DiskName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Disk object that describes the disk to remove
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Disk Object { get; set; }

        protected override void ProcessRecord()
        {
            string name;
            string zone;
            string project;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    name = DiskName;
                    zone = Zone;
                    project = Project;
                    break;
                case ParameterSetNames.ByObject:
                    name = Object.Name;
                    zone = GetZoneNameFromUri(Object.SelfLink);
                    project = GetProjectNameFromUri(Object.SelfLink);
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            if (!ShouldProcess($"{project}/{zone}/{name}", "Delete Disk"))
            {
                return;
            }

            DisksResource.DeleteRequest deleteReq = Service.Disks.Delete(project, zone, name);

            Operation op = deleteReq.Execute();
            AddZoneOperation(project, zone, op);
        }
    }
}
