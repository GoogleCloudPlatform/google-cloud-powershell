﻿// Copyright 2015-2016 Google Inc. All Rights Reserved.
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
    /// <example>
    ///   <para>
    ///     <code> PS C:\> $config = New-GceInstanceConfig -Name "new-instance" -BootDiskImage $image</code>
    ///   </para>
    ///   <para> Creates a new instance description and saves it to $config. The new instance will create a new
    ///   boot disk from $image.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GceInstanceConfig")]
    [OutputType(typeof(Instance))]
    public class NewGceInstanceConfigCmdlet : GceInstanceDescriptionCmdlet
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
        /// The machine type of this instance. Can be a name, a URL or a MachineType object from
        /// Get-GceMachineType. Defaults to "n1-standard-1".
        /// </para>
        /// </summary>
        [Parameter(Position = 1)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(MachineType),
            Property = nameof(Apis.Compute.v1.Data.MachineType.SelfLink))]
        public override string MachineType { get; set; } = "n1-standard-1";

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
        /// Human readable description of this instance.
        /// </para>
        /// </summary>
        [Parameter]
        public override string Description { get; set; }


        /// <summary>
        /// <para type="description">
        /// The persistant disk to use as a boot disk. Use Get-GceDisk to get one of these.
        /// </para>
        /// </summary>
        [Parameter]
        public override Disk BootDisk { get; set; }

        /// <summary>
        /// <para type="description">
        /// The the image used to create the boot disk. Use Get-GceImage to get one of these.
        /// </para>
        /// </summary>
        [Parameter]
        [Alias("DiskImage")]
        public override Image BootDiskImage { get; set; }


        /// <summary>
        /// <para type="description">
        /// An existing disk to attach in read only mode.
        /// </para>
        /// </summary>
        [Parameter]
        public override Disk[] ExtraDisk { get; set; }

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
        [PropertyByTypeTransformation(Property = nameof(Apis.Compute.v1.Data.Network.SelfLink),
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
        [PropertyByTypeTransformation(Property = nameof(Apis.Compute.v1.Data.Address.AddressValue),
            TypeToTransform = typeof(Address))]
        protected override string Address { get; set; }

        protected override void ProcessRecord()
        {
            WriteObject(BuildInstance());
        }
    }
}
