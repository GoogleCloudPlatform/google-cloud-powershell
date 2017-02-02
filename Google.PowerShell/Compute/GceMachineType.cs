// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using Google.PowerShell.ComputeEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace Google.PowerShell.Compute
{
    /// <summary>
    /// <para type="synopsis">
    /// Get Google Compute Engine machine types.
    /// </para>
    /// <para type="description">
    /// Gets all machine types of a project, or all machine types of a project in a zone, or a single machine
    /// type of a project in a zone with a name.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GceMachineType</code>
    ///   <para>Lists all machine types for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceMachineType -Zone "us-central1-a"</code>
    ///   <para>Lists all machine types in zone us-central1-a for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceMachineType "f1-micro"</code>
    ///   <para>Gets the machine type named f1-micro in the default project and zone.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/machineTypes#resource)">
    /// [Machine Type resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceMachineType", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(MachineType))]
    public class GetGceMachineTypeCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string OfZone = "OfZone";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfZone, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.OfProject:
                    WriteObject(GetAllProjectMachineTypes(), true);
                    break;
                case ParameterSetNames.OfZone:
                    WriteObject(GetZoneMachineTypes(), true);
                    break;
                case ParameterSetNames.ByName:
                    WriteObject(Service.MachineTypes.Get(Project, Zone, Name).Execute());
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private IEnumerable<MachineType> GetZoneMachineTypes()
        {
            MachineTypesResource.ListRequest request = Service.MachineTypes.List(Project, Zone);
            do
            {
                MachineTypeList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (MachineType machineType in response.Items)
                    {
                        yield return machineType;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (request.PageToken != null && !Stopping);
        }

        private IEnumerable<MachineType> GetAllProjectMachineTypes()
        {
            MachineTypesResource.AggregatedListRequest request = Service.MachineTypes.AggregatedList(Project);
            do
            {
                MachineTypeAggregatedList response = request.Execute();
                if (response.Items != null)
                {
                    Func<MachineTypesScopedList, IEnumerable<MachineType>> machineTypesOrEmpty =
                        sl => sl.MachineTypes ?? Enumerable.Empty<MachineType>();
                    foreach (MachineType machineType in response.Items.Values.SelectMany(machineTypesOrEmpty))
                    {
                        yield return machineType;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (request.PageToken != null && !Stopping);
        }
    }
}
