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
        // IAM Service used for getting roles that can be granted to a project.
        private Lazy<ComputeService> _computeService =
            new Lazy<ComputeService>(() => new ComputeService(GetBaseClientServiceInitializer()));

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

        /// <para type="description">
        /// The Google Cloud Platform Service Account to be used by the node VMs.
        /// Use New-GceServiceAccountConfig to create the service account and appropriate scopes.
        /// </para>
        [Parameter(Mandatory = false)]
        public virtual Apis.Compute.v1.Data.ServiceAccount ServiceAccount { get; set; }

        /// <para type="description">
        /// If set, every node created will be a preemptible VM instance.
        /// </para>
        [Parameter(Mandatory = false)]
        public virtual SwitchParameter Preemptible { get; set; }

        /// <summary>
        /// Dictionary of image types with key as as tuple of project and zone
        /// and value as the image types available in the project's zone.
        /// This dictionary is used for caching the various image types available in a project's zone.
        /// </summary>
        private static ConcurrentDictionary<Tuple<string, string>, string[]> s_imageTypesDictionary =
            new ConcurrentDictionary<Tuple<string, string>, string[]>();

        /// <summary>
        /// Dictionary of image types with key as as tuple of project and zone
        /// and value as the machine types available in the project's zone.
        /// This dictionary is used for caching the various machine types available in a project's zone.
        /// </summary>
        private static ConcurrentDictionary<Tuple<string, string>, string[]> s_machineTypesDictionary =
            new ConcurrentDictionary<Tuple<string, string>, string[]>();

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
        /// Generate a RuntimeDefinedParameter based on the parameter name,
        /// the help message and the valid set of parameter values.
        /// </summary>
        protected RuntimeDefinedParameter GenerateRuntimeParameter(
            string parameterName,
            string helpMessage,
            string[] validSet,
            string parameterSetName = null)
        {
            ParameterAttribute paramAttribute = new ParameterAttribute()
            {
                Mandatory = false,
                HelpMessage = helpMessage
            };
            if (parameterSetName != null)
            {
                paramAttribute.ParameterSetName = parameterSetName;
            }
            List<Attribute> attributeLists = new List<Attribute>() { paramAttribute };

            if (validSet.Length != 0)
            {
                var validateSetAttribute = new ValidateSetAttribute(validSet);
                validateSetAttribute.IgnoreCase = true;
                attributeLists.Add(validateSetAttribute);
            }

            Collection<Attribute> attributes = new Collection<Attribute>(attributeLists);
            return new RuntimeDefinedParameter(parameterName, typeof(string), attributes);
        }

        /// <summary>
        /// Returns all the possible image types in a given zone in a given project.
        /// </summary>
        protected string[] GetImageTypes(string project, string zone)
        {
            Tuple<string, string> key = new Tuple<string, string>(project, zone);
            if (!s_imageTypesDictionary.ContainsKey(key))
            {
                try
                {
                    ProjectsResource.ZonesResource.GetServerconfigRequest getConfigRequest =
                        Service.Projects.Zones.GetServerconfig(project, zone);
                    ServerConfig config = getConfigRequest.Execute();

                    s_imageTypesDictionary[key] = config.ValidImageTypes.ToArray();
                }
                catch
                {
                    // Just swallow error and don't provide tab completion for -ImageType.
                    s_imageTypesDictionary[key] = new string[] { };
                }
            }
            return s_imageTypesDictionary[key];
        }


        /// <summary>
        /// Returns all the possible machine types in a given zone in a given project.
        /// </summary>
        protected string[] GetMachineTypes(string project, string zone)
        {
            Tuple<string, string> key = new Tuple<string, string>(project, zone);
            if (!s_machineTypesDictionary.ContainsKey(key))
            {
                List<string> machineTypes = new List<string>();
                try
                {
                    Apis.Compute.v1.MachineTypesResource.ListRequest listRequest =
                        _computeService.Value.MachineTypes.List(project, zone);
                    do
                    {
                        Apis.Compute.v1.Data.MachineTypeList response = listRequest.Execute();
                        if (response.Items != null)
                        {
                            machineTypes.AddRange(response.Items.Select(machineType => machineType.Name));
                        }
                        listRequest.PageToken = response.NextPageToken;
                    }
                    while (listRequest.PageToken != null);
                }
                catch
                {
                    // Just swallow error.
                }
                s_machineTypesDictionary[key] = machineTypes.ToArray();
            }
            return s_machineTypesDictionary[key];
        }

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
                ImageType = SelectedImageType
            };

            if (Label != null)
            {
                nodeConfig.Labels = ConvertToDictionary<string, string>(Label);
            }

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

        /// <summary>
        /// Helper function to build a NodePool object.
        /// </summary>
        protected NodePool BuildNodePool(string name, NodeConfig config, int? initialNodeCount, bool autoUpgrade,
            bool autoScaling, int? minimumNodesToScaleTo, int? maximumNodesToScaleTo)
        {
            var nodePool = new NodePool()
            {
                Name = name,
                InitialNodeCount = initialNodeCount ?? 1,
                Config = config
            };

            if (autoScaling)
            {
                var scaling = new NodePoolAutoscaling() { Enabled = true };
                if (maximumNodesToScaleTo == null)
                {
                    throw new PSArgumentException(
                        "When using -EnableAutoScaling, please specify the maximum number of nodes that the node pool "
                        + "can scale up to with -MaximumNodesToScaleTo (make sure you have enough quota).");
                }

                if (minimumNodesToScaleTo == null)
                {
                    minimumNodesToScaleTo = 1;
                }

                if (scaling.MaxNodeCount < minimumNodesToScaleTo)
                {
                    throw new PSArgumentException(
                        "Maximum node count in a node pool has to be greater or equal to the minimum count.");
                }

                scaling.MaxNodeCount = maximumNodesToScaleTo;
                scaling.MinNodeCount = minimumNodesToScaleTo;
                nodePool.Autoscaling = scaling;
            }

            if (autoUpgrade)
            {
                var nodeManagement = new NodeManagement() { AutoUpgrade = true };
                nodePool.Management = nodeManagement;
            }

            return nodePool;
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
    ///                          -EnableAutoScaling `
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
    public class AddGkeClusterCmdlet : GkeNodeConfigCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByNodeConfig = "ByNodeConfig";
            public const string ByNodePool = "ByNodePool";
            public const string ByNodeConfigValues = "ByNodeConfigValues";
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

        /// <para type="description">
        /// If set, every node created in the cluster will be a preemptible VM instance.
        /// </para>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public override SwitchParameter Preemptible { get; set; }

        /// <para type="description">
        /// The name of the cluster.
        /// Name has to start with a letter, end with a number or letter
        /// and consists only of letters, numbers and hyphens.
        /// </para>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidatePattern("[a-zA-Z][a-zA-Z0-9-]*[a-zA-Z0-9]")]
        public string ClusterName { get; set; }

        /// <para type="description">
        /// The description of the cluster.
        /// </para>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Description { get; set; }

        /// <para type="description">
        /// The number of nodes to create in the cluster.
        /// </para>
        [Parameter(Mandatory = false)]
        [ValidateRange(0, int.MaxValue)]
        public int? InitialNodeCount { get; set; }

        /// <para type="description">
        /// The credential to access the master endpoint.
        /// </para>
        [Parameter(Mandatory = false)]
        [ValidateNotNull]
        public PSCredential MasterCredential { get; set; }

        /// <para type="description">
        /// Stop the cluster from using Google Cloud Logging Service to write logs.
        /// </para>
        [Parameter(Mandatory = false)]
        public SwitchParameter DisableLoggingService { get; set; }

        /// <para type="description">
        /// Stop the cluster from using Google Cloud Monitoring service to write metrics.
        /// </para>
        [Parameter(Mandatory = false)]
        public SwitchParameter DisableMonitoringService { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter DisableHttpLoadBalancing { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter DisableHorizontalPodAutoscaling { get; set; }

        /// <para type="description">
        /// Enables Kubernetes alpha features on the cluster. This includes alpha API groups
        /// and features that may not be production ready in the kubernetes version of the master and nodes.
        /// The cluster has no SLA for uptime and master/node upgrades are disabled.
        /// Alpha enabled clusters are AUTOMATICALLY DELETED thirty days after creation.
        /// </para>
        [Parameter(Mandatory = false)]
        public SwitchParameter EnableKubernetesAlpha { get; set; }

        /// <para type="description">
        /// If set, nodes in the cluster will be automatically upgraded.
        /// </para>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfig)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public SwitchParameter EnableAutoUpgrade { get; set; }

        /// <para type="description">
        /// If set, the cluster autoscaler will adjust the size of each node pool to the cluster usage.
        /// </para>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfig)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public SwitchParameter EnableAutoScaling { get; set; }

        /// <para type="description">
        /// Used with -EnableAutoScaling switch to set the minimum number of nodes in the node pool
        /// while autoscaling. Default to 1.
        /// </para>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfig)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public int? MininumNodesToScaleTo { get; set; }

        /// <para type="description">
        /// Used with -EnableAutoScaling switch to set the maximum number of nodes in the node pool
        /// while autoscaling (there has to be enough quota to scale up the cluster).
        /// </para>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfig)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public int? MaximumNodesToScaleTo { get; set; }

        /// <para type="description">
        /// Name of the Google Compute Engine network to which the cluster is connected.
        /// If left unspecified, the default network will be used.
        /// </para>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Network { get; set; }

        /// <para type="description">
        /// The name of the Google Compute Engine subnetwork to which the cluster is connected.
        /// </para>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Subnetwork { get; set; }

        /// <para type="description">
        /// The IP address range of the container pods in this cluster, in CIDR notation.
        /// </para>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string ClusterIpv4AddressRange { get; set; }

        /// <para type="description">
        /// The zones (in addition to the zone specified by -Zone parameter) in which
        /// the cluster's nodes should be located.
        /// </para>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string[] AdditionalZone { get; set; }

        /// <para type="description">
        /// Passed in a NodeConfig object containing configuration for the nodes in this cluster.
        /// This object can be created with New-GkeNodeConfig cmdlet.
        /// </para>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByNodeConfig, ValueFromPipeline = true)]
        public NodeConfig NodeConfig { get; set; }

        /// <para type="description">
        /// The number of node pools that the cluster will have. All the node pools will have the same config.
        /// </para>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfig)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByNodeConfigValues)]
        public int? NumberOfNodePools { get; set; }

        /// <para type="description">
        /// The node pools associated with this cluster.
        /// </para>
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
                parameterSetName: ParameterSetNames.ByNodeConfigValues,
                validSet: machineTypes);
            dynamicParamDict.Add("MachineType", machineTypeParam);

            // Gets all the valid image types of this zone and project combination.
            string[] imageTypes = GetImageTypes(Project, Zone);
            RuntimeDefinedParameter imageTypeParam = GenerateRuntimeParameter(
                parameterName: "ImageType",
                helpMessage: "The image type to use for node in this cluster.",
                parameterSetName: ParameterSetNames.ByNodeConfigValues,
                validSet: imageTypes);
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
                // By default, if there is 1 NodePool, GKE creates a NodePool with the name "default-pool".
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
                    EnableAutoScaling, MininumNodesToScaleTo, MaximumNodesToScaleTo);
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
