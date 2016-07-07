﻿// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Creates a single new AttachedDisk object. 
    /// </para>
    /// <para type="description">
    /// Creates a single new AttachedDisk object. These objects are used by New-GceInstanceConfig and
    /// Add-GceInstanceTemplate.
    /// </para>
    /// </summary>
    /// <example>
    /// <para>
    /// <code>
    /// $disks = New-GceAttachedDisk -Boot -AutoDelete -SourceImage "projects/debian-cloud/global/images/family/debian-8" |
    ///     New-GceAttachedDis -Source "projectDiskName" -ReadOnly
    /// 
    /// Add-GceinstanceTemplate -Name "instanceName" -MachineType n1-standard-1 -Disk $disks 
    /// </code>
    /// </para> </example>
    [Cmdlet(VerbsCommon.New, "GceAttachedDiskConfig", DefaultParameterSetName = ParameterSetNames.Default)]
    public class NewGceAttachedDiskConfigCmdlet : GceCmdlet
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
        /// Pipeline values to be passed on to the next cmdlet.
        /// </para>
        /// </summary>
        [Parameter(ValueFromPipeline = true)]
        public object Pipeline { get; set; }

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
        /// The source image of the new disk.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.New)]
        public string SourceImage { get; set; }

        /// <summary>
        /// <para type="description">
        /// Specifies the type of the disk. Defaults to pd-standard.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.New)]
        public string DiskType { get; set; }

        /// <summary>
        /// <para type="description">
        /// The URI of the preexisting disk to attach to an instance.
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
        /// The size of the disk to create, in GB.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.New)]
        public long? Size { get; set; }

        /// <summary>
        /// Move objects from the input pipeline to the output pipeline.
        /// </summary>
        protected override void ProcessRecord()
        {
            // If pipeline is a bound parameter, it has a value. Pass that value on to the next cmdlet.
            ICollection<string> keys = MyInvocation.BoundParameters.Keys;
            if (keys.Any(k => k == "Pipeline"))
            {
                WriteObject(Pipeline);
            }
        }

        /// <summary>
        /// Create the AttachedDisk object and add it to the end of the pipeline.
        /// </summary>
        protected override void EndProcessing()
        {
            var attachedDisk = new AttachedDisk
            {
                AutoDelete = AutoDelete,
                Boot = Boot,
                DeviceName = DeviceName,
                Interface__ = Nvme ? "NVME" : "SCSI",
                Mode = ReadOnly ? "READ_ONLY" : "READ_WRITE",
                Source = Source
            };

            if (ParameterSetName == ParameterSetNames.New)
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
