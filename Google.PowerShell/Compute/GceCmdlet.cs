// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

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
        public ComputeService Service { get; private set; }

        public GceCmdlet() : this(null)
        {
        }

        public GceCmdlet(ComputeService service)
        {
            if (service == null)
            {
                Service = new ComputeService(GetBaseClientServiceInitializer());
            }
            else
            {
                Service = service;
            }
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

            while (op.Status != "DONE")
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
            while (operation.Status != "DONE")
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
        /// Library method to pull the name of a zone from a uri of the zone.
        /// </summary>
        /// <param name="zoneUri">
        /// A uri to of a zone.
        /// </param>
        /// <returns>
        /// The name of the zone, which is the last path element of a zone uri.
        /// </returns>
        public static string GetZoneNameFromUri(string zoneUri)
        {
            return zoneUri.Split('/', '\\').Last();
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
        /// Container class for all information needed to wait on an operation.
        /// </summary>
        private class ZoneOperation
        {
            public string Project { get; private set; }
            public string Zone { get; private set; }
            public Operation Operation { get; private set; }

            public ZoneOperation(string project, string zone, Operation operation)
            {
                Project = project;
                Zone = zone;
                Operation = operation;
            }
        }

        /// <summary>
        /// A place to store in progress operations to be waitied on in EndProcessing().
        /// </summary>
        private IList<ZoneOperation> _operations = new List<ZoneOperation>();

        /// <summary>
        /// Used by child classes to add operations to wait on.
        /// </summary>
        /// <param name="project">The name of the Google Cloud project</param>
        /// <param name="zone">The name of the zone</param>
        /// <param name="operation">The Operation object to wait on.</param>
        protected void AddOperation(string project, string zone, Operation operation)
        {
            _operations.Add(new ZoneOperation(project, zone, operation));
        }

        /// <summary>
        /// Waits on all the operations started by this cmdlet.
        /// </summary>
        protected override void EndProcessing()
        {
            var exceptions = new List<Exception>();
            foreach (ZoneOperation operation in _operations)
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
            if (exceptions.Count > 1)
            {
                throw new AggregateException(exceptions);
            }
            else if (exceptions.Count == 1)
            {
                throw exceptions.First();
            }
        }
    }
    public abstract class GceZoneConcurrentCmdlet : GceConcurrentCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project of this command.
        /// </para>
        /// </summary>
        [Parameter]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone of this command.
        /// </para>
        /// </summary>
        [Parameter]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        protected override void BeginProcessing()
        {
            Project = GceProjectCmdlet.PopulateProjectOrThrow(Project);
            Zone = GceZoneCmdlet.PopulateZoneOrThrow(Zone);
        }
    }

    public abstract class GceProjectCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project of this command.
        /// </para>
        /// </summary>
        [Parameter]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        protected override void BeginProcessing()
        {
            Project = PopulateProjectOrThrow(Project);
        }

        /// <summary>
        /// Populates an empty project parameter.
        /// </summary>
        /// <param name="project">
        /// The incomming project parameter.
        /// </param>
        /// <returns>
        /// The incomming project parameter if it is not empty, or the default project.
        /// </returns>
        /// <exception cref="PSInvalidOperationException">
        /// If the incomming parameter is empty and there is no default.
        /// </exception>
        public static string PopulateProjectOrThrow(string project)
        {
            if (string.IsNullOrEmpty(project))
            {
                project = CloudSdkSettings.GetDefaultProject();
                if (string.IsNullOrEmpty(project))
                {
                    throw new PSInvalidOperationException(
                        "Parameter Project was not specified and has no default value.");
                }
            }
            return project;
        }
    }

    public abstract class GceZoneCmdlet : GceProjectCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The zone of this command.
        /// </para>
        /// </summary>
        [Parameter]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        protected override void BeginProcessing()
        {
            base.BeginProcessing();

            Zone = PopulateZoneOrThrow(Zone);
        }

        /// <summary>
        /// Populates an empty project parameter.
        /// </summary>
        /// <param name="zone">
        /// The incomming project parameter.
        /// </param>
        /// <returns>
        /// The incomming project parameter if it is not empty, or the default project.
        /// </returns>
        /// <exception cref="PSInvalidOperationException">
        /// If the incomming parameter is empty and there is no default.
        /// </exception>
        public static string PopulateZoneOrThrow(string zone)
        {
            if (string.IsNullOrEmpty(zone))
            {
                zone = CloudSdkSettings.GetDefaultZone();
                if (string.IsNullOrEmpty(zone))
                {
                    throw new PSInvalidOperationException(
                        "Parameter Zone was not specified and has no default value.");
                }
            }
            return zone;
        }
    }
}
