// Copyright 2015-2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// Base class for Google Compute Engine-based cmdlets. 
    /// </summary>
    public abstract class GceCmdlet : GCloudCmdlet
    {
        // The Servcie for the Google Compute API
        protected ComputeService Service => _service.Value;

        internal static ComputeService OptionalComputeService { private get; set; }

        // The value of the progress bar when being used as a spinner. If the operation does not give valid
        // percent complete feedback, this spinner will move from 0 to 99 and then repeat.
        private int _spinnerValue = 0;

        private readonly Lazy<ComputeService> _service = new Lazy<ComputeService>(() => OptionalComputeService ??
            new ComputeService(GetBaseClientServiceInitializer()));

        /// <summary>
        /// Waits for the provided zone operation to complete. This way cmdlets can return newly
        /// created objects once they are finished being created, rather than returning thunks.
        /// 
        /// Will throw an exception if the operation fails for any reason.
        /// </summary>
        protected void WaitForZoneOperation(string project, string zone, Operation op, string progressMessage = null)
        {
            WriteWarnings(op);

            while (op.Status != "DONE" && !Stopping)
            {
                Thread.Sleep(150);
                ZoneOperationsResource.GetRequest getReq = Service.ZoneOperations.Get(project, zone, op.Name);
                op = getReq.Execute();
                WriteWarnings(op);
                WriteProgress(op, progressMessage);
            }
            WriteProgressComplete(op);

            if (op.Error != null)
            {
                throw new GoogleComputeOperationException(op.Error);
            }
        }

        /// <summary>
        /// Waits for the provided region operation to complete. This way cmdlets can return newly
        /// created objects once they are finished being created, rather than returning thunks.
        /// 
        /// Will throw an exception if the operation fails for any reason.
        /// </summary>
        protected void WaitForRegionOperation(string project, string region, Operation op, string progressMessage = null)
        {
            WriteWarnings(op);

            while (op.Status != "DONE" && !Stopping)
            {
                Thread.Sleep(150);
                RegionOperationsResource.GetRequest getReq =
                    Service.RegionOperations.Get(project, region, op.Name);
                op = getReq.Execute();
                WriteWarnings(op);
                WriteProgress(op, progressMessage);
            }

            WriteProgressComplete(op);

            if (op.Error != null)
            {
                throw new GoogleComputeOperationException(op.Error);
            }
        }

        /// <summary>
        /// Waits for the provided global operation to complete. This way cmdlets can return newly
        /// created objects once they are finished being created, rather than returning thunks.
        /// 
        /// Will throw an exception if the operation fails for any reason.
        /// </summary>
        protected void WaitForGlobalOperation(string project, Operation operation, string progressMessage = null)
        {
            WriteWarnings(operation);
            while (operation.Status != "DONE" && !Stopping)
            {
                Thread.Sleep(150);
                operation = Service.GlobalOperations.Get(project, operation.Name).Execute();
                WriteWarnings(operation);
                WriteProgress(operation, progressMessage);
            }

            WriteProgressComplete(operation);

            if (operation.Error != null)
            {
                throw new GoogleComputeOperationException(operation.Error);
            }
        }

        private void WriteWarnings(Operation op)
        {
            if (op.Warnings != null)
            {
                foreach (Operation.WarningsData warning in op.Warnings)
                {
                    WriteWarning(warning.Message);
                }
            }
        }

        /// <summary>
        /// Writes a progress record to give feedback to the user.
        /// </summary>
        /// <param name="op">The operation we are waiting on.</param>
        /// <param name="progressMessage">The custom message to write to the progress bar. If null, this
        ///  method will generate a message from the operation.</param>
        private void WriteProgress(Operation op, string progressMessage)
        {
            // ProgressRecord was throwing an error on negative activityIds.
            int activityId = Math.Abs(op.Id.GetHashCode());
            string activity = progressMessage ?? op.Description ?? BuildActivity(op);
            string statusDescription = op.StatusMessage ?? op.Status;
            int percentComplete;
            if (op.Progress == null || op.Progress == 0)
            {
                percentComplete = _spinnerValue = (_spinnerValue + 1) % 100;
            }
            else
            {
                percentComplete = (int) op.Progress;
            }
            ProgressRecord record = new ProgressRecord(activityId, activity, statusDescription)
            {
                RecordType = ProgressRecordType.Processing,
                PercentComplete = percentComplete,
            };
            WriteProgress(record);
        }

        /// <summary>
        /// Builds an activity. This is displayed on the top line of the progress bar.
        /// </summary>
        /// <param name="op">The operation to build an activity from.</param>
        /// <returns>The type and target of the operation </returns>
        private static string BuildActivity(Operation op)
        {
            // Capitalize the operation type.
            string operationType = op.OperationType.Substring(0, 1).ToUpper()
                                   + op.OperationType.Substring(1);

            // Cut the target uri down to the interesting data.
            string target = op.TargetLink;
            const string baseUri = "https://www.googleapis.com/compute/v1/";
            if (target.StartsWith(baseUri))
            {
                target = target.Substring(baseUri.Length);
            }

            return $"{operationType} {target}";
        }

        /// <summary>
        /// Closes the progress bar of the operation.
        /// </summary>
        /// <param name="op">The operation the progress bar was created for.</param>
        private void WriteProgressComplete(Operation op)
        {
            int activityId = Math.Abs(op.Id.GetHashCode());
            string activity = op.Description ?? op.OperationType ?? BuildActivity(op);
            string statusDescription = op.StatusMessage ?? op.Status;
            ProgressRecord record = new ProgressRecord(activityId, activity, statusDescription)
            {
                RecordType = ProgressRecordType.Completed,
            };
            WriteProgress(record);
            _spinnerValue = 0;
        }

        /// <summary>
        /// Library method to pull the name of a zone from a uri.
        /// </summary>
        /// <param name="uri">
        /// A uri that includes the zone.
        /// </param>
        /// <returns>
        /// The name of the zone part of the uri.
        /// </returns>
        public static string GetZoneNameFromUri(string uri)
        {
            return GetUriPart("zones", uri);
        }

        /// <summary>
        /// Library method to pull the name of a region from a URI.
        /// </summary>
        /// <param name="uri">
        /// A URI that includes the region.
        /// </param>
        /// <returns>
        /// The name of the region part of the URI.
        /// </returns>
        public static string GetRegionNameFromUri(string uri)
        {
            return GetUriPart("regions", uri);
        }

        /// <summary>
        /// Constructs network identifier. If networkName is an absolute URI, returns the URI.
        /// If networkName is empty, we will use the default network.
        /// If project is empty, we will use the default project.
        /// We will use the networkName and project's name to construct a network identifier and returns that.
        /// </summary>
        public static string ConstructNetworkName(string networkName, string project)
        {
            if (Uri.IsWellFormedUriString(networkName, UriKind.Absolute))
            {
                return networkName;
            }

            if (string.IsNullOrWhiteSpace(networkName))
            {
                networkName = "default";
            }

            if (string.IsNullOrWhiteSpace(project) && !networkName.StartsWith("global/networks/"))
            {
                networkName = $"global/networks/{networkName}";
            }
            else if (!string.IsNullOrWhiteSpace(project)
                && !networkName.StartsWith($"projects/{project}/global/networks/"))
            {
                networkName = $"projects/{project}/global/networks/{networkName}";
            }

            return networkName;
        }
    }

    /// <summary>
    /// This class is used write cmdlet that run concurrent Gce operations. A class inheriting this should add
    /// ongoing operations to the operations field in the BeginProcessing() and ProcessRecord() methods. These
    /// operations will then be waited on in the EndProccessing() method. If a child class requires its own
    /// EndProcessing(), it must call the base.EndProcessing() at some point.
    /// </summary>
    public abstract class GceConcurrentCmdlet : GceCmdlet
    {
        /// <summary>
        /// Container class for all information needed to wait on a zone operation.
        /// </summary>
        private class LocalOperation
        {
            public string Project { get; }
            public string Local { get; }
            public Operation Operation { get; }

            /// <summary>
            /// The action executed when the operation is complete.
            /// </summary>
            public Action Callback { get; }

            public LocalOperation(string project, string local, Operation operation)
                : this(project, local, operation, () => { })
            { }

            public LocalOperation(string project, string local, Operation operation, Action callback)
            {
                Project = project;
                Local = local;
                Operation = operation;
                Callback = callback;
            }
        }

        /// <summary>
        /// Container class for all information needed to wait on a gobal operation.
        /// </summary>
        private class GlobalOperation
        {
            public string Project { get; }
            public Operation Operation { get; }

            /// <summary>
            /// The action executed when the operation is complete.
            /// </summary>
            public Action Callback { get; }

            public GlobalOperation(string project, Operation operation)
                : this(project, operation, () => { })
            { }

            public GlobalOperation(string project, Operation operation, Action callback)
            {
                Project = project;
                Operation = operation;
                Callback = callback;
            }
        }

        /// <summary>
        /// A place to store in progress operations to be waitied on in EndProcessing().
        /// </summary>
        private IList<LocalOperation> _zoneOperations = new List<LocalOperation>();
        private IList<LocalOperation> _regionOperations = new List<LocalOperation>();
        private IList<GlobalOperation> _globalOperations = new List<GlobalOperation>();

        /// <summary>
        /// Used by child classes to add zone operations to wait on.
        /// </summary>
        /// <param name="project">The name of the Google Cloud project.</param>
        /// <param name="zone">The name of the zone.</param>
        /// <param name="operation">The Operation object to wait on.</param>
        protected void AddZoneOperation(string project, string zone, Operation operation)
        {
            _zoneOperations.Add(new LocalOperation(project, zone, operation));
        }

        /// <summary>
        /// Used by child classes to add zone operations to wait on.
        /// </summary>
        /// <param name="project">The name of the Google Cloud project.</param>
        /// <param name="zone">The name of the zone.</param>
        /// <param name="operation">The Operation object to wait on.</param>
        /// <param name="callback">The action to call when the operation is complete.</param>
        protected void AddZoneOperation(string project, string zone, Operation operation, Action callback)
        {
            _zoneOperations.Add(new LocalOperation(project, zone, operation, callback));
        }

        /// <summary>
        /// Used by child classes to add region operations to wait on.
        /// </summary>
        /// <param name="project">The name of the Google Cloud project.</param>
        /// <param name="region">The name of the region.</param>
        /// <param name="operation">The Operation object to wait on.</param>
        protected void AddRegionOperation(string project, string region, Operation operation)
        {
            _regionOperations.Add(new LocalOperation(project, region, operation));
        }

        /// <summary>
        /// Used by child classes to add region operations to wait on.
        /// </summary>
        /// <param name="project">The name of the Google Cloud project.</param>
        /// <param name="region">The name of the region.</param>
        /// <param name="operation">The Operation object to wait on.</param>
        /// <param name="callback">The action to call when the operation is complete.</param>
        protected void AddRegionOperation(string project, string region, Operation operation, Action callback)
        {
            _regionOperations.Add(new LocalOperation(project, region, operation, callback));
        }

        /// <summary>
        /// Used by child classes to add global operations to wait on.
        /// </summary>
        /// <param name="project">The name of the Google Cloud project</param>
        /// <param name="operation">The Operation object to wait on.</param>
        protected void AddGlobalOperation(string project, Operation operation)
        {
            _globalOperations.Add(new GlobalOperation(project, operation));
        }

        /// <summary>
        /// Used by child classes to add global operations to wait on.
        /// </summary>
        /// <param name="project">The name of the Google Cloud project</param>
        /// <param name="operation">The Operation object to wait on.</param>
        /// <param name="callback">The action to call when the operation is complete.</param>
        protected void AddGlobalOperation(string project, Operation operation, Action callback)
        {
            _globalOperations.Add(new GlobalOperation(project, operation, callback));
        }

        /// <summary>
        /// Waits on all the operations started by this cmdlet.
        /// </summary>
        protected override void EndProcessing()
        {
            var exceptions = new List<Exception>();
            foreach (LocalOperation operation in _zoneOperations)
            {
                try
                {
                    WaitForZoneOperation(operation.Project, operation.Local, operation.Operation);
                    operation.Callback();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            foreach (LocalOperation operation in _regionOperations)
            {
                try
                {
                    WaitForRegionOperation(operation.Project, operation.Local, operation.Operation);
                    operation.Callback();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            foreach (GlobalOperation operation in _globalOperations)
            {
                try
                {
                    WaitForGlobalOperation(operation.Project, operation.Operation);
                    operation.Callback();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            if (exceptions.Count > 1)
            {
                throw new AggregateException(exceptions);
            }
            else if (exceptions.Count == 1)
            {
                throw exceptions.First();
            }
            base.EndProcessing();
        }
    }
}
