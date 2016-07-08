// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Compute Engine instance templates.
    /// </para>
    /// <para type="description"> 
    /// Gets Google Compute Engine instance templates.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceInstanceTemplate", DefaultParameterSetName = ParameterSetNames.Default)]
    public class GetGceInstanceTemplateCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string Default = "default";
            public const string ByName = "byName";
            public const string ByObject = "byObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the template.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Default)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the tempate to get.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.ByName)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// A template object. It must have valid SelfLink and Name attributes.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.ByObject)]
        public InstanceTemplate Object { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.Default:
                    WriteObject(GetProjectTemplates(), true);
                    break;
                case ParameterSetNames.ByName:
                    WriteObject(GetTemplateByName());
                    break;
                case ParameterSetNames.ByObject:
                    WriteObject(GetTemplateByObject());
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        /// <summary>
        /// Pulls information from an InstanceTemplate object to get a new version
        /// </summary>
        /// <returns>
        /// The version of the object on the Google Cloud service.
        /// </returns>
        private InstanceTemplate GetTemplateByObject()
        {
            string project = GetProjectNameFromUri(Object.SelfLink);
            string name = Object.Name;
            return Service.InstanceTemplates.Get(project, name).Execute();
        }

        /// <summary>
        /// Gets an InstanceTemplate by project and name.
        /// </summary>
        /// <returns>
        /// A single InstanceTemplate.
        /// </returns>
        private InstanceTemplate GetTemplateByName()
        {
            return Service.InstanceTemplates.Get(Project, Name).Execute();
        }

        /// <summary>
        /// Gets a list of InstanceTemplates for a project.
        /// </summary>
        /// <returns>
        /// The InstanceTemplates of a project.
        /// </returns>
        private IEnumerable<InstanceTemplate> GetProjectTemplates()
        {
            InstanceTemplatesResource.ListRequest request = Service.InstanceTemplates.List(Project);
            do
            {
                InstanceTemplateList result = request.Execute();
                if (result.Items != null)
                {
                    foreach (InstanceTemplate template in result.Items)
                    {
                        yield return template;
                    }
                }
                request.PageToken = result.NextPageToken;
            } while (request.PageToken != null && !Stopping);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Adds an instance template to Google Compute Engine.
    /// </para>
    /// <para type="description"> 
    /// Adds an instance template to Google Compute Engine.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceInstanceTemplate", DefaultParameterSetName = ParameterSetNames.ByValues)]
    public class AddGceInstanceTemplateCmdlet : GceInstanceDescriptionCmdletBase
    {
        private struct ParameterSetNames
        {
            public const string FromObject = "FromObject";
            public const string ByValues = "ByValues";
        }

        /// <summary>
        /// <para type="description">
        /// The project that will own the instance template.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// An instance template object to add to Google Compute Engine.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.FromObject)]
        public InstanceTemplate Object { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the new instance template.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.ByValues)]
        public override string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the machine type for this template.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = ParameterSetNames.ByValues)]
        public override string MachineType { get; set; }

        /// <summary>
        /// <para type="description">
        /// Enables instances to send and receive packets for IP addresses other than their own. Switch on if
        /// these instances will be used as an IP gateway or it will be set as the next-hop in a Route
        /// resource.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override SwitchParameter CanIpForward { get; set; }

        /// <summary>
        /// <para type="description">
        /// Human readable description of this instance template.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The the image used to create the boot disk. Use Get-GceImage to get one of these.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override Image BootDiskImage { get; set; }


        /// <summary>
        /// <para type="description">
        /// Name of existing disk to attach. All instances of this template will be able to
        /// read this disk. Will attach in read only mode.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override string[] ExtraDiskName { get; set; }

        /// <summary>
        /// <para type="description">
        /// An AttachedDisk object specifying a disk to attach. Do not specify `-BootDiskImage` or
        /// `-BootDiskSnapshot` if this is a boot disk. You can build one using New-GceAttachedDiskConfig.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override AttachedDisk[] Disk { get; set; }

        /// <summary>
        /// <para type="description">
        /// The keys and values of the Metadata of this instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override IDictionary Metadata { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the network to use. If not specified, it is global/networks/default. This can be a
        /// string, or Network object you get from Get-GceNetwork.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [PropertyByTypeTransformation(Property = nameof(DataType.network.SelfLink),
            TypeToTransform = typeof(Network))]
        public override string Network { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the instances will not have an external ip address.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override SwitchParameter NoExternalIp { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the instances will be preemptible, and AutomaticRestart will be false.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override SwitchParameter Preemptible { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the instances will not restart when shut down by Google Compute Engine.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override bool AutomaticRestart { get; set; } = true;

        /// <summary>
        /// <para type="description">
        /// If set, the instances will terminate rather than migrate when the host undergoes maintenance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override SwitchParameter TerminateOnMaintenance { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ServiceAccount used to specify access tokens. Use New-GceServiceAccountConfig to build one.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override ServiceAccount[] ServiceAccount { get; set; }

        /// <summary>
        /// <para type="description">
        /// A tag of this instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public override string[] Tag { get; set; }

        /// <summary>
        /// Not used.
        /// </summary>
        protected override Disk BootDisk
        {
            get { return null; }
            set { }
        }

        /// <summary>
        /// Not used.
        /// </summary>
        protected override string Address
        {
            get { return null; }
            set { }
        }

        protected override void ProcessRecord()
        {
            InstanceTemplate instanceTemplate;
            switch (ParameterSetName)
            {
                case ParameterSetNames.FromObject:
                    instanceTemplate = Object;
                    break;
                case ParameterSetNames.ByValues:
                    instanceTemplate = BuildInstanceTemplate();
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            AddGlobalOperation(Project, Service.InstanceTemplates.Insert(instanceTemplate, Project).Execute());
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes a Google Compute Engine instance templates.
    /// </para>
    /// <para type="description"> 
    /// Deletes a Google Compute Engine instance templates.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceInstanceTemplate", SupportsShouldProcess = true,
        DefaultParameterSetName = ParamterSetNames.ByName)]
    public class RemoveGceInstanceTemplateCmdlet : GceConcurrentCmdlet
    {
        private class ParamterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the template.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParamterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the template to delete.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true,
            ParameterSetName = ParamterSetNames.ByName)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The instance tempate object to delete.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true,
            ParameterSetName = ParamterSetNames.ByObject)]
        public InstanceTemplate Object { get; set; }

        protected override void ProcessRecord()
        {
            string project;
            string name;
            switch (ParameterSetName)
            {
                case ParamterSetNames.ByName:
                    project = Project;
                    name = Name;
                    break;
                case ParamterSetNames.ByObject:
                    project = GetProjectNameFromUri(Object.SelfLink);
                    name = Object.Name;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            if (ShouldProcess($"{project}/{name}", "Remove GceInstanceTemplate"))
            {
                AddGlobalOperation(project, Service.InstanceTemplates.Delete(project, name).Execute());
            }
        }
    }
}
