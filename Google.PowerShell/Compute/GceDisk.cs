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
}
