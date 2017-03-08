// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.PowerShell.Common;
using Google.Apis.Container.v1;
using Google.Apis.Container.v1.Data;
using System.Management.Automation;
using System.Collections.Generic;
using System.Net;
using System.Linq;

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
}
