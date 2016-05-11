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
        /// Specific zone to lookup disks in, e.g. "us-central1-a".
        /// </para>
        /// </summary>
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

            DisksResource.AggregatedListRequest listReq = service.Disks.AggregatedList(Project);
            // The v1 version of the API only supports one filter at a time. So we need to
            // specify a filter here and manually filter results later.
            if (!String.IsNullOrEmpty(Zone))
            {
                listReq.Filter = $"zone eq \"{this.Zone}\"";
            }
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

                if (!String.IsNullOrEmpty(Zone) && disk.Zone != Zone)
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
        [Parameter(Mandatory = true)]
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
        /// You can provide a private (custom) image using the following input, and Compute Engine
        /// will use the corresponding image from your project. For example:
        /// "global/images/my-private-image"
        /// </paratype>
        /// <paratype="description">
        /// Or you can provide an image from a publicly-available project.For example, to use a
        /// Debian image from the debian-cloud project, make sure to include the project in the URL:
        // "projects/debian-cloud/global/images/debian-7-wheezy-vYYYYMMDD"
        /// </paratype>
        /// <paratype="description">
        /// 
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

            // TODO(chrsmith): Validate the name matches "[a-z]([-a-z0-9]*[a-z0-9])?".

            Disk newDisk = new Disk();
            newDisk.SizeGb = SizeGb;
            newDisk.Type = DiskType;
            newDisk.SourceImage = SourceImage;
            // TODO(chrsmith): Support creating disks based on existing snapshots. See
            // comment above for more info.

            DisksResource.InsertRequest insertReq = service.Disks.Insert(newDisk, Project, Zone);
            /*
            Required field 'resource' not specified [400]
            Errors [
                Message[Required field 'resource' not specified] Location[ - ] Reason[required] Domain[global]
            ]
            */
            // I never "set" this parameter, so it should use the default value, right?
            insertReq.RequestParameters.Add(
                "trace",
                new Google.Apis.Discovery.Parameter
                {
                    Name = "trace",
                    ParameterType = "query",
                    DefaultValue = "email:chrsmith"
                });
            var req = insertReq.CreateRequest();
            WriteObject(req.RequestUri.ToString());

            // Wrap and return for sync? GceCmdlet peramter "-WaitForZoneOpsToComplete"?
            Operation op = insertReq.Execute();
            WriteObject(op);
        }
    }
}
