// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using System.Threading;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// Base class for Google Compute Engine-based cmdlets. 
    /// </summary>
    public abstract class GceCmdlet : GCloudCmdlet
    {
        // The Servcie for the Google Compute API
        public ComputeService Service { get; }

        protected GceCmdlet() : this(null)
        {
        }

        protected GceCmdlet(ComputeService service)
        {
            Service = service ?? new ComputeService(GetBaseClientServiceInitializer());
        }

        /// <summary>
        /// Waits for the provided zone operation to complete. This way cmdlets can return newly
        /// created objects once they are finished being created, rather than returning thunks.
        /// 
        /// Will throw an exception if the operation fails for any reason.
        /// </summary>
        protected void WaitForZoneOperation(string project, string zone, Operation op)
        {
            WriteWarnings(op);

            while (op.Status != "DONE" && !Stopping)
            {
                Thread.Sleep(150);
                ZoneOperationsResource.GetRequest getReq = Service.ZoneOperations.Get(project, zone, op.Name);
                op = getReq.Execute();
                WriteWarnings(op);
            }

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
        protected void WaitForGlobalOperation(string project, Operation operation)
        {
            WriteWarnings(operation);
            while (operation.Status != "DONE" && !Stopping)
            {
                Thread.Sleep(150);
                operation = Service.GlobalOperations.Get(project, operation.Name).Execute();
                WriteWarnings(operation);
            }

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
        /// Library method to pull the name of a project from a uri.
        /// </summary>
        /// <param name="uri">
        /// The uri that includes the project.
        /// </param>
        /// <returns>
        /// The name of the project.
        /// </returns>
        public static string GetProjectNameFromUri(string uri)
        {
            return GetUriPart("projects", uri);
        }

        public static string GetUriPart(string part, string uri)
        {
            Match match = Regex.Match(uri, $"{part}/(?<value>[^/]*)");
            return match.Groups["value"].Value;
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
        private class ZoneOperation
        {
            public string Project { get; }
            public string Zone { get; }
            public Operation Operation { get; }

            public ZoneOperation(string project, string zone, Operation operation)
            {
                Project = project;
                Zone = zone;
                Operation = operation;
            }
        }
        /// <summary>
        /// Container class for all information needed to wait on a gobal operation.
        /// </summary>
        private class GlobalOperation
        {
            public string Project { get; }
            public Operation Operation { get; }

            public GlobalOperation(string project, Operation operation)
            {
                Project = project;
                Operation = operation;
            }
        }

        /// <summary>
        /// A place to store in progress operations to be waitied on in EndProcessing().
        /// </summary>
        private IList<ZoneOperation> _zoneOperations = new List<ZoneOperation>();

        private IList<GlobalOperation> _globalOperations = new List<GlobalOperation>();

        /// <summary>
        /// Used by child classes to add zone operations to wait on.
        /// </summary>
        /// <param name="project">The name of the Google Cloud project</param>
        /// <param name="zone">The name of the zone</param>
        /// <param name="operation">The Operation object to wait on.</param>
        protected void AddOperation(string project, string zone, Operation operation)
        {
            _zoneOperations.Add(new ZoneOperation(project, zone, operation));
        }

        /// <summary>
        /// Used by child classes to add global operations to wait on.
        /// </summary>
        /// <param name="project">The name of the Google Cloud project</param>
        /// <param name="zone">The name of the zone</param>
        /// <param name="operation">The Operation object to wait on.</param>
        protected void AddOperation(string project, Operation operation)
        {
            _globalOperations.Add(new GlobalOperation(project, operation));
        }

        /// <summary>
        /// Waits on all the operations started by this cmdlet.
        /// </summary>
        protected override void EndProcessing()
        {
            var exceptions = new List<Exception>();
            foreach (ZoneOperation operation in _zoneOperations)
            {
                try
                {
                    WaitForZoneOperation(operation.Project, operation.Zone, operation.Operation);
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
