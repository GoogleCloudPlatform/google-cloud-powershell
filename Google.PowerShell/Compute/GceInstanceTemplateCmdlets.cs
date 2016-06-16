// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management.Automation;
using System.Threading;

namespace Google.PowerShell.ComputeEngine
{
    [Cmdlet(VerbsCommon.Get, "GceInstanceTemplate", DefaultParameterSetName = ParameterSetNames.Default)]
    public class GetGceInstanceTemplateCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string Default = "default";
            public const string ByName = "byName";
            public const string ByObject = "byObject";
        }

        [Parameter(ParameterSetName = ParameterSetNames.Default)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigDefault("project")]
        public string Project { get; set; }

        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.ByName)]
        public string Name { get; set; }

        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.ByObject)]
        public InstanceTemplate Template { get; set; }

        private readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();

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
                    throw new PSInvalidOperationException($"{ParameterSetName} is not a valid Parameter Set.");
            }
        }

        private InstanceTemplate GetTemplateByObject()
        {
            var project = GetProjectNameFromUri(Template.SelfLink);
            var name = Template.Name;
            return Service.InstanceTemplates.Get(project, name).ExecuteAsync(_cancellationSource.Token).Result;
        }

        private InstanceTemplate GetTemplateByName()
        {
            return Service.InstanceTemplates.Get(Project, Name).ExecuteAsync(_cancellationSource.Token).Result;
        }

        private IEnumerable<InstanceTemplate> GetProjectTemplates()
        {
            InstanceTemplatesResource.ListRequest request = Service.InstanceTemplates.List(Project);
            do
            {
                InstanceTemplateList result = request.ExecuteAsync(_cancellationSource.Token).Result;
                foreach (InstanceTemplate template in result.Items)
                {
                    yield return template;
                }
                request.PageToken = result.NextPageToken;
            } while (request.PageToken != null);
        }

        protected override void StopProcessing()
        {
            _cancellationSource.Cancel();
        }
    }

    [Cmdlet(VerbsCommon.Add, "GceInstanceTemplate")]
    public class AddGceInstanceTemplateCmdlet : GceConcurrentCmdlet
    {
        private struct ParameterSetNames
        {
            public const string FromObject = "FromObject";
            public const string ByValues = "ByValues";
        }

        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.FromObject)]
        public InstanceTemplate Template { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [ConfigDefault("project")]
        public string Project { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByValues)]
        public string Name { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public SwitchParameter CanIpForward { get; set; }


        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public string Description { get; set; }


        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public List<string> DiskImage { get; set; }


        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public string MachineType { get; set; }


        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public IDictionary Metadata { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public List<string> Network { get; set; }

        protected override void ProcessRecord()
        {
            InstanceTemplate instanceTemplate;
            switch (ParameterSetName)
            {
                case ParameterSetNames.FromObject:
                    instanceTemplate = Template;
                    break;
                case ParameterSetNames.ByValues:
                    instanceTemplate = BuildNewTemplate();
                    break;
                default:
                    throw new PSInvalidOperationException($"{ParameterSetName} is not a valid parameter set");
            }
            Service.InstanceTemplates.Insert(instanceTemplate, Project);
        }

        private InstanceTemplate BuildNewTemplate()
        {
            var newTemplate = new InstanceTemplate();
            newTemplate.Name = Name;
            var properties = new InstanceProperties();
            properties.CanIpForward = CanIpForward;
            properties.Description = Description;
            properties.Disks = BuildNewDisks();
            properties.MachineType = MachineType;
            properties.Metadata = BuildMetadata();
            properties.NetworkInterfaces = BuildNetworkInterfaces();

            newTemplate.Properties = properties;


            return newTemplate;
        }

        private IList<NetworkInterface> BuildNetworkInterfaces()
        {
            var interfaces = new List<NetworkInterface>();
            if (Network == null || Network.Count == 0)
            {
                interfaces.Add(new NetworkInterface
                {
                    Network = "global/networks/default"
                });
            }
            else
            {
                foreach (var networkName in Network)
                {
                    string networkUri = networkName;
                    if (!networkUri.Contains("/networks/"))
                    {
                        networkUri = $"projects/{Project}/global/networks/{networkName}";
                    }
                    interfaces.Add(new NetworkInterface
                    {
                        Network = networkUri
                    });
                }
            }
            return interfaces;
        }

        private Metadata BuildMetadata()
        {
            return InstanceMetadataPSConverter.BuildMetadata(Metadata);
        }


        private IList<AttachedDisk> BuildNewDisks()
        {
            bool boot = true;
            var newDisks = new List<AttachedDisk>();
            foreach (var imageName in DiskImage)
            {
                newDisks.Add(new AttachedDisk
                {
                    Boot = boot,
                    AutoDelete = true,
                    InitializeParams = new AttachedDiskInitializeParams { SourceImage = imageName }
                });
                boot = false;
            }
            return newDisks;
        }
    }
}
