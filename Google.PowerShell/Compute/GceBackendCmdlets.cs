// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    [Cmdlet(VerbsCommon.Add, "GceBackendService")]
    [OutputType(typeof(BackendService))]
    public class AddGceBackendServiceCmdlet : GceCmdlet
    {
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = true, Position = 0)]
        public string Name { get; set; }

        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        public Backend[] Backend { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        [PropertyByTypeTransformation(Property = nameof(HttpHealthCheck.SelfLink),
            TypeToTransform = typeof(HttpHealthCheck)
            )]
        [PropertyByTypeTransformation(Property = nameof(HttpsHealthCheck.SelfLink),
            TypeToTransform = typeof(HttpsHealthCheck))]
        public string HealthCheck { get; set; }

        [Parameter]
        public string PortName { get; set; } = "http";

        [Parameter]
        public string Description { get; set; }

        public enum BackendProtocol
        {
            HTTP,
            HTTPS,
            HTTP2,
            TCP,
            SSL
        }

        [Parameter]
        public BackendProtocol Protocol { get; set; } = BackendProtocol.HTTP;

        [Parameter]
        public TimeSpan? Timeout { get; set; }

        private List<Backend> allBackends = new List<Backend>();

        protected override void ProcessRecord()
        {
            allBackends.AddRange(Backend);
        }

        protected override void EndProcessing()
        {

            BackendService body = new BackendService()
            {
                Name = Name,
                Backends = allBackends,
                Description = Description,
                HealthChecks = new[] { HealthCheck },
                PortName = PortName,
                Protocol = Protocol.ToString(),
                TimeoutSec = (int?)Timeout?.TotalSeconds
            };
            Operation operation = Service.BackendServices.Insert(body, Project).Execute();
            WaitForGlobalOperation(Project, operation);
            WriteObject(Service.BackendServices.Get(Project, body.Name).Execute());
        }
    }

    [Cmdlet(VerbsCommon.New, "GceBackend")]
    [OutputType(typeof(Backend))]
    public class NewGceBackendCmdlet : GceCmdlet
    {
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(InstanceGroupManager),
            Property = nameof(InstanceGroupManager.SelfLink))]
        [PropertyByTypeTransformation(TypeToTransform = typeof(InstanceGroup),
            Property = nameof(Google.Apis.Compute.v1.Data.InstanceGroup.SelfLink))]
        public string InstanceGroup { get; set; }

        public float? MaxUtilization { get; set; }


        public float? MaxRatePerInstance { get; set; }

        public int? MaxRate { get; set; }

        public float? CapacityScaler { get; set; }

        public string Description { get; set; }

        [Alias("Rate")]
        public SwitchParameter RateBalance { get; set; }

        protected override void ProcessRecord()
        {
            var backend = new Backend
            {
                BalancingMode = RateBalance ? "RATE" : "UTILIZATION",
                Description = Description,
                CapacityScaler = CapacityScaler,
                MaxRate = MaxRate,
                MaxRatePerInstance = MaxRatePerInstance,
                Group = InstanceGroup,
                MaxUtilization = MaxUtilization
            };

            WriteObject(backend);
        }
    }
}
