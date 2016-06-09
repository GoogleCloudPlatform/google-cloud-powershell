// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Google.Apis.Compute.v1.Data;

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
    public class NewGceInstanceConfigCmdlet : GceCmdlet
    {
        internal class ParameterSetNames
        {
            public const string DiskByObject = "DiskByObject";
            public const string DiskByImage = "DiskByImage";
            public const string DiskBySource = "DiskBySource";
        }

        /// <summary>
        /// <para type="description">
        /// The name of the instance. The name must be 1-63 characters long and
        /// match [a-z]([-a-z0-9]*[a-z0-9])?
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// Allows this instance to send and receive packets with non-matching destination
        /// or source IPs. This is required if you plan to use this instance to forward routes.
        /// </para>
        /// </summary>
        [Parameter]
        public bool? CanIpForward { get; set; }

        /// <summary>
        /// <para type="description">
        /// A description of this resource.
        /// </para>
        /// </summary>
        [Parameter]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// Disks associated with this instance.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DiskByObject)]
        public List<AttachedDisk> Disk { get; set; }

        /// <summary>
        /// <para type="description">
        /// The path to the boot disk image.
        /// For example: "projects/debian-cloud/global/images/debian-8-jessie-v20160511".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DiskByImage)]
        public string DiskImage { get; set; }

        /// <summary>
        /// <para type="description">
        /// The path to the boot disk.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DiskBySource)]
        public string DiskSource { get; set; }

        /// <summary>
        /// <para type="description">
        /// A string describing the machine type. This can be either just the machine type name,
        /// or the full url. For example: n1-standard-4
        /// <para type="description">
        /// </summary>
        [Parameter(Mandatory = true)]
        public string MachineType { get; set; }

        /// <summary>
        /// <para type="description">
        /// The metadata key/value pairs assigned to this instance. This includes custom
        /// metadata and predefined keys.
        /// </para>
        /// </summary>
        [Parameter]
        public Hashtable Metadata { get; set; }

        /// <summary>
        /// <para type="description">
        /// An array of configurations for this interface. This specifies how this interface
        /// is configured to interact with other network services, such as connecting to
        /// the internet.
        /// </para>
        /// </summary>
        [Parameter]
        public List<NetworkInterface> NetworkInterface { get; set; }

        /// <summary>
        /// <para type="description">
        /// Scheduling options for this instance.
        /// </para>
        /// </summary>
        [Parameter]
        public Scheduling Scheduling { get; set; }

        /// <summary>
        /// <para type="description">
        /// A list of service accounts, with their specified scopes, authorized for this
        /// instance. Service accounts generate access tokens that can be accessed through
        /// the metadata server and used to authenticate applications on the instance. See
        /// Authenticating from Google Compute Engine for more information.
        /// </para>
        /// </summary>
        [Parameter]
        public List<ServiceAccount> ServiceAccount { get; set; }

        /// <summary>
        /// <para type="description">
        /// A list of tags to apply to this instance. Tags are used to identify valid sources
        /// or targets for network firewalls. Each tag within
        /// the list must comply with RFC1035.
        /// </para>
        /// </summary>
        [Parameter]
        public List<string> Tag { get; set; }

        protected override void ProcessRecord()
        {
            Instance newInstance = ProduceInstance();

            WriteObject(newInstance);
        }

        private Instance ProduceInstance()
        {
            Instance newInstance = new Instance
            {
                Name = Name,
                CanIpForward = CanIpForward,
                Description = Description,
                MachineType = MachineType,
                Scheduling = Scheduling,
                ServiceAccounts = ServiceAccount,
            };

            if (NetworkInterface != null)
            {
                newInstance.NetworkInterfaces = NetworkInterface;
            }
            else
            {
                newInstance.NetworkInterfaces = new List<NetworkInterface> {
                    new NetworkInterface {
                        Network = "global/networks/default",
                        AccessConfigs = new List<AccessConfig> {
                            new AccessConfig {
                                Type = "ONE_TO_ONE_NAT"
                            }
                        }
                    }
                };
            }

            newInstance.Disks = GetDisk();

            if (Metadata != null)
            {
                newInstance.Metadata = GetMetadata();
            }

            if (Tag != null)
            {
                newInstance.Tags = new Tags { Items = Tag.ToList() };
            }

            return newInstance;
        }

        /// <summary>
        /// Creates a metadata object from the Metadata Hashtable
        /// </summary>
        /// <returns>
        /// The Metadata object.
        /// </returns>
        private Metadata GetMetadata()
        {
            IList<Metadata.ItemsData> items = new List<Metadata.ItemsData>();
            for (var e = Metadata.GetEnumerator(); e.MoveNext();)
            {
                items.Add(new Metadata.ItemsData { Key = e.Key.ToString(), Value = e.Value.ToString() });
            }
            return new Metadata { Items = items };
        }

        /// <summary>
        /// Creates the attached disks based on various parameters. The parameter set "DiskByObject" will
        /// simply return the parameter Disks. The parameter sets "DiskByImage" and "DiskBySource" will
        /// generate a new list of attached disks that reflect these parameters.
        /// </summary>
        /// <returns>
        /// The attached disks of the image.
        /// </returns>
        private IList<AttachedDisk> GetDisk()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.DiskByObject:
                    return Disk.ToList();

                case ParameterSetNames.DiskByImage:
                    return new List<AttachedDisk> {
                        new AttachedDisk {
                            Boot = true,
                            AutoDelete = true,
                            InitializeParams = new AttachedDiskInitializeParams { SourceImage = DiskImage }
                        }
                    };

                case ParameterSetNames.DiskBySource:
                    return new List<AttachedDisk> {
                        new AttachedDisk {
                            Boot = true,
                            AutoDelete = false,
                            Source = DiskSource
                        }
                    };

                default:
                    throw new InvalidOperationException($"{ParameterSetName} is not a valid ParameterSet.");

            }
        }
    }
}
