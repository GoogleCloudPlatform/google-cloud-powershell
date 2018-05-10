// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    ///   <para type="synopsis">
    ///     Use this cmdlet when you need to provide additional information to Set-GceInstance -AddDisk or
    ///     Add-GceInstance.
    ///   </para>
    ///   <para type="description">
    ///     Creates a single new AttachedDisk object. These objects are used by New-GceInstanceConfig,
    ///     Add-GceInstance, Add-GceInstanceTemplate and Set-GceInstance. They provide additional information
    ///     about the disk being attached, such as the local name of the disk, or whether the disk should be
    ///     automatically deleted.
    ///   </para>
    ///   <example>
    ///     <code>
    /// PS C:\> $image = Get-GceImage "windows-cloud" -Family "windows-2012-r2"
    /// PS C:\> $disks = (New-GceAttachedDiskConfig $image -Boot -AutoDelete), `
    ///                  (New-GceAttachedDiskConfig (Get-GceDisk "persistant-disk-name") -ReadOnly)
    /// PS C:\> Add-GceInstanceTemplate -Name "template-name" -Disk $disks
    ///     </code>
    ///     <para>Creates two attached disk objects, and creates a new template using them.</para>
    ///   </example>
    ///   <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instances/attachDisk#request-body)">
    ///     [Attached Disk resource definition]
    ///   </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GceAttachedDiskConfig", DefaultParameterSetName = ParameterSetNames.Persistent)]
    [OutputType(typeof(AttachedDisk))]
    public class NewGceAttachedDiskConfigCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string Persistent = "Persistent";
            public const string New = "New";
        }

        /// <summary>
        /// <para type="description">
        /// The URI of the preexisting disk to attach to an instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Persistent, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Disk Source { get; set; }

        /// <summary>
        /// <para type="description">
        /// The source image of the new disk.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.New,
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

            if(DiskType != null && GetUriPart("disktypes", DiskType.ToLower()) == "local-ssd")
            {
                attachedDisk.Type = "SCRATCH";
            }
            else
            {
                attachedDisk.Type = "PERSISTENT";
            }

            if (ParameterSetName == ParameterSetNames.New)
            {
                attachedDisk.InitializeParams = new AttachedDiskInitializeParams
                {
                    DiskName = Name,
                    DiskSizeGb = Size,
                    DiskType = DiskType,
                    SourceImage = SourceImage?.SelfLink
                };
            }
            WriteObject(attachedDisk);
        }
    }
}
