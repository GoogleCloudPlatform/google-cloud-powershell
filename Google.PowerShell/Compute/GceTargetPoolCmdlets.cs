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
    /// Gets Google Compute Engine target pools.
    /// </para>
    /// <para type="description">
    /// This command lists target pools of a project, or gets a specific one.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GceTargetPool</code>
    ///   <para>This command lists all target pools for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceTargetPool -Region us-central1</code>
    ///   <para>This command lists all target pools in region "us-central1" for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceTargetPool "my-target-pool"</code>
    ///   <para>This command gets the target pool named "my-target-pool" in the default project and zone</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/targetPools#resource)">
    /// [Target Pool resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceTargetPool", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(TargetPool))]
    public class GetGceTargetPoolCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string OfRegion = "OfRegion";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// The project the target pools belong to. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfProject)]
        [Parameter(ParameterSetName = ParameterSetNames.OfRegion)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The region of the forwaring rule to get. Defaults to the region in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfRegion, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the target pool to get.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        // TODO(jimwp): Understand and add health status check.

        protected override void ProcessRecord()
        {
            IEnumerable<TargetPool> pools;
            switch (ParameterSetName)
            {
                case ParameterSetNames.OfProject:
                    pools = GetAllProjectTargetPools(Project);
                    break;
                case ParameterSetNames.OfRegion:
                    pools = GetRegionTargetPools(Project, Region);
                    break;
                case ParameterSetNames.ByName:
                    pools = new[] { Service.TargetPools.Get(Project, Region, Name).Execute() };
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            WriteObject(pools, true);
        }

        private IEnumerable<HealthStatus> GetPoolHealth(IEnumerable<TargetPool> pools)
        {
            foreach (TargetPool targetPool in pools)
            {
                foreach (string instanceUrl in targetPool.Instances ?? Enumerable.Empty<string>())
                {
                    InstanceReference body = new InstanceReference { Instance = instanceUrl };
                    string project = GetProjectNameFromUri(targetPool.SelfLink);
                    string region = GetRegionNameFromUri(targetPool.Region);
                    TargetPoolsResource.GetHealthRequest request =
                        Service.TargetPools.GetHealth(body, project, region, targetPool.Name);
                    TargetPoolInstanceHealth response = request.Execute();
                    var statuses = response.HealthStatus ?? Enumerable.Empty<HealthStatus>();
                    foreach (HealthStatus healthStatus in statuses)
                    {
                        yield return healthStatus;
                    }
                }
            }
        }

        private IEnumerable<TargetPool> GetRegionTargetPools(string project, string region)
        {
            TargetPoolsResource.ListRequest request = Service.TargetPools.List(project, region);
            do
            {
                TargetPoolList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (TargetPool targetPool in response.Items)
                    {
                        yield return targetPool;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }


        private IEnumerable<TargetPool> GetAllProjectTargetPools(string project)
        {
            TargetPoolsResource.AggregatedListRequest request =
                Service.TargetPools.AggregatedList(project);
            do
            {
                TargetPoolAggregatedList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (KeyValuePair<string, TargetPoolsScopedList> kvp in response.Items)
                    {
                        if (kvp.Value?.TargetPools != null)
                        {
                            foreach (TargetPool targetPool in kvp.Value.TargetPools)
                            {
                                yield return targetPool;
                            }
                        }
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Sets data about Google Compute Engine target pools.
    /// </para>
    /// <para type="description">
    /// Set-GceTargetPool adds and removes instance to and from target pools
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> $instance = Get-GceInstance "my-instance"
    ///   PS C:\> Get-GceTargetPool "my-pool" | Set-GceTargetPool -AddInstance $instance
    ///   </code>
    ///   <para>This command adds instance "my-instance" to the target pool "my-pool"</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GceTargetPool "my-pool" -RemoveInstance $instanceUrl
    ///   </code>
    ///   <para>This command removes the instance pointed to by $instanceUrl from target pool "my-pool".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/targetPools#resource)">
    /// [Target Pool resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GceTargetPool")]
    [OutputType(typeof(TargetPool))]
    public class SetGceTargetPoolCmdlet : GceConcurrentCmdlet
    {

        private class ParameterSetNames
        {
            public const string AddInstanceByName = "AddInstanceByName";
            public const string AddInstanceByObject = "AddInstanceByObject";
            public const string RemoveInstanceByName = "RemoveInstanceByName";
            public const string RemoveInstanceByObject = "RemoveInstanceByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project the target pool belongs to. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.RemoveInstanceByName)]
        [Parameter(ParameterSetName = ParameterSetNames.AddInstanceByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The region of the target pool. Defaults to the region in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.RemoveInstanceByName)]
        [Parameter(ParameterSetName = ParameterSetNames.AddInstanceByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the target pool to change.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.RemoveInstanceByName, Mandatory = true, Position = 0)]
        [Parameter(ParameterSetName = ParameterSetNames.AddInstanceByName, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The target pool object to change.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.RemoveInstanceByObject, Mandatory = true,
            ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.AddInstanceByObject, Mandatory = true,
            ValueFromPipeline = true)]
        public TargetPool InputObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// A list of instance to add to the target pool. Can take either string urls or
        /// Google.Apis.Compute.v1.Data.Instance objects.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AddInstanceByName, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.AddInstanceByObject, Mandatory = true)]
        [PropertyByTypeTransformation(Property = nameof(Instance.SelfLink), TypeToTransform = typeof(Instance))]
        public string[] AddInstance { get; set; }

        /// <summary>
        /// <para type="description">
        /// A list of instance to remove from the target pool. Can take either string urls or
        /// Google.Apis.Compute.v1.Data.Instance objects.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.RemoveInstanceByName, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.RemoveInstanceByObject, Mandatory = true)]
        [PropertyByTypeTransformation(Property = nameof(Instance.SelfLink), TypeToTransform = typeof(Instance))]
        public string[] RemoveInstance { get; set; }

        protected override void ProcessRecord()
        {
            string project;
            string region;
            string name;
            switch (ParameterSetName)
            {
                case ParameterSetNames.AddInstanceByName:
                case ParameterSetNames.RemoveInstanceByName:
                    project = Project;
                    region = Region;
                    name = Name;
                    break;
                case ParameterSetNames.AddInstanceByObject:
                case ParameterSetNames.RemoveInstanceByObject:
                    project = GetProjectNameFromUri(InputObject.SelfLink);
                    region = GetRegionNameFromUri(InputObject.Region);
                    name = InputObject.Name;
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            switch (ParameterSetName)
            {
                case ParameterSetNames.AddInstanceByName:
                case ParameterSetNames.AddInstanceByObject:
                    AddInstanceToPool(project, region, name);
                    break;
                case ParameterSetNames.RemoveInstanceByName:
                case ParameterSetNames.RemoveInstanceByObject:
                    RemoveInstanceFromPool(project, region, name);
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private void AddInstanceToPool(string project, string region, string name)
        {
            TargetPoolsAddInstanceRequest addBody = new TargetPoolsAddInstanceRequest
            {
                Instances = AddInstance.Select(i => new InstanceReference { Instance = i }).ToList()
            };
            AddRegionOperation(project, region,
                Service.TargetPools.AddInstance(addBody, project, region, name).Execute(),
                () => WriteObject(Service.TargetPools.Get(project, region, name).Execute()));
        }

        private void RemoveInstanceFromPool(string project, string region, string name)
        {
            TargetPoolsRemoveInstanceRequest removeBody = new TargetPoolsRemoveInstanceRequest
            {
                Instances = RemoveInstance.Select(i => new InstanceReference { Instance = i }).ToList()
            };
            AddRegionOperation(project, region,
                Service.TargetPools.RemoveInstance(removeBody, project, region, name).Execute(),
                () => WriteObject(Service.TargetPools.Get(project, region, name).Execute()));
        }
    }
}
