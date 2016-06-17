// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Creates a new AttachedDisk object. 
    /// </para>
    /// <para type="description">
    /// Creates a new AttachedDisk object. These objects are used by New-GceInstanceConfig and
    /// Add-GceInstanceTemplate.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GceInstanceDiskConfig", DefaultParameterSetName = ParameterSetNames.Default)]
    public class NewGceInstanceDiskConfigCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string Default = "Default";
            public const string New = "New";
        }

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
        /// The name of the disk to create.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.New)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// When set, the disk interface will be NVME rather than SCSI.
        [Parameter]
        public SwitchParameter NVME { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the disk on the instance.
        /// </para>
        /// </summary>
        [Parameter]
        public string DeviceName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The source image of the new disk.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.New)]
        public string SourceImage { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specifies the type of the disk. Defaults to pd-standard
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.New)]
        public string DiskType { get; set; }

        /// <summary>
        /// <para type="description">
        /// The url of the preexisting disk to attach to an instance.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.Default)]
        public string Source { get; set; }

        /// <summary>
        /// <para type="description">
        /// Set to limit the instance to read operations.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter ReadOnly { get; set; }

        /// <summary>
        /// <para type="description">
        /// Set to make the disk type SCRATCH rather than PERSISTENT
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter Scratch { get; set; }

        /// <summary>
        /// <para type="description">
        /// The size of the disk to create, in GB.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.New)]
        public long? Size { get; set; }

        protected override void ProcessRecord()
        {
            var attachedDisk = new AttachedDisk
            {
                AutoDelete = AutoDelete,
                Boot = Boot,
                DeviceName = DeviceName,
                Interface__ = NVME ? "NVME" : "SCSI",
                Mode = ReadOnly ? "READ_ONLY" : "READ_WRITE",
                Source = Source,
                Type = Scratch ? "SCRATCH" : "PERSISTENT"
            };

            if (ParameterSetName.Equals(ParameterSetNames.New))
            {
                attachedDisk.InitializeParams = new AttachedDiskInitializeParams
                {
                    DiskName = Name,
                    DiskSizeGb = Size,
                    DiskType = DiskType,
                    SourceImage = SourceImage
                };
            }
            WriteObject(attachedDisk);
        }
    }
}