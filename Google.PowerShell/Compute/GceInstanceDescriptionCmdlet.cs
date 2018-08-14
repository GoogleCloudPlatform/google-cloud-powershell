// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// This abstract class describes all of the information needed to create an instance template description.
    /// It is extended by AddGceInstanceTemplateCmdlet, which sends an instance template description to the
    /// server, and by GceInstanceDescriptionCmdlet to provide a unifed set of parameters for instances and
    /// instance templates.
    /// </summary>
    public abstract class GceTemplateDescriptionCmdlet : GceConcurrentCmdlet
    {
        /// <summary>
        /// The name of the instance or instance template.
        /// </summary>
        public abstract string Name { get; set; }

        /// <summary>
        /// The name of the machine type for the instances.
        /// </summary>
        public abstract string MachineType { get; set; }

        /// <summary>
        /// Number of vCPUs used for a custom machine type.
        /// This has to be used together with CustomMemory.
        /// </summary>
        public abstract int CustomCpu { get; set; }

        /// <summary>
        /// Total amount of memory used for a custom machine type.
        /// This has to be used together with CustomCpu.
        /// The amount of memory is in MB.
        /// </summary>
        public abstract int CustomMemory { get; set; }

        /// <summary>
        /// Enables instances to send and receive packets for IP addresses other than their own. Switch on if
        /// this instance will be used as an IP gateway or it will be set as the next-hop in a Route
        /// resource.
        /// </summary>
        public abstract SwitchParameter CanIpForward { get; set; }

        /// <summary>
        /// Human readable description.
        /// </summary>
        public abstract string Description { get; set; }

        /// <summary>
        /// The the image used to create the boot disk. Use Get-GceImage to get one of these.
        /// </summary>
        public abstract Image BootDiskImage { get; set; }

        /// <summary>
        /// An existing disk to attach. It will be attached in read-only mode.
        /// </summary>
        public abstract Disk[] ExtraDisk { get; set; }

        /// <summary>
        /// An AttachedDisk object specifying a disk to attach. Do not specify `-BootDiskImage` or
        /// `-BootDiskSnapshot` if this is a boot disk. You can build one using New-GceAttachedDiskConfig.
        /// </summary>
        public abstract AttachedDisk[] Disk { get; set; }

        /// <summary>
        /// <para type="description">
        /// The keys and values of the Metadata of this instance.
        /// </para>
        /// </summary>
        public abstract IDictionary Metadata { get; set; }

        /// <summary>
        /// The name of the network to use. If not specified, it is global/networks/default.
        /// </summary>
        public abstract string Network { get; set; }

        /// <summary>
        /// The region of the subnetwork.
        /// </summary>
        public abstract string Region { get; set; }

        /// <summary>
        /// (Optional) The name of the subnetwork to use.
        /// </summary>
        public abstract string Subnetwork { get; set; }

        /// <summary>
        /// If set, the instance will not have an external ip address.
        /// </summary>
        public abstract SwitchParameter NoExternalIp { get; set; }

        /// <summary>
        /// If set, the instance will be preemptible, and AutomaticRestart will be false.
        /// </summary>
        public abstract SwitchParameter Preemptible { get; set; }

        /// <summary>
        /// If set, the instance will not restart when shut down by Google Compute Engine.
        /// </summary>
        public abstract bool AutomaticRestart { get; set; }

        /// <summary>
        /// If set, the instance will terminate rather than migrate when the host undergoes maintenance.
        /// </summary>
        public abstract SwitchParameter TerminateOnMaintenance { get; set; }

        /// <summary>
        /// The ServiceAccount used to specify access tokens. Use New-GceServiceAccountConfig to build one.
        /// </summary>
        public abstract ServiceAccount[] ServiceAccount { get; set; }

        /// <summary>
        /// A tag of this instance.
        /// </summary>
        public abstract string[] Tag { get; set; }

        /// <summary>
        /// <para type="description">
        /// The map of labels (key/value pairs) to be applied to the instance.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public abstract Hashtable Label { get; set; }

        /// <summary>
        /// Builds a network interface given the Network and NoExternalIp parameters.
        /// </summary>
        /// <returns>
        /// The NetworkInsterface object to use in the instance template description.
        /// </returns>
        protected virtual NetworkInterface BuildNetworkInterfaces()
        {
            var accessConfigs = new List<AccessConfig>();
            if (!NoExternalIp)
            {
                accessConfigs.Add(new AccessConfig
                {
                    Name = "External NAT",
                    Type = "ONE_TO_ONE_NAT"
                });
            }

            string networkUri = Network;
            if (string.IsNullOrEmpty(networkUri))
            {
                networkUri = "default";
            }

            networkUri = ConstructNetworkName(networkUri, Project);

            NetworkInterface result = new NetworkInterface
            {
                Network = networkUri,
                AccessConfigs = accessConfigs
            };

            if (Subnetwork != null)
            {
                if (!Subnetwork.Contains($"regions/{Region}/subnetworks/"))
                {
                    Subnetwork = $"regions/{Region}/subnetworks/{Subnetwork}";
                }
                result.Subnetwork = Subnetwork;
            }

            return result;
        }

        /// <summary>
        /// Creates a list of AttachedDisk objects form Disk, BootDiskImage, and ExtraDis.
        /// </summary>
        /// <returns>
        /// A list of AttachedDisk objects to be used in the instance template description.
        /// </returns>
        protected virtual IList<AttachedDisk> BuildAttachedDisks()
        {
            var disks = new List<AttachedDisk>();
            if (Disk != null)
            {
                disks.AddRange(Disk);
            }

            if (BootDiskImage != null)
            {
                disks.Add(new AttachedDisk
                {
                    Boot = true,
                    AutoDelete = true,
                    InitializeParams = new AttachedDiskInitializeParams { SourceImage = BootDiskImage.SelfLink }
                });
            }

            if (ExtraDisk != null)
            {
                foreach (Disk disk in ExtraDisk)
                {
                    disks.Add(new AttachedDisk
                    {
                        Source = disk.SelfLink,
                        Mode = "READ_ONLY"
                    });
                }
            }

            return disks;
        }

        /// <summary>
        /// Creates a machine type string. Most of the logic is for the
        /// custom machine case where -CustomMemory or -CustomCpu is used.
        /// If nothing is supplied, use "n1-standard-1" as default.
        /// </summary>
        /// <returns></returns>
        protected string BuildMachineType()
        {
            if (!string.IsNullOrWhiteSpace(MachineType))
            {
                return MachineType;
            }

            if (CustomCpu < 1 || (CustomCpu > 1 && CustomCpu % 2 != 0))
            {
                throw new PSArgumentException("Number of custom vCPUs must be positive and be either 1 or even.");
            }

            if (CustomMemory < 0 || CustomMemory % 256 != 0)
            {
                throw new PSArgumentException("Total custom memory must be positive and a multiple of 256.");
            }

            double memoryPerCpu = (double)CustomMemory/(double)CustomCpu;

            if (memoryPerCpu < 6.5 * AmountOfMbInGb)
            {
                return $"custom-{CustomCpu}-{CustomMemory}";
            }
            else
            {
                return $"custom-{CustomCpu}-{CustomMemory}-ext";
            }
        }

        /// <summary>
        /// Builds an InstanceTemplate from parameter values.
        /// </summary>
        /// <returns>
        /// An InstanceTemplate to be sent to Google Compute Engine as part of a insert instance template
        /// request.
        /// </returns>
        protected InstanceTemplate BuildInstanceTemplate()
        {
            return new InstanceTemplate
            {
                Name = Name,
                Description = Description,
                Properties = new InstanceProperties
                {
                    CanIpForward = CanIpForward,
                    Description = Description,
                    Disks = BuildAttachedDisks(),
                    MachineType = BuildMachineType(),
                    Metadata = InstanceMetadataPSConverter.BuildMetadata(Metadata),
                    NetworkInterfaces = new List<NetworkInterface> { BuildNetworkInterfaces() },
                    Scheduling = new Scheduling
                    {
                        AutomaticRestart = AutomaticRestart && !Preemptible,
                        Preemptible = Preemptible,
                        OnHostMaintenance = TerminateOnMaintenance ? "TERMINATE" : "MIGRATE"
                    },
                    ServiceAccounts = ServiceAccount,
                    Tags = new Tags
                    {
                        Items = Tag
                    },
                    Labels = ConvertToDictionary<string, string>(Label)
                }
            };
        }

        private static readonly int AmountOfMbInGb = 1024;
    }

    /// <summary>
    /// Base cmdlet class indicating what parameters are needed to describe an instance. Used by
    /// NewGceInstanceConfigCmdlet and AdGceInstanceCmdlet to provide a unified way to build an instance
    /// description.
    /// </summary>
    public abstract class GceInstanceDescriptionCmdlet : GceTemplateDescriptionCmdlet
    {
        /// <summary>
        /// The persistant disk to use as a boot disk. Use Get-GceDisk to get one of these.
        /// </summary>
        public abstract Disk BootDisk { get; set; }

        /// <summary>
        /// <para type="description">
        /// The static ip address this instance will have.
        /// </para>
        /// </summary>
        public abstract string Address { get; set; }

        /// <summary>
        /// Extend the parent BuildAttachedDisks by optionally appending a disk from the BootDisk attribute.
        /// </summary>
        /// <returns>
        /// A list of AttachedDisk objects to be used in the instance description.
        /// </returns>
        protected override IList<AttachedDisk> BuildAttachedDisks()
        {
            IList<AttachedDisk> disks = base.BuildAttachedDisks();

            if (BootDisk != null)
            {
                disks.Add(new AttachedDisk
                {
                    Boot = true,
                    AutoDelete = false,
                    Source = BootDisk.SelfLink
                });
            }
            return disks;
        }

        /// <summary>
        /// Extends the parent BuildnetworkInterfaces by adding the static address to the network interface.
        /// </summary>
        /// <returns>
        /// The NetworkInsterface object to use in the instance description.
        /// </returns>
        protected override NetworkInterface BuildNetworkInterfaces()
        {
            NetworkInterface networkInterface = base.BuildNetworkInterfaces();
            networkInterface.NetworkIP = Address;
            return networkInterface;
        }

        /// <summary>
        /// Builds the instance description based on the cmdlet parameters.
        /// </summary>
        /// <returns>
        /// An Instance object to be sent to Google Compute Engine as part of an insert instance request.
        /// </returns>
        protected Instance BuildInstance()
        {
            return new Instance
            {
                Name = Name,
                CanIpForward = CanIpForward,
                Description = Description,
                Disks = BuildAttachedDisks(),
                MachineType = BuildMachineType(),
                Metadata = InstanceMetadataPSConverter.BuildMetadata(Metadata),
                NetworkInterfaces = new List<NetworkInterface> { BuildNetworkInterfaces() },
                Scheduling = new Scheduling
                {
                    AutomaticRestart = AutomaticRestart && !Preemptible,
                    Preemptible = Preemptible,
                    OnHostMaintenance = TerminateOnMaintenance ? "TERMINATE" : "MIGRATE"
                },
                ServiceAccounts = ServiceAccount,
                Tags = new Tags
                {
                    Items = Tag
                },
                Labels = ConvertToDictionary<string, string>(Label)
            };
        }
    }
}
