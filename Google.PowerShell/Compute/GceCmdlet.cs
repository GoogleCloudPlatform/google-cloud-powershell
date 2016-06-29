﻿// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Waits for the provided region operation to complete. This way cmdlets can return newly
        /// created objects once they are finished being created, rather than returning thunks.
        /// 
        /// Will throw an exception if the operation fails for any reason.
        /// </summary>
        protected void WaitForRegionOperation(string project, string region, Operation op)
        {
            WriteWarnings(op);

            while (op.Status != "DONE" && !Stopping)
            {
                Thread.Sleep(150);
                RegionOperationsResource.GetRequest getReq =
                    Service.RegionOperations.Get(project, region, op.Name);
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

        /// <summary>
        /// Library method to pull a resource name from a Rest uri.
        /// </summary>
        /// <param name="resourceType">
        /// The type of resource to get the name of (e.g. projects, zones, instances)
        /// </param>
        /// <param name="uri">
        /// The uri to pull the resource name from.
        /// </param>
        /// <returns>
        /// The name of the resource i.e. the section of the uri following the resource type.
        /// </returns>
        public static string GetUriPart(string resourceType, string uri)
        {
            Match match = Regex.Match(uri, $"{resourceType}/(?<value>[^/]*)");
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
        private class LocalOperation
        {
            public string Project { get; }
            public string Local { get; }
            public Operation Operation { get; }

            public LocalOperation(string project, string local, Operation operation)
            {
                Project = project;
                Local = local;
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
        /// Used by child classes to add global operations to wait on.
        /// </summary>
        /// <param name="project">The name of the Google Cloud project</param>
        /// <param name="operation">The Operation object to wait on.</param>
        protected void AddGlobalOperation(string project, Operation operation)
        {
            _globalOperations.Add(new GlobalOperation(project, operation));
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
