// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets information about a Google Compute Engine address.
    /// </para>
    /// <para type="description">
    /// Get an object that has information about an address.
    /// </para>
    /// <example>
    /// <code>Get-GceAddress</code>
    /// <para>
    /// List all global and region addresses.
    /// </para>
    /// </example>
    /// <example>
    /// <code>Get-GceAddress $addressName</code>
    /// <para>
    /// Get a named addresses of the region of the current gcloud config.
    /// </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/addresses#resource)">
    /// [Address resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceAddress", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(Address))]
    public class GetGceAddressCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string OfRegion = "OfRegion";
            public const string ByName = "ByName";
            public const string Global = "Global";
            public const string GlobalByName = "GlobalByName";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the address. Required if not specified by the gcloud config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfProject)]
        [Parameter(ParameterSetName = ParameterSetNames.OfRegion)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [Parameter(ParameterSetName = ParameterSetNames.Global)]
        [Parameter(ParameterSetName = ParameterSetNames.GlobalByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The region the address is in. Requried when listing addresses of a region.
        /// Defaults to gcloud config region when getting a non-global named address.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfRegion, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the address to get.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.GlobalByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will get global addresses, rather than region specific ones.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Global, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.GlobalByName, Mandatory = true)]
        public SwitchParameter Global { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.OfProject:
                    WriteObject(GetProjectAddresses(), true);
                    break;
                case ParameterSetNames.OfRegion:
                    WriteObject(GetRegionAddresses(), true);
                    break;
                case ParameterSetNames.ByName:
                    WriteObject(GetAddressByName());
                    break;
                case ParameterSetNames.Global:
                    WriteObject(GetGlobalProjectAddresses(), true);
                    break;
                case ParameterSetNames.GlobalByName:
                    WriteObject(GetGlobalAddressByName());
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private Address GetGlobalAddressByName()
        {
            return Service.GlobalAddresses.Get(Project, Name).Execute();
        }

        private IEnumerable<Address> GetGlobalProjectAddresses()
        {
            GlobalAddressesResource.ListRequest request = Service.GlobalAddresses.List(Project);
            do
            {
                AddressList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (Address address in response.Items)
                    {
                        yield return address;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }

        private Address GetAddressByName()
        {
            return Service.Addresses.Get(Project, Region, Name).Execute();
        }

        private IEnumerable<Address> GetRegionAddresses()
        {
            AddressesResource.ListRequest request = Service.Addresses.List(Project, Region);
            do
            {
                AddressList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (Address address in response.Items)
                    {
                        yield return address;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }

        private IEnumerable<Address> GetProjectAddresses()
        {
            AddressesResource.AggregatedListRequest request = Service.Addresses.AggregatedList(Project);
            do
            {
                AddressAggregatedList response = request.Execute();
                if (response.Items != null)
                {
                    IEnumerable<AddressesScopedList> populated =
                        response.Items.Values.Where(sl => sl.Addresses != null);
                    foreach (Address address in populated.SelectMany(sl => sl.Addresses))
                    {
                        yield return address;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Adds a new address.
    /// </para>
    /// <para type="description">
    /// Adds a new static external IP address to Google Compute Engine.
    /// </para>
    /// <example>
    /// <code>Add-GceAddress $addressName</code>
    /// <para>
    /// Adds an address to the default project and region:
    /// </para>
    /// </example>
    /// <example>
    /// <code>Add-GceAddress $addressName -Global</code>
    /// <para>Adds a global address to the default project.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/addresses#resource)">
    /// [Address resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceAddress", DefaultParameterSetName = ParameterSetNames.ByValues)]
    [OutputType(typeof(Address))]
    public class AddGceAddressCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByObject = "ByObject";
            public const string ByValues = "ByValues";
            public const string GlobalByObject = "GlobalByObject";
            public const string GlobalByValues = "GlobalByValues";
        }

        /// <summary>
        /// <para type="description">
        /// The project that will own the address. Will default to the gcloud config.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The region the address will be in. For non-global addresses, will default to the gcloud config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject)]
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Google.Apis.Compute.v1.Data.Address object with the information used to create an address.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.GlobalByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Address Object { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the address to create. Must comply with RFC1035.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.GlobalByValues, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// Human readable description of the address.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues, Position = 1)]
        [Parameter(ParameterSetName = ParameterSetNames.GlobalByValues, Position = 1)]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will create a global address, rather than a region specific one.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.GlobalByValues, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.GlobalByObject, Mandatory = true)]
        public SwitchParameter Global { get; set; }

        protected override void ProcessRecord()
        {
            Address address;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByObject:
                case ParameterSetNames.GlobalByObject:
                    address = Object;
                    break;
                case ParameterSetNames.ByValues:
                case ParameterSetNames.GlobalByValues:
                    address = new Address
                    {
                        Name = Name,
                        Description = Description
                    };
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            if (Global)
            {
                Operation operation = Service.GlobalAddresses.Insert(address, Project).Execute();
                AddGlobalOperation(Project, operation, () =>
                {
                    WriteObject(Service.GlobalAddresses.Get(Project, Name));
                });
            }
            else
            {
                Operation operation = Service.Addresses.Insert(address, Project, Region).Execute();
                AddRegionOperation(Project, Region, operation, () =>
                {
                    WriteObject(Service.Addresses.Get(Project, Region, Name));
                });
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes a Google Compute Engine address.
    /// </para>
    /// <para type="description">
    /// Removes a Google Compute Engine static external IP address.
    /// </para>
    /// <example>
    /// <code>Remove-GceAddress $addressName</code>
    /// <para>Removes an address of the default project and region</para>
    /// </example>
    /// <example>
    /// <code>Remove-GceAddress $addressName -Global</code>
    /// <para>
    /// Removes a global address of the default project.
    /// </para>
    /// </example>
    /// <example>
    /// <code>Get-GceAddress | Remove-GceAddress</code>
    /// <para>
    /// Removes all global and region specific addresses of the default project.
    /// </para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceAddress", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.Default)]
    public class RemoveGceAddressCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string Default = "Default";
            public const string Global = "Global";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the address. Defaults to the gcloud config project.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The region the address is in. Defaults to the gcloud config region.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Default)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the address to delete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Default, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.Global, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will delete a global address, rather than a region specific one.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Global, Mandatory = true)]
        public SwitchParameter Global { get; set; }

        /// <summary>
        /// <para type="description">
        /// The address object to delete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Address Object { get; set; }

        protected override void ProcessRecord()
        {
            string addressName;
            bool global;
            string region;
            if (ParameterSetName == ParameterSetNames.ByObject)
            {
                addressName = Object.Name;
                global = (Object.Region == null);
                region = global ? null : GetUriPart("regions", Object.Region);
            }
            else
            {
                addressName = Name;
                global = Global;
                region = Region;
            }

            if (global)
            {
                if (ShouldProcess($"{Project}/{addressName}", "Remove-GceAddress -Global"))
                {
                    Operation operation = Service.GlobalAddresses.Delete(Project, addressName).Execute();
                    AddGlobalOperation(Project, operation);
                }
            }
            else
            {
                if (ShouldProcess($"{Project}/{region}/{addressName}", "Remove-GceAddress"))
                {
                    Operation operation = Service.Addresses.Delete(Project, region, addressName).Execute();
                    AddRegionOperation(Project, region, operation);
                }
            }
        }
    }
}
