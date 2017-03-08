// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
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
                        exceptionMessage: $"Cluster '{cluster}' cannot be found in zone '{zone}' of project '{Project}'",
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
        /// The list of instance tags applied to each nodes.
        /// Tags are used to identify valid sources or targets for network firewalls.
        /// </para>
        [Parameter(Mandatory = false)]
        public virtual string[] Tags { get; set; }

        /// <para type="description">
        /// The Google Cloud Platform Service Account to be used by the node VMs.
        /// Use New-GceServiceAccountConfig to create the service account and appropriate scopes.
        /// </para>
        [Parameter(Mandatory = false)]
        public virtual ServiceAccount ServiceAccount { get; set; }

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
                        MachineTypeList response = listRequest.Execute();
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
                /// Metadata key/value pairs assigned to instances in the cluster.
                /// Keys must conform to the regexp [a-zA-Z0-9-_]+ and not conflict with any other
                /// metadata keys for the project or be one of the four reserved keys: "instance-template",
                /// "kube-env", "startup-script" and "user-data".
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
    public class NewGkeNodeConfig : GkeNodeConfigCmdlet
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
}
