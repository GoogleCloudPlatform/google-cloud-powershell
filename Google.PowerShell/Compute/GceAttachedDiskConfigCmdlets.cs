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
    [Cmdlet(VerbsCommon.New, "GceAttachedDiskConfig", DefaultParameterSetName = ParameterSetNames.Default)]
    public class NewGceAttachedDiskConfigCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string Default = "Default";
            public const string DefaultPipeline = "DefaultPipeline";
            public const string New = "New";
            public const string NewPipeline = "NewPipeline";
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
        /// Pipeline values to be passed on to the next cmdlet.
        /// </para>
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetNames.DefaultPipeline)]
        [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetNames.NewPipeline)]
        public object Pipeline { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the disk to create.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.New)]
        [Parameter(ParameterSetName = ParameterSetNames.NewPipeline)]
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
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.NewPipeline)]
        public string SourceImage { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specifies the type of the disk. Defaults to pd-standard
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.New)]
        [Parameter(ParameterSetName = ParameterSetNames.NewPipeline)]
        public string DiskType { get; set; }

        /// <summary>
        /// <para type="description">
        /// The url of the preexisting disk to attach to an instance.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.Default)]
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DefaultPipeline)]
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
        [Parameter(ParameterSetName = ParameterSetNames.NewPipeline)]
        public long? Size { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.NewPipeline:
                case ParameterSetNames.DefaultPipeline:
                    WriteObject(Pipeline);
                    break;
            }
        }

        protected override void EndProcessing()
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

            base.EndProcessing();
        }
    }
}