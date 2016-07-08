// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Collections;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Makes a new Google Compute Engine VM instance description.
    /// </para>
    /// <para type="description"> 
    /// Makes a new Google Compute Engine VM instance description.
    /// Use Add-GceInstance to instantiate the instance.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GceInstanceConfig")]
    public class NewGceInstanceConfigCmdlet : GceInstanceDescriptionCmdletBase
    {

        /// <summary>
        /// <para type="description">
        /// The name of the instance. The name must be 1-63 characters long and
        /// match [a-z]([-a-z0-9]*[a-z0-9])?
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true)]
        public override string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the machine type for this template.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public override string MachineType { get; set; }

        /// <summary>
        /// <para type="description">
        /// Enables instances to send and receive packets for IP addresses other than their own. Switch on if
        /// these instances will be used as an IP gateway or it will be set as the next-hop in a Route
        /// resource.
        /// </para>
        /// </summary>
        [Parameter]
        public override SwitchParameter CanIpForward { get; set; }

        /// <summary>
        /// <para type="description">
        /// Human readable description of this instance template.
        /// </para>
        /// </summary>
        [Parameter]
        public override string Description { get; set; }

        protected override Disk BootDisk { get; set; }
        /// <summary>
        /// <para type="description">
        /// The the image used to create the boot disk. Use Get-GceImage to get one of these.
        /// </para>
        /// </summary>
        [Parameter]
        public override Image BootDiskImage { get; set; }


        /// <summary>
        /// <para type="description">
        /// Name of existing disk to attach. All instances of this template will be able to
        /// read this disk.
        /// </para>
        /// </summary>
        [Parameter]
        public override string[] ExtraDiskName { get; set; }

        /// <summary>
        /// <para type="description">
        /// An AttachedDisk object specifying a disk to attach. Do not specify `-BootDiskImage` or
        /// `-BootDiskSnapshot` if this is a boot disk. You can build one using New-GceAttachedDiskConfig.
        /// </para>
        /// </summary>
        [Parameter]
        public override AttachedDisk[] Disk { get; set; }

        /// <summary>
        /// <para type="description">
        /// The keys and values of the Metadata of this instance.
        /// </para>
        /// </summary>
        [Parameter]
        public override IDictionary Metadata { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the network to use. If not specified, is default. This can be a Network object you get
        /// from Get-GceNetwork.
        /// </para>
        /// </summary>
        [Parameter]
        [PropertyByTypeTransformation(Property = nameof(DataType.network.SelfLink),
            TypeToTransform = typeof(Network))]
        public override string Network { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the instances will not have an external ip address.
        /// </para>
        /// </summary>
        [Parameter]
        public override SwitchParameter NoExternalIp { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the instances will be preemptible. If set, AutomaticRestart will be false.
        /// </para>
        /// </summary>
        [Parameter]
        public override SwitchParameter Preemptible { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the instances will not restart when shut down by Google Compute Engine.
        /// </para>
        /// </summary>
        [Parameter]
        public override bool AutomaticRestart { get; set; } = true;

        /// <summary>
        /// <para type="description">
        /// If set, the instances will terminate rather than migrate when the host undergoes maintenance.
        /// </para>
        /// </summary>
        [Parameter]
        public override SwitchParameter TerminateOnMaintenance { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ServiceAccount used to specify access tokens.
        /// </para>
        /// </summary>
        [Parameter]
        public override ServiceAccount[] ServiceAccount { get; set; }

        /// <summary>
        /// <para type="description">
        /// A tag of this instance.
        /// </para>
        /// </summary>
        [Parameter]
        public override string[] Tag { get; set; }

        /// <summary>
        /// <para type="description">
        /// The static ip address this instance will have. Can be a string, or and Address object from
        /// Get-GceAddress.
        /// </para>
        /// </summary>
        [PropertyByTypeTransformation(Property = nameof(DataType.address.AddressValue),
            TypeToTransform = typeof(Address))]
        protected override string Address { get; set; }

        protected override void ProcessRecord()
        {
            WriteObject(BuildInstance());
        }
    }
}
