using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

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
        [Parameter(Position = 0, Mandatory = true)]
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
            var service = GetComputeService();

            // Special case. If you specify the Project, Zone, and DiskName we use the Get command to
            // get the specific disk. This will throw a 404 if it does not exist.
            if (!String.IsNullOrEmpty(Project) && !String.IsNullOrEmpty(Zone) && !String.IsNullOrEmpty(DiskName))
            {
                DisksResource.GetRequest getReq = service.Disks.Get(Project, Zone, DiskName);
                Disk disk = getReq.Execute();
                WriteObject(disk);
                return;
            }

            DisksResource.AggregatedListRequest listReq = service.Disks.AggregatedList(Project);
            // The v1 version of the API only supports one filter at a time. So we need to
            // specify a filter here and manually filter results later. Also, since the only
            // operations are "eq" and "ne", we don't use the filter for zone so that we can
            // can allow filtering by regions.
            if (!String.IsNullOrEmpty(DiskName))
            {
                listReq.Filter = $"name eq \"{this.DiskName}\"";
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
    [Cmdlet(VerbsCommon.New, "GceDisk")]
    public class NewGceDiskCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to associate the new Compute Engine disk.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specific zone to create the disk in, e.g. "us-central1-a".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the disk.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true), ValidatePattern("[a-z]([-a-z0-9]*[a-z0-9])?")]
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
        [Parameter, ValidateSet(new string[] { "pd-ssd", "pd-standard" })]
        public string DiskType { get; set; }

        /// <summary>
        /// <paratype="description">
        /// Source image to apply to the disk.
        /// </paratype>
        /// <paratype="description">
        /// Or you can provide an image from a publicly-available project. For example, to use a
        /// Windows Serve image use "projects/windows-cloud/global/images/family/windows-2012-r2".
        /// For more information type `gcloud compute images list`.
        /// </paratype>
        /// </summary>
        [Parameter]
        public string SourceImage { get; set; }

        // TODO(chrsmith): Provide a way to create new disks from an existing disk snapshot.
        // A prereq for this is having PowerShell support for snapshots, etc.

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetComputeService();

            // The GCE API requires disk types to be specified as URI-like things.
            // If null will default to "pd-standard"
            string diskTypeResource = DiskType;
            if (diskTypeResource != null)
            {
                diskTypeResource = $"zones/{Zone}/diskTypes/{DiskType}";
            }

            Disk newDisk = new Disk();
            newDisk.Name = DiskName;
            newDisk.Description = Description;
            // Optional fields. OK if null.
            newDisk.SizeGb = SizeGb;
            newDisk.Type = diskTypeResource;

            DisksResource.InsertRequest insertReq = service.Disks.Insert(newDisk, Project, Zone);
            insertReq.SourceImage = SourceImage;
            // TODO(chrsmith): Support creating disks based on existing snapshots. See
            // comment above for more info.

            Operation op = insertReq.Execute();
            WaitForZoneOperation(service, Project, Zone, op);

            // Return the newly created disk.
            DisksResource.GetRequest getReq = service.Disks.Get(Project, Zone, DiskName);
            Disk disk = getReq.Execute();

            WriteObject(disk);
        }
    }

    // TODO(chrsmith): Support -Force, -WhatIf, etc.

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
        [Parameter(Position = 0, Mandatory = true)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specific zone to create the disk in, e.g. "us-central1-a".
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
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
            var service = GetComputeService();

            DisksResizeRequest diskResizeReq = new DisksResizeRequest();
            diskResizeReq.SizeGb = NewSizeGb;

            DisksResource.ResizeRequest resizeReq = service.Disks.Resize(diskResizeReq, Project, Zone, DiskName);

            Operation op = resizeReq.Execute();
            WaitForZoneOperation(service, Project, Zone, op);

            // Return the updated disk.
            DisksResource.GetRequest getReq = service.Disks.Get(Project, Zone, DiskName);
            Disk disk = getReq.Execute();

            WriteObject(disk);
        }
    }

    // TODO(chrsmith): Support -Force, -WhatIf, etc.

    /// <summary>
    /// <para type="synopsis">
    /// Deletes a Compute Engine disk.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceDisk", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public class RemoveGceDiskCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to associate the new Compute Engine disk.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specific zone to create the disk in, e.g. "us-central1-a".
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the disk.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true), ValidatePattern("[a-z]([-a-z0-9]*[a-z0-9])?")]
        public string DiskName { get; set; }

        [Parameter]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            var service = GetComputeService();

            base.ProcessRecord();
            if (!base.ConfirmAction(Force.IsPresent, DiskName, "Remove-GceDisk (DeleteDisk)"))
            {
                return;
            }

            // First try to get the disk, this way the cmdlet fails with a 404 if the
            // disk does not exist. (Otherwise the delete operation would succeed when
            // trying to delete a non-existant disk.)
            service.Disks.Get(Project, Zone, DiskName);

            DisksResource.DeleteRequest deleteReq = service.Disks.Delete(Project, Zone, DiskName);

            Operation op = deleteReq.Execute();
            WaitForZoneOperation(service, Project, Zone, op);
        }
    }
}
