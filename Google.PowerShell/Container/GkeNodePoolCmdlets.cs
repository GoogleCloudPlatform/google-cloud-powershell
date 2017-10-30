// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Container.v1;
using Google.Apis.Container.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Text.RegularExpressions;

namespace Google.PowerShell.Container
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Container Engine Node Pools from a Cluster.
    /// </para>
    /// <para type="description">
    /// Gets Google Container Engine Node Pools from a Cluster. If -Project and/or -Zone parameter is not specified,
    /// the default project and/or the default zone will be used. If -NodePoolName parameter is not used,
    /// the cmdlet will return every node pools in the cluster.
    /// You can either supply cluster name with -ClusterName or a Cluster object from
    /// Get-GkeCluster with -ClusterObject. If a Cluster object is used, the cmdlet will use the
    /// Project and Zone from the object.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GkeNodePool -ClusterName "my-cluster"</code>
    ///   <para>Lists all node pools in cluster "my-cluster" in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Get-GkeNodePool -Zone "us-central1-a" -Project "my-project" -ClusterName "my-cluster"
    ///   </code>
    ///   <para>
    ///   Lists all node pools in cluster "my-cluster" in zone us-central1-a of the project "my-project".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Get-GkeNodePool -ClusterName "my-cluster" -NodePoolName "default-1", "default-2"
    ///   </code>
    ///   <para>
    ///   Gets node pools "default-1" and "default-2" in cluster "my-cluster" in the default project.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/container-engine/docs/node-pools)">
    /// [Node Pools]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GkeNodePool")]
    public class GetGkeNodePoolCmdlet : GkeCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByClusterName = "ByClusterName";
            public const string ByClusterObject = "ByClusterObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that the node pool's cluster is in.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByClusterName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the node pool's cluster is in.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByClusterName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the cluster that the node pool belongs to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByClusterName)]
        [ValidateNotNullOrEmpty]
        public string ClusterName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name(s) of the node pool(s) that will be retrieved.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 1)]
        [ValidateNotNullOrEmpty]
        public string[] NodePoolName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the cluster that the node pool belongs to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByClusterObject,
            ValueFromPipeline = true)]
        [ValidateNotNull]
        public Cluster ClusterObject { get; set; }

        protected override void ProcessRecord()
        {
            if (ClusterObject != null)
            {
                Zone = ClusterObject.Zone;
                ClusterName = ClusterObject.Name;
                Project = GetProjectNameFromUri(ClusterObject.SelfLink);
            }

            if (NodePoolName != null)
            {
                WriteObject(GetNodePoolsByName(Project, Zone, ClusterName, NodePoolName), true);
            }
            else
            {
                WriteObject(GetAllNodepools(Project, Zone, ClusterName), true);
            }
        }

        /// <summary>
        /// Returns node pools that have the names in nodePoolNames array in cluster 'clusterName'
        /// of zone 'zone' in project 'project'.
        /// </summary>
        private IEnumerable<NodePool> GetNodePoolsByName(string project, string zone,
            string clusterName, string[] nodePoolNames)
        {
            foreach (string poolName in NodePoolName)
            {
                NodePool result = null;
                try
                {
                    ProjectsResource.ZonesResource.ClustersResource.NodePoolsResource.GetRequest getRequest =
                        Service.Projects.Zones.Clusters.NodePools.Get(Project, Zone, ClusterName, poolName);
                    result = getRequest.Execute();
                }
                catch (GoogleApiException apiEx) when (apiEx.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    WriteResourceMissingError(
                        exceptionMessage: $"Nodepool '{poolName}' cannot be found in cluster '{clusterName}'"
                                        + $" in zone '{zone}' of project '{Project}'.",
                        errorId: "NodePoolNotFound",
                        targetObject: poolName);
                }

                if (result != null)
                {
                    yield return result;
                }
            }
        }

        /// <summary>
        /// Given a cluster in a zone of a project, returns all the node pools of that cluster.
        /// </summary>
        private IEnumerable<NodePool> GetAllNodepools(string project, string zone, string clusterName)
        {
            ProjectsResource.ZonesResource.ClustersResource.NodePoolsResource.ListRequest listRequest =
                Service.Projects.Zones.Clusters.NodePools.List(Project, Zone, clusterName);
            ListNodePoolsResponse response = listRequest.Execute();
            if (response.NodePools != null)
            {
                foreach (NodePool nodePool in response.NodePools)
                {
                    yield return nodePool;
                }
            }
        }
    }

    /// <summary>
    /// Abstract class for cmdlets that needs to create node pool objects.
    /// </summary>
    public abstract class GkeNodePoolConfigCmdlet : GkeNodeConfigCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the node pool.
        /// </para>
        /// </summary>
        [ValidateNotNullOrEmpty]
        public virtual string NodePoolName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Passed in a NodeConfig object containing configuration for the nodes in this node pool.
        /// This object can be created with New-GkeNodeConfig cmdlet.
        /// </para>
        /// </summary>
        [Parameter]
        public virtual NodeConfig NodeConfig { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, nodes in the node pool will be automatically upgraded.
        /// </para>
        /// </summary>
        [Parameter]
        public virtual SwitchParameter EnableAutoUpgrade { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the node pool will have autoscaling enabled and this number will represent
        /// the minimum number of nodes in the node pool that the cluster can scale to.
        /// </para>
        /// </summary>
        [Parameter]
        public virtual int? MaximumNodesToScaleTo { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the node pool will have autoscaling enabled and this number will represent
        /// the maximum number of nodes in the node pool that the cluster can scale to.
        /// </para>
        /// </summary>
        [Parameter]
        public virtual int? MininumNodesToScaleTo { get; set; }

        /// <summary>
        /// <para type="description">
        /// The number of nodes to create in a nodepool.
        /// </para>
        /// </summary>
        [Parameter]
        [ValidateRange(0, int.MaxValue)]
        public virtual int? InitialNodeCount { get; set; }

        /// <summary>
        /// Helper function to build a NodePool object.
        /// InitialNodeCount will default to 1.
        /// MaximumNodesToScaleTo have to be greater than MinimumNodesToScaleTo, which defaults to 1.
        /// </summary>
        /// <param name="name">The name of the node pool.</param>
        /// <param name="config">The config of the node pool.</param>
        /// <param name="initialNodeCount">The number of nodes created in the pool initially.</param>
        /// <param name="autoUpgrade">If true, nodes will have auto-upgrade enabled.</param>
        /// <param name="minimumNodesToScaleTo">The maximum number of nodes to scale to.</param>
        /// <param name="maximumNodesToScaleTo">
        /// The minimum number of nodes to scale to. Defaults to 1.
        /// </param>
        /// <returns></returns>
        protected NodePool BuildNodePool(string name, NodeConfig config, int? initialNodeCount, bool autoUpgrade,
            int? minimumNodesToScaleTo, int? maximumNodesToScaleTo)
        {
            var nodePool = new NodePool()
            {
                Name = name,
                InitialNodeCount = initialNodeCount ?? 1,
                Config = config
            };

            if (maximumNodesToScaleTo != null)
            {
                nodePool.Autoscaling = BuildAutoscaling(maximumNodesToScaleTo, minimumNodesToScaleTo);
            }

            if (autoUpgrade)
            {
                var nodeManagement = new NodeManagement() { AutoUpgrade = true };
                nodePool.Management = nodeManagement;
            }

            return nodePool;
        }

        internal static NodePoolAutoscaling BuildAutoscaling(int? maximumNodesToScaleTo, int? minimumNodesToScaleTo)
        {
            var scaling = new NodePoolAutoscaling() { Enabled = true };

            if (minimumNodesToScaleTo == null)
            {
                minimumNodesToScaleTo = 1;
            }

            if (maximumNodesToScaleTo < minimumNodesToScaleTo)
            {
                throw new PSArgumentException(
                    "Maximum node count in a node pool has to be greater or equal to the minimum count.");
            }

            // No need to check maximum nodes since we know for sure at this point it will be greater or equal to this.
            if (minimumNodesToScaleTo <= 0)
            {
                throw new PSArgumentException(
                    "Both -MaximumNodesToScaleTo and -MinimumNodesToScaleTo have to be greater than 0.");
            }

            scaling.MaxNodeCount = maximumNodesToScaleTo;
            scaling.MinNodeCount = minimumNodesToScaleTo;

            return scaling;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a Google Container Engine Node Pool
    /// </para>
    /// <para type="description">
    /// Creates a Google Container Engine Node Pool. The node pool can be used to create a cluster with
    /// Add-GkeCluster or added to an existing cluster with Add-GkeNodePool. If -Project is not used,
    /// the cmdlet will use the default project. If -Zone is not used, the cmdlet will use the default zone.
    /// -Project and -Zone parameters are only used to provide tab-completion for the possible list of
    /// image and machine types applicable to the nodes.
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GkeNodePool -NodePoolName "my-nodepool" -ImageType CONTAINER_VM</code>
    ///   <para>Creates a node pool "my-nodepool" with image type CONTAINER_VM for each node.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GkeNodePool -NodePoolName "my-nodepool" `
    ///                           -ImageType CONTAINER_VM `
    ///                           -MachineType n1-standard-1 `
    ///                           -InitialNodeCount 3
    ///   </code>
    ///   <para>
    ///   Creates a node pool with image type CONTAINER_VM for each node and machine type n1-standard-1
    ///   for each Google Compute Engine used to create the cluster. The node pool will have an initial
    ///   node count of 3.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GkeNodePool "my-nodepool" -DiskSizeGb 20 -SsdCount 2 -EnableAutoUpgrade</code>
    ///   <para>
    ///   Creates a node pool with 20 Gb disk size and 2 SSDs for each node. Each node in the node pool
    ///   will have autoupgrade enabled.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GkeNodePool "my-nodepool" -Metadata @{"key" = "value"} `
    ///                                         -Label @{"release" = "stable"} `
    ///                                         -MaximumNodesToScaleTo 3
    ///   </code>
    ///   <para>
    ///   Creates a node pool with metadata pair "key" = "value" and Kubernetes label "release" = "stable".
    ///   The node pool will scale to 3 nodes maximum.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> $serviceAccount = New-GceServiceAccountConfig -BigTableAdmin Full `
    ///                                                         -CloudLogging None `
    ///                                                         -CloudMonitoring None `
    ///                                                         -ServiceControl $false `
    ///                                                         -ServiceManagement $false `
    ///                                                         -Storage None
    ///   PS C:\> New-GkeNodePool "my-nodepool" -ServiceAccount $serviceAccount
    ///   </code>
    ///   <para>
    ///   Creates a node pool that uses the default service account with scopes "bigtable.admin".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GkeNodePool "my-nodepool" -Preemptible</code>
    ///   <para>
    ///   Creates a node pool where each node is created as preemptible VM instances.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GkeNodePool "my-nodepool" -NodeConfig $nodeConfig</code>
    ///   <para>
    ///   Creates a node pool using NodeConfig $nodeconfig.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/container-engine/reference/rest/v1/NodeConfig)">
    /// [Node Configs]
    /// </para>
    /// <para type="link" uri="(https://kubernetes.io/docs/user-guide/labels/)">
    /// [Kubernetes Labels]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/instances/preemptible)">
    /// [Preemptible VM instances]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/container-engine/docs/node-pools)">
    /// [Node Pools]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GkeNodePool")]
    public class NewGkeNodePoolCmdlet : GkeNodePoolConfigCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByNodeConfig = "ByNodeConfig";
            public const string ByNodeConfigValues = "ByNodeConfigValues";
            public const string ByNodePool = "ByNodePool";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the node pool.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public override string NodePoolName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Passed in a NodeConfig object containing configuration for the nodes in this node pool.
        /// This object can be created with New-GkeNodeConfig cmdlet.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByNodeConfig)]
        [ValidateNotNull]
        public override NodeConfig NodeConfig { get; set; }

        /// <summary>
        /// <para type="description">
        /// Size of the disk attached to each node in this node pool, specified in GB.
        /// The smallest allowed disk size is 10GB.
        /// The default disk size is 100GB.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        [ValidateRange(10, int.MaxValue)]
        public override int? DiskSizeGb { get; set; }

        /// <summary>
        /// <para type="description">
        /// Metadata key/value pairs assigned to instances in this node pool.
        /// Keys must conform to the regexp [a-zA-Z0-9-_]+ and not conflict with any other
        /// metadata keys for the project or be one of the four reserved keys: "instance-template",
        /// "kube-env", "startup-script" and "user-data".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        [Alias("Metadata")]
        public override Hashtable InstanceMetadata { get; set; }

        /// <summary>
        /// <para type="description">
        /// The map of Kubernetes labels (key/value pairs) to be applied to each node in this node pool.
        /// This is in addition to any default label(s) that Kubernetes may apply to the node.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public override Hashtable Label { get; set; }

        /// <summary>
        /// <para type="description">
        /// The number of local SSD disks attached to each node in this node pool.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        [ValidateRange(0, int.MaxValue)]
        public override int? LocalSsdCount { get; set; }

        /// <summary>
        /// <para type="description">
        /// The list of instance tags applied to each node in this node pool.
        /// Tags are used to identify valid sources or targets for network firewalls.
        /// Each tag must complied with RFC1035.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public override string[] Tags { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Google Cloud Platform Service Account to be used by each node's VMs.
        /// Use New-GceServiceAccountConfig to create the service account and appropriate scopes.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public override Apis.Compute.v1.Data.ServiceAccount ServiceAccount { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, every node created in this node pool will be a preemptible VM instance.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public override SwitchParameter Preemptible { get; set; }

        /// <summary>
        /// Add -MachineType and -ImageType parameter (they belong to "ByNodeConfigValues" parameter set).
        /// </summary>
        protected override void PopulateDynamicParameter(string project, string zone,
            RuntimeDefinedParameterDictionary dynamicParamDict)
        {
            // Gets all the valid machine types of this zone and project combination.
            string[] machineTypes = GetMachineTypes(Project, Zone);
            RuntimeDefinedParameter machineTypeParam = GenerateRuntimeParameter(
                parameterName: "MachineType",
                helpMessage: "The Google Compute Engine machine type to use for node in this node pool.",
                validSet: machineTypes,
                parameterSetNames: ParameterSetNames.ByNodeConfigValues);
            dynamicParamDict.Add("MachineType", machineTypeParam);

            // Gets all the valid image types of this zone and project combination.
            string[] imageTypes = GetImageTypes(Project, Zone);
            RuntimeDefinedParameter imageTypeParam = GenerateRuntimeParameter(
                parameterName: "ImageType",
                helpMessage: "The image type to use for node in this node pool.",
                validSet: imageTypes,
                parameterSetNames: ParameterSetNames.ByNodeConfigValues);
            dynamicParamDict.Add("ImageType", imageTypeParam);
        }

        protected override void ProcessRecord()
        {
            NodePool nodePool = BuildNodePoolFromParams();
            WriteObject(nodePool);
        }

        protected NodePool BuildNodePoolFromParams()
        {
            // Build the node config from parameters if user does not supply a NodeConfig object.
            if (ParameterSetName == ParameterSetNames.ByNodeConfigValues)
            {
                NodeConfig = BuildNodeConfig();
            }

            return BuildNodePool(NodePoolName, NodeConfig, InitialNodeCount, EnableAutoUpgrade,
                MininumNodesToScaleTo, MaximumNodesToScaleTo);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Adds a Google Container Engine Node Pool to a Cluster.
    /// </para>
    /// <para type="description">
    /// Adds a Google Container Engine Node Pool to a Cluster. If -Project is not used,
    /// the cmdlet will use the default project. If -Zone is not used, the cmdlet will use the default zone.
    /// -Project and -Zone parameters are only used to provide tab-completion for the possible list of
    /// image and machine types applicable to the nodes. You can either create a NodePool
    /// object separately with New-GkeNodePool and use it with -NodePool parameter or simply use
    /// the available parameters on this cmdlet to create a new NodePool.
    /// Instead of using -ClusterName to provide the name of the cluster, you can also use
    /// -ClusterObject, which takes in a Cluster object from Get-GkeCluster. When you use
    /// -ClusterObject, the Project and Zone will automatically be taken from the Cluster object.
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> $nodePool = New-GkeNodePool -NodePoolName "my-nodepool" -ImageType CONTAINER_VM
    ///   PS C:\> Add-GkeNodePool -NodePool $nodePool -Cluster "my-cluster"
    ///   </code>
    ///   <para>
    ///   Creates a node pool "my-nodepool" with image type CONTAINER_VM for each node.
    ///   Adds that pool to cluster "my-cluter".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Add-GkeNodePool -NodePoolName "my-nodepool" `
    ///                           -ImageType CONTAINER_VM `
    ///                           -MachineType n1-standard-1 `
    ///                           -InitialNodeCount 3 `
    ///                           -Cluster $cluster
    ///   </code>
    ///   <para>
    ///   Creates a node pool with image type CONTAINER_VM for each node and machine type n1-standard-1
    ///   for each Google Compute Engine used to create the node pool. The node pool will be added
    ///   to cluster $cluster where $cluster is a Cluster object returned from Get-GkeCluster.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Add-GkeNodePool "my-nodepool" -DiskSizeGb 20 `
    ///                                               -SsdCount 2 `
    ///                                               -EnableAutoUpgrade `
    ///                                               -Cluster "my-cluster" `
    ///                                               -Zone "europe-west1-c"
    ///   </code>
    ///   <para>
    ///   Creates a node pool with 20 Gb disk size and 2 SSDs for each node. Each node in the node pool
    ///   will have autoupgrade enabled. The node pool will be added to cluster "my-cluster" in zone
    ///   "europe-west1-c".
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/container-engine/reference/rest/v1/NodeConfig)">
    /// [Node Configs]
    /// </para>
    /// <para type="link" uri="(https://kubernetes.io/docs/user-guide/labels/)">
    /// [Kubernetes Labels]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/instances/preemptible)">
    /// [Preemptible VM instances]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/container-engine/docs/node-pools)">
    /// [Node Pools]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GkeNodePool")]
    public class AddGkeNodePoolCmdlet : GkeNodePoolConfigCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByNodeConfigClusterName = "ByNodeConfigClusterName";
            public const string ByNodeConfigValuesClusterName = "ByNodeConfigValuesClusterName";
            public const string ByNodePoolClusterName = "ByNodePoolClusterName";

            public const string ByNodeConfigClusterObject = "ByNodeConfigClusterObject";
            public const string ByNodeConfigValuesClusterObject = "ByNodeConfigValuesClusterObject";
            public const string ByNodePoolClusterObject = "ByNodePoolClusterObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that the node pool's cluster is in.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodePoolClusterName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the node pool's cluster is in.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodePoolClusterName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public override string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The node pool to be added to the cluster.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.ByNodePoolClusterObject)]
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.ByNodePoolClusterName)]
        [ValidateNotNull]
        public NodePool NodePool { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the node pool to be added.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByNodeConfigClusterName)]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterName)]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByNodeConfigClusterObject)]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterObject)]
        [ValidateNotNullOrEmpty]
        public override string NodePoolName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the cluster.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.ByNodeConfigClusterName)]
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterName)]
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.ByNodePoolClusterName)]
        [ValidateNotNullOrEmpty]
        public string ClusterName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The cluster object that the node pool will be added to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.ByNodeConfigClusterObject)]
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterObject)]
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.ByNodePoolClusterObject)]
        [ValidateNotNull]
        public Cluster ClusterObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// Passed in a NodeConfig object containing configuration for the nodes in this node pool.
        /// This object can be created with New-GkeNodeConfig cmdlet.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByNodeConfigClusterObject)]
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByNodeConfigClusterName)]
        [ValidateNotNull]
        public override NodeConfig NodeConfig { get; set; }

        /// <summary>
        /// <para type="description">
        /// Size of the disk attached to each node in this node pool, specified in GB.
        /// The smallest allowed disk size is 10GB.
        /// The default disk size is 100GB.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterObject)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterName)]
        [ValidateRange(10, int.MaxValue)]
        public override int? DiskSizeGb { get; set; }

        /// <summary>
        /// <para type="description">
        /// Metadata key/value pairs assigned to instances in this node pool.
        /// Keys must conform to the regexp [a-zA-Z0-9-_]+ and not conflict with any other
        /// metadata keys for the project or be one of the four reserved keys: "instance-template",
        /// "kube-env", "startup-script" and "user-data".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterObject)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterName)]
        [Alias("Metadata")]
        public override Hashtable InstanceMetadata { get; set; }

        /// <summary>
        /// <para type="description">
        /// The map of Kubernetes labels (key/value pairs) to be applied to each node in this node pool.
        /// This is in addition to any default label(s) that Kubernetes may apply to the node.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterObject)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterName)]
        public override Hashtable Label { get; set; }

        /// <summary>
        /// <para type="description">
        /// The number of local SSD disks attached to each node in this node pool.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterObject)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterName)]
        [ValidateRange(0, int.MaxValue)]
        public override int? LocalSsdCount { get; set; }

        /// <summary>
        /// <para type="description">
        /// The list of instance tags applied to each node in this node pool.
        /// Tags are used to identify valid sources or targets for network firewalls.
        /// Each tag must complied with RFC1035.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterObject)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterName)]
        public override string[] Tags { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Google Cloud Platform Service Account to be used by each node's VMs.
        /// Use New-GceServiceAccountConfig to create the service account and appropriate scopes.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterObject)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterName)]
        public override Apis.Compute.v1.Data.ServiceAccount ServiceAccount { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, every node created in this node pool will be a preemptible VM instance.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterObject)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValuesClusterName)]
        public override SwitchParameter Preemptible { get; set; }

        /// <summary>
        /// Add -MachineType and -ImageType parameter (they belong to "ByNodeConfigValues" parameter set).
        /// </summary>
        protected override void PopulateDynamicParameter(string project, string zone,
            RuntimeDefinedParameterDictionary dynamicParamDict)
        {
            // Gets all the valid machine types of this zone and project combination.
            string[] machineTypes = GetMachineTypes(Project, Zone);
            RuntimeDefinedParameter machineTypeParam = GenerateRuntimeParameter(
                parameterName: "MachineType",
                helpMessage: "The Google Compute Engine machine type to use for node in this node pool.",
                validSet: machineTypes,
                isMandatory: false,
                parameterSetNames: new string[] { ParameterSetNames.ByNodeConfigValuesClusterObject,
                                                  ParameterSetNames.ByNodeConfigValuesClusterName });
            dynamicParamDict.Add("MachineType", machineTypeParam);

            // Gets all the valid image types of this zone and project combination.
            string[] imageTypes = GetImageTypes(Project, Zone);
            RuntimeDefinedParameter imageTypeParam = GenerateRuntimeParameter(
                parameterName: "ImageType",
                helpMessage: "The image type to use for node in this node pool.",
                validSet: imageTypes,
                isMandatory: false,
                parameterSetNames: new string[] { ParameterSetNames.ByNodeConfigValuesClusterObject,
                                                  ParameterSetNames.ByNodeConfigValuesClusterName });
            dynamicParamDict.Add("ImageType", imageTypeParam);
        }

        protected override void ProcessRecord()
        {
            if (ClusterObject != null)
            {
                Zone = ClusterObject.Zone;
                ClusterName = ClusterObject.Name;
                Project = GetProjectNameFromUri(ClusterObject.SelfLink);
            }

            try
            {
                NodePool = NodePool ?? BuildNodePoolFromParams();
                var requestBody = new CreateNodePoolRequest() { NodePool = NodePool };
                ProjectsResource.ZonesResource.ClustersResource.NodePoolsResource.CreateRequest request =
                    Service.Projects.Zones.Clusters.NodePools.Create(requestBody, Project, Zone, ClusterName);
                Operation createOperation = request.Execute();
                NodePool createdNodePool = WaitForNodePoolCreation(createOperation);
                WriteObject(createdNodePool);
            }
            catch (GoogleApiException apiEx) when (apiEx.HttpStatusCode == HttpStatusCode.Conflict)
            {
                WriteResourceExistsError(
                    exceptionMessage: $"NodePool '{NodePool.Name}' already exists in cluster '{ClusterName}'"
                        + $"in zone '{Zone}' of project '{Project}'.",
                    errorId: "NodePoolAlreadyExists",
                    targetObject: NodePool.Name);
            }
            catch (GoogleApiException apiEx) when (apiEx.HttpStatusCode == HttpStatusCode.NotFound)
            {
                WriteResourceExistsError(
                    exceptionMessage: $"Cluster '{ClusterName}' cannot be found in zone '{Zone}'"
                        + $" of project '{Project}'.",
                    errorId: "ClusterNotFound",
                    targetObject: ClusterName);
            }
        }

        /// <summary>
        /// Wait for the NodePool creation operation to complete.
        /// Use write progress to display the progress in the meantime.
        /// </summary>
        private NodePool WaitForNodePoolCreation(Operation operation)
        {
            string activity = $"Creating NodePool '{NodePool.Name}' in cluster '{ClusterName}'" +
                $" in zone '{Zone}' of project '{Project}'.";
            string status = "Creating NodePool";
            WaitForClusterOperation(operation, Project, Zone, activity, status);

            // Returns the NodePool after it is created.
            ProjectsResource.ZonesResource.ClustersResource.NodePoolsResource.GetRequest getRequest =
                Service.Projects.Zones.Clusters.NodePools.Get(Project, Zone, ClusterName, NodePool.Name);
            return getRequest.Execute();
        }

        protected NodePool BuildNodePoolFromParams()
        {
            // Build the node config from parameters if user does not supply a NodeConfig object.
            if (ParameterSetName == ParameterSetNames.ByNodeConfigValuesClusterName
                || ParameterSetName == ParameterSetNames.ByNodeConfigValuesClusterObject)
            {
                NodeConfig = BuildNodeConfig();
            }

            return BuildNodePool(NodePoolName, NodeConfig, InitialNodeCount, EnableAutoUpgrade,
                MininumNodesToScaleTo, MaximumNodesToScaleTo);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes a Google Container NodePool from a Cluster.
    /// </para>
    /// <para type="description">
    /// Removes a Google Container NodePool from a Cluster.
    /// If -Project and -Zone are not specified, the cmdlets will default
    /// to the default project and zone.
    /// If -ClusterObject is used instead of -ClusterName, the Project and
    /// Zone will come from the cluster object.
    /// If a node pool object is given to -ClusterName, the cmdlet will
    /// get Project, Zone and Cluster information from the object.
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> Remove-GkeCluster -ClusterName "my-cluster" `
    ///                             -Zone "us-west1-b" `
    ///                             -NodePoolName "my-nodepool"
    ///   </code>
    ///   <para>
    ///   Removes the node pool "my-nodepool" in cluster "my-cluster" in the zone
    ///   "us-west1-b" of the default project.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/container-engine/docs/node-pools)">
    /// [Node Pools]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/container-engine/docs/clusters/)">
    /// [Container Clusters]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GkeNodePool", SupportsShouldProcess = true)]
    public class RemoveGkeNodePoolCmdlet : GkeCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByClusterObject = "ByClusterObject";
            public const string ByClusterName = "ByClusterName";
            public const string ByNodePoolObject = "ByNodePoolObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that the node pool's cluster belongs to.
        /// This parameter defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByClusterName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone that the node pool's cluster belongs to.
        /// This parameter defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByClusterName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the node pool to be removed.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0,
            ParameterSetName = ParameterSetNames.ByClusterName)]
        [Parameter(Mandatory = true, Position = 0,
            ParameterSetName = ParameterSetNames.ByClusterObject)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(NodePool),
            Property = nameof(Google.Apis.Container.v1.Data.NodePool.Name))]
        [ValidateNotNullOrEmpty]
        public string NodePoolName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The NodePool object to be removed. Cluster, Zone and Project will be inferred
        /// from the object.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByNodePoolObject)]
        [ValidateNotNull]
        public NodePool NodePoolObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the container cluster that the node pool is in.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1,
            ParameterSetName = ParameterSetNames.ByClusterName)]
        [ValidateNotNullOrEmpty]
        public string ClusterName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The container cluster object that the node pool is in.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1,
            ParameterSetName = ParameterSetNames.ByClusterObject)]
        [ValidateNotNullOrEmpty]
        public Cluster ClusterObject { get; set; }

        protected override void ProcessRecord()
        {
            ProcessParams();

            ProjectsResource.ZonesResource.ClustersResource.NodePoolsResource.DeleteRequest deleteRequest =
                Service.Projects.Zones.Clusters.NodePools.Delete(Project, Zone, ClusterName, NodePoolName);
            if (ShouldProcess($"Node pool '{NodePoolName}' in cluster '{ClusterName}' in"
                + $" zone '{Zone}' of project '{Project}'.",
                "Removing GKE Cluster"))
            {
                try
                {
                    Operation deleteOperation = deleteRequest.Execute();
                    WaitForNodePoolDeletion(deleteOperation);
                }
                catch (GoogleApiException apiEx) when (apiEx.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    WriteResourceMissingError(
                        $"NodePool '{NodePoolName}' cannot be found in cluster '{ClusterName}' in zone "
                           + $"'{Zone}' of project '{Project}'.",
                        "ClusterNotFound",
                        ClusterName);
                }
            }
        }

        /// <summary>
        /// Process parameters and fill out Project, Zone and ClusterName.
        /// </summary>
        private void ProcessParams()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByNodePoolObject:
                    Uri nodePoolLink;
                    NodePoolName = NodePoolObject.Name;
                    if (Uri.TryCreate(NodePoolObject.SelfLink, UriKind.Absolute, out nodePoolLink)
                        && nodePoolLink.Scheme == "https")
                    {
                        Project = GetProjectNameFromUri(NodePoolObject.SelfLink);
                        Zone = GetUriPart("zones", NodePoolObject.SelfLink);
                        ClusterName = GetUriPart("clusters", NodePoolObject.SelfLink);
                        break;
                    }
                    throw new PSArgumentException("Cluster Object does not have SelfLink URL.");
                case ParameterSetNames.ByClusterObject:
                    Zone = ClusterObject.Zone;
                    ClusterName = ClusterObject.Name;
                    Project = GetProjectNameFromUri(ClusterObject.SelfLink);
                    break;
                case ParameterSetNames.ByClusterName:
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        /// <summary>
        /// Wait for the node pool deletion operation to complete.
        /// Use write progress to display the progress in the meantime.
        /// </summary>
        private void WaitForNodePoolDeletion(Operation operation)
        {
            string activity = $"Deleting node pool '{NodePoolName}' in '{ClusterName}'"
                + $" in zone '{Zone}' of project '{Project}'.";
            string status = "Deleting node pool";
            WaitForClusterOperation(operation, Project, Zone, activity, status);
        }
    }
}
