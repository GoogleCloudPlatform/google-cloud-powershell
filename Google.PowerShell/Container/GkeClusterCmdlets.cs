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
using ComputeService = Google.Apis.Compute.v1.ComputeService;

namespace Google.PowerShell.Container
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Container Clusters.
    /// </para>
    /// <para type="description">
    /// Gets Google Container Clusters. If -Project parameter is not specified, the default project will be used.
    /// If neither -Zone nor -ClusterName is used, the cmdlet will return every cluster in every zone in the project.
    /// If -Zone is used without -ClusterName, the cmdlet will return every cluster in the specified zone.
    /// If -ClusterName is used without -Zone, the cmdlet will return the specified clusters in the default zone
    /// (set in Cloud SDK Config). If -Clustername is used with -Zone, the cmdlet will return the specified
    /// clusters in the specified zone.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GkeCluster</code>
    ///   <para>Lists all container clusters in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GkeCluster -Zone "us-central1-a" -Project "my-project"</code>
    ///   <para>Lists all container clusters in zone us-central1-a for the project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GkeCluster -ClusterName "my-cluster"</code>
    ///   <para>Gets the cluster "my-cluster" in the default zone of the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GkeCluster -ClusterName "my-cluster", "my-cluster-2" -Zone "us-central1-a"</code>
    ///   <para>
    ///   Gets the clusters "my-cluster", "my-cluster-2" in the zone "us-central1-a" of the default project.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/container-engine/docs/clusters/)">
    /// [Container Clusters]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GkeCluster", DefaultParameterSetName = ParameterSetNames.AllZone)]
    public class GetGkeClusterCmdlet : GkeCmdlet
    {
        private class ParameterSetNames
        {
            public const string AllZone = "AllZone";
            public const string ByZone = "ByZone";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// The project that the container clusters belong to.
        /// This parameter defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone that the container clusters belong to.
        /// This parameter defaults to the zone in the Cloud SDK config if -ClusterName parameter is used.
        /// Otherwise, it defaults to all the zones.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByZone, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names of the clusters to search for.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        [Alias("Name")]
        [ValidateNotNullOrEmpty]
        public string[] ClusterName { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.AllZone:
                    // "-" represents all zone.
                    WriteObject(GetClustersByZone(Project, "-"), true);
                    break;
                case ParameterSetNames.ByZone:
                    WriteObject(GetClustersByZone(Project, Zone), true);
                    break;
                case ParameterSetNames.ByName:
                    WriteObject(GetClustersByName(Project, Zone, ClusterName), true);
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        /// <summary>
        /// Returns clusters that have the names in clusters array in zone 'zone' in project 'project'.
        /// </summary>
        private IEnumerable<Cluster> GetClustersByName(string project, string zone, string[] clusters)
        {
            foreach (var cluster in clusters)
            {
                Cluster result = null;
                try
                {
                    ProjectsResource.ZonesResource.ClustersResource.GetRequest getRequest =
                        Service.Projects.Zones.Clusters.Get(project, zone, cluster);
                    result = getRequest.Execute();
                }
                catch (GoogleApiException apiEx) when (apiEx.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    WriteResourceMissingError(
                        exceptionMessage: $"Cluster '{cluster}' cannot be found in zone '{zone}' of project '{Project}'.",
                        errorId: "ClusterNotFound",
                        targetObject: cluster);
                }

                if (result != null)
                {
                    yield return result;
                }
            }
        }

        /// <summary>
        /// Returns all clusters in zone 'zone' in project 'project'.
        /// </summary>
        private IEnumerable<Cluster> GetClustersByZone(string project, string zone)
        {
            // This list request does not have page token so we can only send one.
            ProjectsResource.ZonesResource.ClustersResource.ListRequest listRequest =
                Service.Projects.Zones.Clusters.List(project, zone);
            ListClustersResponse response = listRequest.Execute();
            if (response.Clusters != null)
            {
                foreach (Cluster cluster in response.Clusters)
                {
                    yield return cluster;
                }
            }
            // The list of clusters returned may be missing these zones.
            if (response.MissingZones != null && response.MissingZones.Count != 0)
            {
                // Concatenate the zones and put single quote around them.
                string joinedMissingZones =
                    string.Join(", ", response.MissingZones.Select(missingZone => $"'{missingZone}'"));
                WriteWarning($"The clusters returned may be missing the following zones: {joinedMissingZones}");
            }
        }
    }

    /// <summary>
    /// Abstract class for cmdlets that deal with node configuration such as
    /// New-GkeNodeConfig and Add-GkeCluster.
    /// </summary>
    public abstract class GkeNodeConfigCmdlet : GkeCmdlet, IDynamicParameters
    {
        // Regex that is used to check metadata key.
        private static readonly Regex s_metadataKeyRegex = new Regex("[a-zA-Z0-9-_]+");

        // Reserved key word for metadata key.
        private static readonly string[] s_reservedMetadataKey =
            new string[] { "instance-template", "kube-env", "startup-script", "user-data" };

        /// <summary>
        /// <para type="description">
        /// The project that the node config belongs to.
        /// This parameter defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone that the node config belongs to.
        /// This parameter defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public virtual string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Size of the disk attached to each node, specified in GB.
        /// The smallest allowed disk size is 10GB.
        /// The default disk size is 100GB.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateRange(10, int.MaxValue)]
        public virtual int? DiskSizeGb { get; set; }

        /// <summary>
        /// <para type="description">
        /// Metadata key/value pairs assigned to instances in the cluster.
        /// Keys must conform to the regexp [a-zA-Z0-9-_]+ and not conflict with any other
        /// metadata keys for the project or be one of the four reserved keys: "instance-template",
        /// "kube-env", "startup-script" and "user-data".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [Alias("Metadata")]
        public virtual Hashtable InstanceMetadata { get; set; }

        /// <summary>
        /// <para type="description">
        /// The map of Kubernetes labels (key/value pairs) to be applied to each node.
        /// This is in addition to any default label(s) that Kubernetes may apply to the node.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public virtual Hashtable Label { get; set; }

        /// <summary>
        /// <para type="description">
        /// The number of local SSD disks attached to each node.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateRange(0, int.MaxValue)]
        public virtual int? LocalSsdCount { get; set; }

        /// <para type="description">
        /// The list of instance tags applied to each node.
        /// Tags are used to identify valid sources or targets for network firewalls.
        /// </para>
        [Parameter(Mandatory = false)]
        public virtual string[] Tags { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Google Cloud Platform Service Account to be used by the node VMs.
        /// Use New-GceServiceAccountConfig to create the service account and appropriate scopes.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public virtual Apis.Compute.v1.Data.ServiceAccount ServiceAccount { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, every node created will be a preemptible VM instance.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public virtual SwitchParameter Preemptible { get; set; }

        /// <summary>
        /// This dynamic parameter dictionary is used by PowerShell to generate parameters dynamically.
        /// </summary>
        private RuntimeDefinedParameterDictionary _dynamicParameters;

        /// <summary>
        /// Generate dynamic parameter -MachineType and -ImageType based on the value of -Project
        /// and -Zone. This will provide tab-completion for -MachineType and -ImageType parameters.
        /// </summary>
        public object GetDynamicParameters()
        {
            if (_dynamicParameters == null)
            {
                _dynamicParameters = new RuntimeDefinedParameterDictionary();

                // Try to resolve Project variable to a string, use default value from the SDK if we fail to do so.
                Project = GetCloudSdkSettingValue(CloudSdkSettings.CommonProperties.Project, Project);
                // Try to resolve Zone variable to a string, use default value from the SDK if we fail to do so.
                Zone = GetCloudSdkSettingValue(CloudSdkSettings.CommonProperties.Zone, Zone);

                PopulateDynamicParameter(Project, Zone, _dynamicParameters);
            }

            return _dynamicParameters;
        }

        /// <summary>
        /// Using project and zone, create dynamic parameters (project and zone are used to make API call
        /// to get valid set of values for the parameters) and populate the dynamic parameter dictionary.
        /// </summary>
        protected abstract void PopulateDynamicParameter(string project, string zone,
            RuntimeDefinedParameterDictionary dynamicParamDict);

        /// <summary>
        /// Returns the machine type that the user selected.
        /// </summary>
        protected string SelectedMachineType
        {
            get
            {
                if (_dynamicParameters.ContainsKey("MachineType"))
                {
                    return _dynamicParameters["MachineType"].Value?.ToString().ToLower();
                }
                return null;
            }
        }

        /// <summary>
        /// Returns the image type that the user selected.
        /// </summary>
        protected string SelectedImageType
        {
            get
            {
                if (_dynamicParameters.ContainsKey("ImageType"))
                {
                    return _dynamicParameters["ImageType"].Value?.ToString().ToLower();
                }
                return null;
            }
        }

        protected NodeConfig BuildNodeConfig()
        {
            var nodeConfig = new NodeConfig()
            {
                DiskSizeGb = DiskSizeGb,
                LocalSsdCount = LocalSsdCount,
                Tags = Tags,
                ServiceAccount = ServiceAccount?.Email,
                OauthScopes = ServiceAccount?.Scopes,
                Preemptible = Preemptible.ToBool(),
                MachineType = SelectedMachineType,
                ImageType = SelectedImageType,
                Labels = ConvertToDictionary<string, string>(Label),
            };

            if (InstanceMetadata != null)
            {
                // Metadata key/value pairs assigned to instances in the cluster.
                // Keys must conform to the regexp [a-zA-Z0-9-_]+ and not conflict with any other
                // metadata keys for the project or be one of the four reserved keys: "instance-template",
                // "kube-env", "startup-script" and "user-data".
                Dictionary<string, string> metadataDict = ConvertToDictionary<string, string>(InstanceMetadata);
                foreach (string key in metadataDict.Keys)
                {
                    if (!s_metadataKeyRegex.IsMatch(key))
                    {
                        ThrowTerminatingError(new ErrorRecord(
                            new ArgumentException("Metadata key can only be alphanumeric, hyphen or underscore."),
                            "InvalidMetadataKey",
                            ErrorCategory.InvalidArgument,
                            key));
                    }

                    if (s_reservedMetadataKey.Contains(key, StringComparer.OrdinalIgnoreCase))
                    {
                        ThrowTerminatingError(new ErrorRecord(
                            new ArgumentException($"Metadata key '{key}' is a reserved keyword."),
                            "InvalidMetadataKey",
                            ErrorCategory.InvalidArgument,
                            key));
                    }
                }
                nodeConfig.Metadata = metadataDict;
            }

            return nodeConfig;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a Google Container Engine Node Config.
    /// </para>
    /// <para type="description">
    /// Creates a Google Container Engine Node Config. The node config is used to configure various properties
    /// of a node in a container cluster so you can use the object returned by the cmdlet in Add-GkeCluster
    /// to create a container cluster. If -Project is not used, the cmdlet will use the default project.
    /// If -Zone is not used, the cmdlet will use the default zone. -Project and -Zone parameters are only
    /// used to provide tab-completion for the possible list of image and machine types applicable to the nodes.
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GkeNodeConfig -ImageType CONTAINER_VM</code>
    ///   <para>Creates a node config with image type CONTAINER_VM for each node.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GkeNodeConfig -ImageType CONTAINER_VM -MachineType n1-standard-1</code>
    ///   <para>
    ///   Creates a node config with image type CONTAINER_VM for each node and machine type n1-standard-1
    ///   for each Google Compute Engine used to create the cluster.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GkeNodeConfig -DiskSizeGb 20 -SsdCount 2</code>
    ///   <para>
    ///   Creates a node config with 20 Gb disk size and 2 SSDs for each node.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GkeNodeConfig -Metadata @{"key" = "value"} -Label @{"release" = "stable"}</code>
    ///   <para>
    ///   Creates a node config with metadata pair "key" = "value" and Kubernetes label "release" = "stable".
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
    ///   PS C:\> New-GkeNodeConfig -ServiceAccount $serviceAccount
    ///   </code>
    ///   <para>
    ///   Creates a node config that uses the default service account with scopes "bigtable.admin".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GkeNodeConfig -Preemptible</code>
    ///   <para>
    ///   Creates a node config where each node is created as preemptible VM instances.
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
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GkeNodeConfig")]
    public class NewGkeNodeConfigCmdlet : GkeNodeConfigCmdlet
    {
        protected override void PopulateDynamicParameter(string project, string zone,
            RuntimeDefinedParameterDictionary dynamicParamDict)
        {
            // Gets all the valid machine types of this zone and project combination.
            string[] machineTypes = GetMachineTypes(Project, Zone);
            RuntimeDefinedParameter machineTypeParam = GenerateRuntimeParameter(
                parameterName: "MachineType",
                helpMessage: "The Google Compute Engine machine type to use for this node.",
                validSet: machineTypes);
            dynamicParamDict.Add("MachineType", machineTypeParam);

            // Gets all the valid image types of this zone and project combination.
            string[] imageTypes = GetImageTypes(Project, Zone);
            RuntimeDefinedParameter imageTypeParam = GenerateRuntimeParameter(
                parameterName: "ImageType",
                helpMessage: "The image type to use for this node.",
                validSet: imageTypes);
            dynamicParamDict.Add("ImageType", imageTypeParam);
        }

        protected override void ProcessRecord()
        {
            WriteObject(BuildNodeConfig());
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a Google Container Cluster.
    /// </para>
    /// <para type="description">
    /// Creates a Google Container Cluster. If -Project and/or -Zone are not used, the cmdlet will use
    /// the default project and/or default zone. There are 3 ways to create a cluster.
    /// You can pass in a NodeConfig object (created using New-GkeNodeConfig) and the cmdlet will create
    /// a cluster whose node pools will have their configurations set from the NodeConfig object.
    /// Instead of passing in a NodeConfig object, you can also use the parameters provided in this cmdlet
    /// and a NodeConfig object will be automatically created and used in the cluster creation (same as above).
    /// In both cases above, you can specify how many node pools the cluster will have with -NumberOfNodePools.
    /// Lastly, you can also create a cluster by passing in an array of NodePool objects and a cluster with
    /// node pools similar to that array will be created.
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> Add-GkeCluster -NodeConfig $nodeConfig `
    ///                          -ClusterName "my-cluster" `
    ///                          -Network "my-network"
    ///   </code>
    ///   <para>
    ///   Creates a cluster named "my-cluster" in the default zone of the default project using config
    ///   $nodeConfig and network "my-network".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Add-GkeCluster -MachineType "n1-standard-4" `
    ///                          -ClusterName "my-cluster" `
    ///                          -Description "My new cluster" `
    ///                          -Subnetwork "my-subnetwork" `
    ///                          -EnableAutoUpgrade `
    ///                          -MaximumNodesToScaleTo 2
    ///   </code>
    ///   <para>
    ///   Creates a cluster named "my-cluster" with description "my new cluster" in the default zone of
    ///   the default project using machine type "n1-standard-4" for each Google Compute Engine VMs
    ///   in the cluster. The cluster will use the subnetwork "my-subnetwork".
    ///   The cluster's nodes will have autoupgrade enabled.
    ///   The cluster will also autoscale its node pool to a maximum of 2 nodes.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Add-GkeCluster -ImageType "GCI" `
    ///                          -ClusterName "my-cluster" `
    ///                          -Zone "us-central1-a" `
    ///                          -MasterCredential (Get-Credential) `
    ///                          -DisableMonitoringService `
    ///                          -AdditionalZone "us-central1-f" `
    ///                          -NumberOfNodePools 2 `
    ///                          -DisableHttpLoadBalancing
    ///   </code>
    ///   <para>
    ///   Creates a cluster named "my-cluster" in zone "us-central1-a" of the default project.
    ///   Asides from "us-central1-a", the cluster's nodes will also be found at zone "us-central1-f".
    ///   The cluster will not have Google Monitoring Service enabled to write metrics.
    ///   The master node of the cluster will have credential supplied by (Get-Credential).
    ///   Each node of the cluster will be of type GCI. The cluster will not have HTTP load balancing.
    ///   The cluster created will have 2 node pools with the same node config.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> $nodePools = Get-GkeNodePool -Cluster "my-cluster"
    ///   PS C:\> Add-GkeCluster -ClusterName "my-cluster-2" `
    ///                          -NodePool $nodePools `
    ///                          -DisableHorizontalPodAutoscaling
    ///   </code>
    ///   <para>
    ///   Creates cluster "my-cluster-2" using the node pools from "my-cluster".
    ///   The cluster will have horizontal pod autoscaling disabled.
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
    [Cmdlet(VerbsCommon.Add, "GkeCluster")]
    public class AddGkeClusterCmdlet : GkeNodePoolConfigCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByNodeConfig = "ByNodeConfig";
            public const string ByNodeConfigValues = "ByNodeConfigValues";
            public const string ByNodePool = "ByNodePool";
        }

        /// <summary>
        /// <para type="description">
        /// Size of the disk attached to each node in the cluster, specified in GB.
        /// The smallest allowed disk size is 10GB.
        /// The default disk size is 100GB.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        [ValidateRange(10, int.MaxValue)]
        public override int? DiskSizeGb { get; set; }

        /// <summary>
        /// <para type="description">
        /// Metadata key/value pairs assigned to instances in the cluster.
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
        /// The map of Kubernetes labels (key/value pairs) to be applied to each node in the cluster.
        /// This is in addition to any default label(s) that Kubernetes may apply to the node.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public override Hashtable Label { get; set; }

        /// <summary>
        /// <para type="description">
        /// The number of local SSD disks attached to each node in the cluster.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        [ValidateRange(0, int.MaxValue)]
        public override int? LocalSsdCount { get; set; }

        /// <summary>
        /// <para type="description">
        /// The list of instance tags applied to each node in the cluster.
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
        /// If set, every node created in the cluster will be a preemptible VM instance.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public override SwitchParameter Preemptible { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the cluster.
        /// Name has to start with a letter, end with a number or letter
        /// and consists only of letters, numbers and hyphens.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidatePattern("[a-zA-Z][a-zA-Z0-9-]*[a-zA-Z0-9]")]
        public string ClusterName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The description of the cluster.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The credential to access the master endpoint.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNull]
        public PSCredential MasterCredential { get; set; }

        /// <summary>
        /// <para type="description">
        /// Stop the cluster from using Google Cloud Logging Service to write logs.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter DisableLoggingService { get; set; }

        /// <summary>
        /// <para type="description">
        /// Stop the cluster from using Google Cloud Monitoring service to write metrics.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter DisableMonitoringService { get; set; }

        /// <summary>
        /// <para type="description">
        /// Removes HTTP load balancing controller addon.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter DisableHttpLoadBalancing { get; set; }

        /// <summary>
        /// <para type="description">
        /// Removes horizontal pod autoscaling feature, which increases or decreases the number of replica
        /// pods a replication controller has based on the resource usage of the existing pods.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter DisableHorizontalPodAutoscaling { get; set; }

        /// <summary>
        /// <para type="description">
        /// Enables Kubernetes alpha features on the cluster. This includes alpha API groups
        /// and features that may not be production ready in the kubernetes version of the master and nodes.
        /// The cluster has no SLA for uptime and master/node upgrades are disabled.
        /// Alpha enabled clusters are AUTOMATICALLY DELETED thirty days after creation.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter EnableKubernetesAlpha { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, nodes in the cluster will be automatically upgraded.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfig)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public override SwitchParameter EnableAutoUpgrade { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the cluster will have autoscaling enabled and this number will represent
        /// the maximum number of nodes in the node pool that the cluster can scale to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfig)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public override int? MaximumNodesToScaleTo { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the cluster will have autoscaling enabled and this number will represent
        /// the minimum number of nodes in the node pool that the cluster can scale to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfig)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public override int? MininumNodesToScaleTo { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the Google Compute Engine network to which the cluster is connected.
        /// If left unspecified, the default network will be used.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Network { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the Google Compute Engine subnetwork to which the cluster is connected.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Subnetwork { get; set; }

        /// <summary>
        /// <para type="description">
        /// The IP address range of the container pods in this cluster, in CIDR notation.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string ClusterIpv4AddressRange { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zones (in addition to the zone specified by -Zone parameter) in which
        /// the cluster's nodes should be located.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string[] AdditionalZone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Passed in a NodeConfig object containing configuration for the nodes in this cluster.
        /// This object can be created with New-GkeNodeConfig cmdlet.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByNodeConfig, ValueFromPipeline = true)]
        [ValidateNotNull]
        public override NodeConfig NodeConfig { get; set; }

        /// <summary>
        /// <para type="description">
        /// The number of node pools that the cluster will have. All the node pools will have the same config.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfig)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public int? NumberOfNodePools { get; set; }

        /// <summary>
        /// <para type="description">
        /// The node pools associated with this cluster.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByNodePool, ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        public NodePool[] NodePool { get; set; }

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
                helpMessage: "The Google Compute Engine machine type to use for node in this cluster.",
                validSet: machineTypes,
                parameterSetNames: ParameterSetNames.ByNodeConfigValues);
            dynamicParamDict.Add("MachineType", machineTypeParam);

            // Gets all the valid image types of this zone and project combination.
            string[] imageTypes = GetImageTypes(Project, Zone);
            RuntimeDefinedParameter imageTypeParam = GenerateRuntimeParameter(
                parameterName: "ImageType",
                helpMessage: "The image type to use for node in this cluster.",
                validSet: imageTypes,
                parameterSetNames: ParameterSetNames.ByNodeConfigValues);
            dynamicParamDict.Add("ImageType", imageTypeParam);
        }

        protected override void ProcessRecord()
        {
            try
            {
                CreateClusterRequest requestBody = new CreateClusterRequest() { Cluster = BuildCluster() };
                ProjectsResource.ZonesResource.ClustersResource.CreateRequest request =
                    Service.Projects.Zones.Clusters.Create(requestBody, Project, Zone);
                Operation createOperation = request.Execute();
                Cluster createdCluster = WaitForClusterCreation(createOperation);
                WriteObject(createdCluster);
            }
            catch (GoogleApiException apiEx) when (apiEx.HttpStatusCode == HttpStatusCode.Conflict)
            {
                WriteResourceExistsError(
                    exceptionMessage: $"Cluster '{ClusterName}' already exists in zone '{Zone}' of project '{Project}'",
                    errorId: "ClusterAlreadyExists",
                    targetObject: ClusterName);
            }
        }

        /// <summary>
        /// Wait for the cluster creation operation to complete.
        /// Use write progress to display the progress in the meantime.
        /// </summary>
        private Cluster WaitForClusterCreation(Operation operation)
        {
            string activity = $"Creating cluster '{ClusterName}' in zone '{Zone}' of project '{Project}'.";
            string status = "Creating cluster";
            WaitForClusterOperation(operation, Project, Zone, activity, status);

            // Returns the cluster after it is created.
            ProjectsResource.ZonesResource.ClustersResource.GetRequest getClusterRequest =
                Service.Projects.Zones.Clusters.Get(Project, Zone, ClusterName);
            return getClusterRequest.Execute();
        }

        /// <summary>
        /// Build a cluster object based on the parameter given.
        /// </summary>
        private Cluster BuildCluster()
        {
            // Build the node config from parameters if user does not supply a NodeConfig object.
            if (ParameterSetName == ParameterSetNames.ByNodeConfigValues)
            {
                NodeConfig = BuildNodeConfig();
            }

            // Build the node pool from the node config if user does not supply a NodePool object.
            if (ParameterSetName != ParameterSetNames.ByNodePool)
            {
                NodePool = BuildNodePools(NodeConfig).ToArray();
            }

            Cluster cluster = new Cluster()
            {
                Name = ClusterName,
                Description = Description,
                EnableKubernetesAlpha = EnableKubernetesAlpha,
                // If LoggingService field is not set, the cluster will default to logging.googleapis.com
                LoggingService = DisableLoggingService ? "none" : null,
                // If this field is not set, the cluster will default to monitoring.googleapis.com
                MonitoringService = DisableMonitoringService ? "none" : null
            };
            SetAddonsConfig(cluster, DisableHorizontalPodAutoscaling, DisableHttpLoadBalancing);
            cluster.NodePools = NodePool;

            if (EnableKubernetesAlpha)
            {
                WriteWarning("Cluster with Kubernetes alpha features has no SLA for uptime and master/node upgrades are disabled."
                    + "Alpha enabled clusters are automatically deleted thirty days after creation.");
            }

            // Add all the locations of the cluster's nodes.
            List<string> locations = new List<string> { Zone };
            if (AdditionalZone != null)
            {
                locations.AddRange(AdditionalZone);
            }
            cluster.Locations = locations;

            PopulateClusterNetwork(cluster);
            PopulateClusterMasterCredential(cluster);

            return cluster;
        }

        /// <summary>
        /// Set AddonsConfig of cluster if user wants to disable horizontal pod autoscaling or HTTP load balancing.
        /// </summary>
        private void SetAddonsConfig(Cluster cluster, bool disablePodAutoscaling, bool disableLoadBalancing)
        {
            if (disablePodAutoscaling || disableLoadBalancing)
            {
                var addons = new AddonsConfig();
                if (disableLoadBalancing)
                {
                    addons.HttpLoadBalancing = new HttpLoadBalancing() { Disabled = true };
                }

                if (disablePodAutoscaling)
                {
                    addons.HorizontalPodAutoscaling = new HorizontalPodAutoscaling { Disabled = true };
                }

                cluster.AddonsConfig = addons;
            }
        }

        /// <summary>
        /// Create a node pool based on either the NodeConfig object passed in or ByNodeConfigValues parameters.
        /// </summary>
        private IEnumerable<NodePool> BuildNodePools(NodeConfig nodeConfig)
        {
            string[] nodePoolNames = null;

            if (NumberOfNodePools == null || NumberOfNodePools <= 1)
            {
                // By default, if we do not use a NodePool object in the API, GKE will create
                // a NodePool with the name "default-pool" so we try to mimick that behavior.
                // API errors will be raised if NodePool object passed in does not have a name.
                nodePoolNames = new string[] { "default-pool" };
            }
            else
            {
                nodePoolNames = new string[NumberOfNodePools.Value];
                for (int i = 0; i < nodePoolNames.Length; i += 1)
                {
                    nodePoolNames[i] = $"default-pool-{i}";
                }
            }

            foreach (string nodePoolName in nodePoolNames)
            {
                yield return BuildNodePool(nodePoolName, nodeConfig, InitialNodeCount, EnableAutoUpgrade,
                    MininumNodesToScaleTo, MaximumNodesToScaleTo);
            }
        }

        /// <summary>
        /// Fill out username and password for master node based on MasterCredential.
        /// </summary>
        private void PopulateClusterMasterCredential(Cluster cluster)
        {
            if (MasterCredential != null)
            {
                NetworkCredential networkCred = MasterCredential.GetNetworkCredential();
                var masterAuth = new MasterAuth()
                {
                    Username = networkCred.UserName,
                    Password = networkCred.Password
                };
                cluster.MasterAuth = masterAuth;
            }
        }

        /// <summary>
        /// Fill out network, subnetwork and IPv4 address range for the cluster.
        /// </summary>
        private void PopulateClusterNetwork(Cluster cluster)
        {
            // It seems for this API, we have to specify the short name of the network and not the full name.
            // For example "my-network" works but not projects/my-project/global/networks/my-network.
            if (Network != null && Network.Contains("networks/"))
            {
                Network = GetUriPart("networks", Network);
            }

            if (Subnetwork != null && Subnetwork.Contains("subnetworks/"))
            {
                Subnetwork = GetUriPart("subnetworks", Subnetwork);
            }
            cluster.Network = Network;
            cluster.Subnetwork = Subnetwork;
            cluster.ClusterIpv4Cidr = ClusterIpv4AddressRange;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Updates a Google Container Cluster.
    /// </para>
    /// <para type="description">
    /// Updates a Google Container Cluster. Only one property can be updated at a time.
    /// The properties are:
    ///   1. AddonsConfig for a cluster (-LoadBalancing and -HorizontalPodAutoscaling).
    ///   2. Additional zones for the cluster (-AdditionalZone).
    ///   3. Version of the master, which can only be changed to the latest (-UpdateMaster).
    ///   4. Monitoring service for a cluster (-MonitoringService).
    ///   5. Autoscaling for a node pool in the cluster (-Min/MaximumNodesToScaleTo).
    ///   6. Kubernetes version for nodes in a node pool in the cluster (-NodeVersion).
    ///   7. Image type for nodes in a node pool in the cluster (-ImageType).
    /// </para>
    /// <para type="description">
    /// To specify a cluster, you can supply its name to -ClusterName. If -Project and/or -Zone
    /// are not used in this case, the cmdlet will use the default project and/or default zone.
    /// The cmdlet also accepts a Cluster object (from Get-GkeCluster cmdlet) with -ClusterObject.
    /// In this case, the Project and Zone will come from the cluster object itself.
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GkeCluster -ClusterName "my-cluster" `
    ///                          -LoadBalancing $true
    ///   </code>
    ///   <para>
    ///   Turns on load balancing for cluster "my-cluster" in the default zone and project.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GkeCluster -ClusterName "my-cluster" `
    ///                          -Zone "asia-east1-a" `
    ///                          -AdditionalZone "asia-east1-b", "asia-east1-c"
    ///   </code>
    ///   <para>
    ///   Sets additional zones of cluster "my-cluster" in zone "asia-east1-a" to zones
    ///   "asia-east1-b" and "asia-east1-c". This means the clusters will have nodes
    ///   created in these zones. The primary zone ("asia-east1-a" in this case)
    ///   will be added to the AdditionalZone array by the cmdlet.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GkeCluster -ClusterObject $clusterObject `
    ///                          -NodePoolName "default-pool" `
    ///                          -MaximumNodesToScaleTo 3
    ///   </code>
    ///   <para>
    ///   Sets the node pool "default-pool" in the Cluster object $clusterObject
    ///   to have autoscaling with a max nodes count of 3.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GkeCluster -ClusterName "my-cluster" `
    ///                          -NodePoolName "default-pool" `
    ///                          -NodeVersion "1.4.9"
    ///   </code>
    ///   <para>
    ///   Sets the Kubernetes version of nodes in node pool "default-pool" in cluster
    ///   "my-cluster" to 1.4.9. Note that the version of the nodes has to be
    ///   less than that of the master. Otherwise, the cmdlet will throw an error.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/container-engine/docs/clusters/)">
    /// [Container Clusters]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/container-engine/docs/node-pools)">
    /// [Node Pools]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GkeCluster")]
    public class SetGkeClusterCmdlet : GkeCmdlet, IDynamicParameters
    {
        private class ParameterSetNames
        {
            public const string UpdateNodePoolClusterName = "UpdateNodePoolClusterName";
            public const string UpdateNodePoolClusterObject = "UpdateNodePoolClusterObject";

            public const string UpdateAdditionalZoneClusterName = "UpdateAdditionalZoneClusterName";
            public const string UpdateAdditionalZoneClusterObject = "UpdateAdditionalZoneClusterObject";

            public const string UpdateMasterClusterName = "UpdateMasterClusterName";
            public const string UpdateMasterClusterObject = "UpdateMasterClusterObject";

            public const string UpdateMonitoringServiceClusterName = "UpdateMonitoringServiceClusterName";
            public const string UpdateMonitoringServiceClusterObject = "UpdateMonitoringServiceClusterObject";

            public const string AddonConfigsClusterName = "AddonConfigsClusterName";
            public const string AddonConfigsClusterObject = "AddonConfigsClusterObject";
        }

        /// <summary>
        /// This dynamic parameter dictionary is used by PowerShell to generate parameters dynamically.
        /// </summary>
        private RuntimeDefinedParameterDictionary _dynamicParameters;

        /// <summary>
        /// <para type="description">
        /// The project that the cluster belongs to.
        /// This parameter defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.UpdateNodePoolClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.UpdateAdditionalZoneClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.UpdateMasterClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.AddonConfigsClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.UpdateMonitoringServiceClusterName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone that the cluster belongs to.
        /// This parameter defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.UpdateNodePoolClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.UpdateAdditionalZoneClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.UpdateMasterClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.AddonConfigsClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.UpdateMonitoringServiceClusterName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of node pool in the cluster to be updated.
        /// This parameter is mandatory if you want to update NodeVersion, Autoscaling or ImageType of a cluster.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.UpdateNodePoolClusterName)]
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.UpdateNodePoolClusterObject)]
        [ValidateNotNullOrEmpty]
        public string NodePoolName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the cluster to be updated.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0,
            ParameterSetName = ParameterSetNames.UpdateNodePoolClusterName)]
        [Parameter(Mandatory = true, Position = 0,
            ParameterSetName = ParameterSetNames.UpdateAdditionalZoneClusterName)]
        [Parameter(Mandatory = true, Position = 0,
            ParameterSetName = ParameterSetNames.UpdateMasterClusterName)]
        [Parameter(Mandatory = true, Position = 0,
            ParameterSetName = ParameterSetNames.AddonConfigsClusterName)]
        [Parameter(Mandatory = true, Position = 0,
            ParameterSetName = ParameterSetNames.UpdateMonitoringServiceClusterName)]
        [ValidateNotNullOrEmpty]
        public string ClusterName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the cluster that the node pool belongs to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.UpdateNodePoolClusterObject)]
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.UpdateAdditionalZoneClusterObject)]
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.UpdateMasterClusterObject)]
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.AddonConfigsClusterObject)]
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.UpdateMonitoringServiceClusterObject)]
        [ValidateNotNull]
        public Cluster ClusterObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// The desired list of Google Compute Engine locations in which the cluster's nodes should be located.
        /// Changing the locations a cluster is in will result in nodes being either created or removed from
        /// the cluster, depending on whether locations are being added or removed. This list must always include
        /// the cluster's primary zone.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.UpdateAdditionalZoneClusterName)]
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.UpdateAdditionalZoneClusterObject)]
        public string[] AdditionalZone { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the cluster's master will be updated to the latest Kubernetes version.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.UpdateMasterClusterName)]
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.UpdateMasterClusterObject)]
        public SwitchParameter UpdateMaster { get; set; }

        /// <summary>
        /// <para type="description">
        /// This parameter is used to enable or disable HTTP load balancing in the cluster.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.AddonConfigsClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.AddonConfigsClusterObject)]
        public bool? LoadBalancing { get; set; }

        /// <summary>
        /// <para type="description">
        /// This parameter is used to enable or disable HorizontalPodAutoscaling in the cluster.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.AddonConfigsClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.AddonConfigsClusterObject)]
        public bool? HorizontalPodAutoscaling { get; set; }

        /// <summary>
        /// <para type="description">
        /// This parameter is used to set the monitoring service of the cluster.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.UpdateMonitoringServiceClusterName)]
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.UpdateMonitoringServiceClusterObject)]
        [ValidateSet("monitoring.googleapis.com", "none", IgnoreCase = true)]
        public string MonitoringService { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, a node pool in the cluster will have autoscaling enabled and this number will represent
        /// the maximum number of nodes that the node pool can scale to.
        /// If the cluster has more than 1 node pool, -NodePoolName is needed to determine
        /// which node pool the autoscaling will be applied to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.UpdateNodePoolClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.UpdateNodePoolClusterObject)]
        public int? MaximumNodesToScaleTo { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, a node pool in the cluster will have autoscaling enabled and this number will represent
        /// the minimum number of nodes that the node pool can scale to.
        /// If the cluster has more than 1 node pool, -NodePoolName is needed to determine
        /// which node pool the autoscaling will be applied to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.UpdateNodePoolClusterName)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.UpdateNodePoolClusterObject)]
        public int? MininumNodesToScaleTo { get; set; }

        /// <summary>
        /// Generate dynamic parameter -MachineType and -ImageType based on the value of -Project
        /// and -Zone. This will provide tab-completion for -MachineType and -ImageType parameters.
        /// </summary>
        public object GetDynamicParameters()
        {
            if (_dynamicParameters == null)
            {
                _dynamicParameters = new RuntimeDefinedParameterDictionary();

                // Try to resolve Project variable to a string, use default value from the SDK if we fail to do so.
                Project = GetCloudSdkSettingValue(CloudSdkSettings.CommonProperties.Project, Project);
                // Try to resolve Zone variable to a string, use default value from the SDK if we fail to do so.
                Zone = GetCloudSdkSettingValue(CloudSdkSettings.CommonProperties.Zone, Zone);

                PopulateDynamicParameters(Project, Zone);
            }

            return _dynamicParameters;
        }

        /// <summary>
        /// Populate -NodeVersion and -ImageType parameters.
        /// PowerShell doesn't seem to like it if we make these dynamic parameters the
        /// unique and mandatory parameter in a parameter set so I have to make them
        /// non-mandatory and have logic to separate the parameter set in
        /// UpdateNodePool.
        /// </summary>
        protected void PopulateDynamicParameters(string project, string zone)
        {
            // Gets all the valid image types of this zone and project combination.
            string[] imageTypes = GetImageTypes(Project, Zone);
            RuntimeDefinedParameter imageTypeParam = GenerateRuntimeParameter(
                parameterName: "ImageType",
                helpMessage: "The updated image type to used for a node pool in the cluster. If the cluster " +
                    "has more than 1 node pool, -NodePoolName is needed to determine which node pool " +
                    "the image type will be applied to.",
                validSet: imageTypes,
                parameterSetNames: new string[] { ParameterSetNames.UpdateNodePoolClusterName,
                                                  ParameterSetNames.UpdateNodePoolClusterObject});
            _dynamicParameters.Add("ImageType", imageTypeParam);

            // Gets all the valid node versions this zone and project combination.
            string[] nodeVersions = GetValidNodeVersions(Project, Zone);
            RuntimeDefinedParameter nodeVersionParam = GenerateRuntimeParameter(
                parameterName: "NodeVersion",
                helpMessage: "The Kubernetes version that a node pool in the cluster will use. If the cluster " +
                    "has more than 1 node pool, -NodePoolName is needed to determine which node pool " +
                    "the version will be applied to.",
                validSet: nodeVersions,
                parameterSetNames: new string[] { ParameterSetNames.UpdateNodePoolClusterName,
                                                  ParameterSetNames.UpdateNodePoolClusterObject});
            _dynamicParameters.Add("NodeVersion", nodeVersionParam);
        }

        protected override void ProcessRecord()
        {
            if (ClusterObject != null)
            {
                Zone = ClusterObject.Zone;
                ClusterName = ClusterObject.Name;
                Project = GetProjectNameFromUri(ClusterObject.SelfLink);
            }
            UpdateClusterRequest requestBody = BuildUpdateClusterRequest();

            ProjectsResource.ZonesResource.ClustersResource.UpdateRequest request =
                Service.Projects.Zones.Clusters.Update(requestBody, Project, Zone, ClusterName);
            Operation updateOperation = request.Execute();
            Cluster updatedCluster = WaitForClusterUpdate(updateOperation);

            WriteObject(updatedCluster);
        }

        /// <summary>
        /// Wait for the cluster update operation to complete.
        /// Use write progress to display the progress in the meantime.
        /// </summary>
        private Cluster WaitForClusterUpdate(Operation operation)
        {
            string activity = $"Updating cluster '{ClusterName}' in zone '{Zone}' of project '{Project}'.";
            string status = "Updating cluster";
            WaitForClusterOperation(operation, Project, Zone, activity, status);

            // Returns the cluster after it is created.
            ProjectsResource.ZonesResource.ClustersResource.GetRequest getClusterRequest =
                Service.Projects.Zones.Clusters.Get(Project, Zone, ClusterName);
            return getClusterRequest.Execute();
        }

        /// <summary>
        /// Constructs an UpdateClusterRequest based on selected ParameterSet.
        /// </summary>
        private UpdateClusterRequest BuildUpdateClusterRequest()
        {
            ClusterUpdate updateBody = new ClusterUpdate();

            switch (ParameterSetName)
            {
                case ParameterSetNames.AddonConfigsClusterName:
                case ParameterSetNames.AddonConfigsClusterObject:
                    UpdateAddonsConfig(updateBody);
                    break;
                case ParameterSetNames.UpdateNodePoolClusterName:
                case ParameterSetNames.UpdateNodePoolClusterObject:
                    UpdateNodePool(updateBody);
                    break;
                case ParameterSetNames.UpdateAdditionalZoneClusterName:
                case ParameterSetNames.UpdateAdditionalZoneClusterObject:
                    UpdateAdditionalZones(updateBody);
                    break;
                case ParameterSetNames.UpdateMasterClusterName:
                case ParameterSetNames.UpdateMasterClusterObject:
                    // This will update master to the latest version.
                    updateBody.DesiredMasterVersion = "-";
                    break;
                case ParameterSetNames.UpdateMonitoringServiceClusterName:
                case ParameterSetNames.UpdateMonitoringServiceClusterObject:
                    updateBody.DesiredMonitoringService = MonitoringService;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            var request = new UpdateClusterRequest() { Update = updateBody };
            return request;
        }

        private Cluster GetCluster()
        {
            try
            {
                ProjectsResource.ZonesResource.ClustersResource.GetRequest getRequest =
                    Service.Projects.Zones.Clusters.Get(Project, Zone, ClusterName);
                Cluster cluster = getRequest.Execute();
                return cluster;
            }
            catch (GoogleApiException apiEx) when (apiEx.HttpStatusCode == HttpStatusCode.NotFound)
            {
                throw new PSArgumentException($"Cluster '{ClusterName}' cannot be found in zone " +
                    $"'{Zone}' of project '{Project}'.");
            }
        }

        /// <summary>
        /// Helper function to update a NodePool's property in the cluster.
        /// Only 1 property (Autoscaling, NodeVersion or ImageType) can be updated
        /// per cmdlet invocation.
        /// </summary>
        private void UpdateNodePool(ClusterUpdate clusterUpdate)
        {
            string imageType = null;
            string nodeVersion = null;

            if (_dynamicParameters.ContainsKey("ImageType"))
            {
                imageType = _dynamicParameters["ImageType"].Value?.ToString().ToLower();
                if (!string.IsNullOrWhiteSpace(imageType))
                {
                    // Have to check that -NodeVersion and -Min/MaxNodesToScaleTo are not set
                    // because the cmdlet only does one update.
                    if (!string.IsNullOrWhiteSpace(nodeVersion) || MaximumNodesToScaleTo != null
                        || MininumNodesToScaleTo != null)
                    {
                        throw new PSArgumentException("When -ImageType is used, -NodeVersion" +
                            " and -Max/MinNodesToScaleTo cannot be used.");
                    }
                    clusterUpdate.DesiredImageType = imageType;
                    return;
                }
            }

            if (_dynamicParameters.ContainsKey("NodeVersion"))
            {
                nodeVersion = _dynamicParameters["NodeVersion"].Value?.ToString().ToLower();
                if (!string.IsNullOrWhiteSpace(nodeVersion))
                {
                    // If we reach here, then it is for sure that -ImageType is either null or
                    // empty so we don't have to check it.
                    if (MaximumNodesToScaleTo != null || MininumNodesToScaleTo != null)
                    {
                        throw new PSArgumentException("When -NodeVersion is used, -ImageType" +
                            " and -Max/MinNodesToScaleTo cannot be used.");
                    }

                    UpdateNodeVersion(clusterUpdate, nodeVersion);
                    return;
                }
            }

            if (MaximumNodesToScaleTo == null)
            {
                throw new PSArgumentException("Either -ImageType, -NodeVersion" +
                    " or -Max/MinNodesToScaleTo should be used when -NodePoolName is used.");
            }

            UpdateAutoScaling(clusterUpdate);
        }

        /// <summary>
        /// Helper function to set the DesiredNodeVersion in clusterUpdate
        /// based on the string nodeVersion. This function also performs check
        /// to make sure that the node version is less than the master version.
        /// </summary>
        private void UpdateNodeVersion(ClusterUpdate clusterUpdate, string nodeVersion)
        {
            // We have to make sure that the node version we are updating to is less than
            // that of the master.
            Version resolvedNodeVersion = null;
            if ("latest".Equals(nodeVersion, StringComparison.OrdinalIgnoreCase))
            {
                // Find the latest version from all valid node versions.
                string[] validNodeVersionStrings = GetValidNodeVersions(Project, Zone);
                foreach (string validNodeVersionString in validNodeVersionStrings)
                {
                    Version validNodeVersion;
                    if (Version.TryParse(validNodeVersionString, out validNodeVersion))
                    {
                        if (resolvedNodeVersion == null)
                        {
                            resolvedNodeVersion = validNodeVersion;
                        }
                        else if (resolvedNodeVersion < validNodeVersion)
                        {
                            resolvedNodeVersion = validNodeVersion;
                        }
                    }
                }
            }
            else
            {
                if (!Version.TryParse(nodeVersion, out resolvedNodeVersion))
                {
                    throw new PSArgumentException(
                        $"Node version '{nodeVersion}' is not a valid version.");
                }
            }

            if (resolvedNodeVersion != null)
            {
                Cluster cluster = GetCluster();
                Version masterVersion;
                if (Version.TryParse(cluster.CurrentMasterVersion, out masterVersion))
                {
                    if (masterVersion < resolvedNodeVersion)
                    {
                        throw new PSArgumentException("-NodeVersion cannot be greater than the" +
                            $" master version of the node which is '{masterVersion}'.");
                    }
                }
            }

            clusterUpdate.DesiredNodeVersion = nodeVersion;
        }

        /// <summary>
        /// Set DesiredLocations of the clusterUpdate.
        /// Since the primary zone also has to be included in the additional zones,
        /// we add that to the additional zones list if it is not already there.
        /// </summary>
        private void UpdateAdditionalZones(ClusterUpdate clusterUpdate)
        {
            List<string> additionalZones = AdditionalZone.Select(zone => zone.ToLower()).ToList();
            if (!additionalZones.Contains(Zone.ToLower()))
            {
                additionalZones.Add(Zone.ToLower());
            }

            clusterUpdate.DesiredLocations = additionalZones;
        }

        /// <summary>
        /// Set DesiredNodePoolAutoscaling of the clusterUpdate based on MaximumNodesToScaleTo and MinimumNodesToScaleTo.
        /// </summary>
        private void UpdateAutoScaling(ClusterUpdate clusterUpdate)
        {
            NodePoolAutoscaling autoscaling =
                GkeNodePoolConfigCmdlet.BuildAutoscaling(MaximumNodesToScaleTo, MininumNodesToScaleTo);
            clusterUpdate.DesiredNodePoolAutoscaling = autoscaling;
        }

        /// <summary>
        /// Set DesiredAddonsConfig of the clusterUpdate based on LoadBalancing and HorizontalPodAutoscaling.
        /// </summary>
        private void UpdateAddonsConfig(ClusterUpdate clusterUpdate)
        {
            if (!LoadBalancing.HasValue && !HorizontalPodAutoscaling.HasValue)
            {
                throw new PSArgumentException(
                    "Either -LoadBalancing or -HorizontalPodAutoscaling has to be set.");
            }

            var addons = new AddonsConfig();
            if (LoadBalancing.HasValue)
            {
                addons.HttpLoadBalancing = new HttpLoadBalancing { Disabled = !LoadBalancing };
            }

            if (HorizontalPodAutoscaling.HasValue)
            {
                addons.HorizontalPodAutoscaling =
                    new HorizontalPodAutoscaling { Disabled = !HorizontalPodAutoscaling };
            }

            clusterUpdate.DesiredAddonsConfig = addons;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes a Google Container Cluster.
    /// </para>
    /// <para type="description">
    /// Removes a Google Container Cluster. You can either pass in a cluster object (from Get-GkeCluster cmdlet)
    /// or use -Cluster, -Project and -Zone parameters (if -Project and/or -Zone parameters are not used,
    /// the cmdlet will use the default project and/or default zone).
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> Remove-GkeCluster -ClusterName "my-cluster" `
    ///                             -Zone "us-west1-b"
    ///   </code>
    ///   <para>Removes the cluster "my-cluster" in the zone "us-west1-b" of the default project.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> $cluster = Get-GkeCluster -ClusterName "my-cluster"
    ///   PS C:\> Remove-GkeCluster -InputObject $cluster
    ///   </code>
    ///   <para>
    ///   Removes the cluster "my-cluster" by using the cluster object returned from Get-GkeCluster.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Get-GkeCluster -Zone "us-west1-b" | Remove-GkeCluster
    ///   </code>
    ///   <para>
    ///   Removes all clusters in zone "us-west1-b" of the default project by pipelining.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/container-engine/docs/clusters/)">
    /// [Container Clusters]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GkeCluster", SupportsShouldProcess = true)]
    public class RemoveGkeClusterCmdlet : GkeCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that the container clusters belong to.
        /// This parameter defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone that the container clusters belong to.
        /// This parameter defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the container cluster to be removed.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByName)]
        [Alias("Name")]
        public string ClusterName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The cluster object to be removed.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByObject,
            ValueFromPipeline = true)]
        public Cluster InputObject { get; set; }

        protected override void ProcessRecord()
        {
            if (ParameterSetName == ParameterSetNames.ByObject)
            {
                Zone = InputObject.Zone;
                ClusterName = InputObject.Name;
                Project = GetProjectNameFromUri(InputObject.SelfLink);
            }

            ProjectsResource.ZonesResource.ClustersResource.DeleteRequest getRequest =
                Service.Projects.Zones.Clusters.Delete(Project, Zone, ClusterName);
            if (ShouldProcess($"Cluster '{ClusterName}' in zone '{Zone}' of project '{Project}'.",
                "Removing GKE Cluster"))
            {
                try
                {
                    Operation deleteOperation = getRequest.Execute();
                    WaitForClusterDeletion(deleteOperation);
                }
                catch (GoogleApiException apiEx) when (apiEx.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    WriteResourceMissingError(
                        $"Cluster '{ClusterName}' in zone '{Zone}' of project '{Project}' cannot be found.",
                        "ClusterNotFound",
                        ClusterName);
                }
            }
        }

        /// <summary>
        /// Wait for the cluster deletion operation to complete.
        /// Use write progress to display the progress in the meantime.
        /// </summary>
        private void WaitForClusterDeletion(Operation operation)
        {
            string activity = $"Deleting cluster '{ClusterName}' in zone '{Zone}' of project '{Project}'.";
            string status = "Deleting cluster";
            WaitForClusterOperation(operation, Project, Zone, activity, status);
        }
    }
}
