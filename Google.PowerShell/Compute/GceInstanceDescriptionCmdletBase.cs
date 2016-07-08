using Google.Apis.Compute.v1.Data;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// Base cmdlet class indicating what parameters are needed to describe an instance.
    /// </summary>
    public abstract class GceInstanceDescriptionCmdletBase : GceConcurrentCmdlet
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
        /// The persistant disk to use as a boot disk. Use Get-GceDisk to get one of these.
        /// </summary>
        protected abstract Disk BootDisk { get; set; }

        /// <summary>
        /// The the image used to create the boot disk. Use Get-GceImage to get one of these.
        /// </summary>
        public abstract Image BootDiskImage { get; set; }

        /// <summary>
        /// Name of existing disk to attach. It will be attached in read-only mode.
        /// </summary>
        public abstract string[] ExtraDiskName { get; set; }

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
        /// The static ip address this instance will have.
        /// </para>
        /// </summary>
        protected abstract string Address { get; set; }

        protected NetworkInterface BuildNetworkInterfaces()
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

            if (!networkUri.Contains("global/networks"))
            {
                networkUri = $"global/networks/{networkUri}";
            }

            return new NetworkInterface
            {
                Network = networkUri,
                AccessConfigs = accessConfigs,
                NetworkIP = Address
            };
        }

        /// <summary>
        /// Creates a list of AttachedDisk objects form Disk, BootDiskImage, and ExtraDiskName.
        /// </summary>
        protected IList<AttachedDisk> BuildAttachedDisks()
        {
            var disks = new List<AttachedDisk>();
            if (Disk != null)
            {
                disks.AddRange(Disk);
            }

            if (BootDisk != null)
            {
                disks.Add(new AttachedDisk
                {
                    Boot = true,
                    Source = BootDisk.SelfLink
                });
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

            if (ExtraDiskName != null)
            {
                foreach (var diskName in ExtraDiskName)
                {
                    disks.Add(new AttachedDisk
                    {
                        Source = diskName,
                        Mode = "READ_ONLY"
                    });
                }
            }

            return disks;
        }

        protected Instance BuildInstance()
        {
            return new Instance
            {
                Name = Name,
                CanIpForward = CanIpForward,
                Description = Description,
                Disks = BuildAttachedDisks(),
                MachineType = MachineType,
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
                }
            };
        }

        /// <summary>
        /// Builds an InstanceTemplate from parameter values.
        /// </summary>
        /// <returns>
        /// The new instance template to create.
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
                    MachineType = MachineType,
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
                    }
                }
            };
        }
    }
}