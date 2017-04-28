// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.PowerShell.Common;
using Google.Apis.Container.v1;
using System;
using System.Management.Automation;
using System.Threading;
using Google.Apis.Container.v1.Data;

namespace Google.PowerShell.Container
{
    /// <summary>
    /// Base class for Google Container Engine cmdlets.
    /// </summary>
    public class GkeCmdlet : GCloudCmdlet
    {
        // Service for Google Container API.
        public ContainerService Service { get; private set; }

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
                Thread.Sleep(200);
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
