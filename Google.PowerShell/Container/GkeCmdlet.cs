// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Container.v1;
using Google.Apis.Container.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using ComputeService = Google.Apis.Compute.v1.ComputeService;

namespace Google.PowerShell.Container
{
    /// <summary>
    /// Base class for Google Container Engine cmdlets.
    /// </summary>
    public class GkeCmdlet : GCloudCmdlet
    {
        // Service for Google Container API.
        public ContainerService Service { get; private set; }

        // IAM Service used for getting roles that can be granted to a project.
        private Lazy<ComputeService> _computeService =
            new Lazy<ComputeService>(() => new ComputeService(GetBaseClientServiceInitializer()));

        /// <summary>
        /// Dictionary of image types with key as as tuple of project and zone
        /// and value as the image types available in the project's zone.
        /// This dictionary is used for caching the various image types available in a project's zone.
        /// TODO(quoct): Add timeout for these caches.
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
        /// Dictionary of valid node versions with key as as tuple of project and zone
        /// and value as the valid node versions in the project's zone.
        /// This dictionary is used for caching the valid cluster versions in a project's zone.
        /// </summary>
        private static ConcurrentDictionary<Tuple<string, string>, string[]> s_validNodeVersions =
            new ConcurrentDictionary<Tuple<string, string>, string[]>();

        public GkeCmdlet()
        {
            Service = new ContainerService(GetBaseClientServiceInitializer());
        }

        /// <summary>
        /// Wait for the cluster creation operation to complete.
        /// Use write progress to display the progress (with activity and status string supplied).
        /// </summary>
        protected void WaitForClusterOperation(Operation operation, string project, string zone, string activity, string status)
        {
            int activityId = (new Random()).Next();
            int percentage = 0;
            ContainerOperationStatus operationStatus = ContainerOperationStatus.RUNNING;

            while (operationStatus != ContainerOperationStatus.DONE)
            {
                // We don't want to have a sleep interval too short since that will
                // send many requests to the server and this may cause some API quotas
                // to be hit.
                Thread.Sleep(1000);
                var progressRecord = new ProgressRecord(activityId, activity, status);
                // Since we don't know how long the operation will take, we will just make the progress
                // bar loop through.
                percentage = (percentage + 1) % 100;
                progressRecord.PercentComplete = percentage;
                WriteProgress(progressRecord);
                ProjectsResource.ZonesResource.OperationsResource.GetRequest getRequest =
                    Service.Projects.Zones.Operations.Get(project, zone, operation.Name);
                operation = getRequest.Execute();
                Enum.TryParse(operation.Status, out operationStatus);
            }

            var progressCompleteRecord = new ProgressRecord(activityId, activity, status);
            progressCompleteRecord.RecordType = ProgressRecordType.Completed;
            progressCompleteRecord.PercentComplete = 100;
            WriteProgress(progressCompleteRecord);
        }

        /// <summary>
        /// Returns all the possible node versions in a given zone in a given project.
        /// </summary>
        protected string[] GetValidNodeVersions(string project, string zone)
        {
            Tuple<string, string> key = new Tuple<string, string>(project, zone);
            if (!s_validNodeVersions.ContainsKey(key))
            {
                try
                {
                    ProjectsResource.ZonesResource.GetServerconfigRequest getConfigRequest =
                        Service.Projects.Zones.GetServerconfig(project, zone);
                    ServerConfig config = getConfigRequest.Execute();

                    config.ValidNodeVersions.Add("latest");
                    s_validNodeVersions[key] = config.ValidNodeVersions.ToArray();
                }
                catch
                {
                    // Just swallow error and don't provide tab completion for -ImageType.
                    s_validNodeVersions[key] = new string[] { };
                }
            }
            return s_validNodeVersions[key];
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
    }

    /// <summary>
    /// The status of the container operation.
    /// </summary>
    public enum ContainerOperationStatus
    {
        PENDING,
        RUNNING,
        DONE
    }
}
