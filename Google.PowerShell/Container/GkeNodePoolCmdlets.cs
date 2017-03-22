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
        /// <summary>
        /// <para type="description">
        /// The project that the node pool's cluster is in.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the node pool's cluster is in.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Zone)]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the cluster that the node pool belongs to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
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

        protected override void ProcessRecord()
        {
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
                Service.Projects.Zones.Clusters.NodePools.List(Project, Zone, ClusterName);
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
}
