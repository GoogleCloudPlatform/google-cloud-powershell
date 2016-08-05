// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Creates a single new AttachedDisk object. 
    /// </para>
    /// <para type="description">
    /// Creates a single new AttachedDisk object. These objects are used by New-GceInstanceConfig,
    /// Add-GceInstance, Add-GceInstanceTemplate, and Set-GceInstance.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// <para>PS C:\> $disks = (New-GceAttachedDiskConfig (Get-GceImage "debian-cloud" -Family "debian-8") -Boot -AutoDelete),</para>
    /// <para>                 (New-GceAttachedDiskConfig (Get-GceDisk "persistant-disk-name") -ReadOnly)</para>
    /// <para>PS C:\> Add-GceInstanceTemplate -Name "template-name" -Disk $disks</para>
    /// </code>
    /// <para>Creates two attached disk objects, and creates a new template using them.</para>
    ///  </example>
    [Cmdlet(VerbsCommon.New, "GceAttachedDiskConfig", DefaultParameterSetName = ParameterSetNames.Persistant)]
    public class NewGceAttachedDiskConfigCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string Persistant = "Persistant";
            public const string New = "New";
        }

        /// <summary>
        /// <para type="description">
        /// The URI of the preexisting disk to attach to an instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Persistant, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Disk Source { get; set; }

        /// <summary>
        /// <para type="description">
        /// The source image of the new disk.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.New,
            Position = 0, ValueFromPipeline = true)]
        public Image SourceImage { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the disk to create.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.New)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specifies the type of the disk. Defaults to pd-standard.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.New)]
        public string DiskType { get; set; }

        /// <summary>
        /// <para type="description">
        /// The size of the disk to create, in GB.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.New)]
        public long? Size { get; set; }

        /// <summary>
        /// <para type="description">
        /// When set, disk will be deleted when the instance is.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter AutoDelete { get; set; }

        /// <summary>
        /// <para type="description">
        /// When set, describes the boot disk of an instance.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter Boot { get; set; }

        /// <summary>
        /// <para type="description">
        /// When set, the disk interface will be NVME rather than SCSI.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter Nvme { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the disk on the instance.
        /// </para>
        /// </summary>
        [Parameter]
        public string DeviceName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Set to limit the instance to read operations.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter ReadOnly { get; set; }

        protected override void ProcessRecord()
        {
            var attachedDisk = new AttachedDisk
            {
                AutoDelete = AutoDelete,
                Boot = Boot,
                DeviceName = DeviceName,
                Interface__ = Nvme ? "NVME" : "SCSI",
                Mode = ReadOnly ? "READ_ONLY" : "READ_WRITE",
                Source = Source?.SelfLink
            };

            if (ParameterSetName == ParameterSetNames.New)
            {
                attachedDisk.InitializeParams = new AttachedDiskInitializeParams
                {
                    DiskName = Name,
                    DiskSizeGb = Size,
                    DiskType = DiskType,
                    SourceImage = SourceImage.SelfLink
                };
            }
            WriteObject(attachedDisk);
        }
    }
}
