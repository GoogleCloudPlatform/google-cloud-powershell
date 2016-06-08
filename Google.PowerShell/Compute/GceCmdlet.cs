// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.Apis.Requests;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using static Google.Apis.Compute.v1.Data.Operation;

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
        /// Waits for the provided RegionOperation to complete. This way cmdlets can return newly
        /// created objects once they are finished being created, rather than returning thunks.
        /// 
        /// Will throw an exception if the operation fails for any reason.
        /// </summary>
        protected void WaitForZoneOperation(string project, string zone, Operation op)
        {
            new ZoneOperation(project, zone, op).Wait(Service, CommandRuntime);
        }

        protected void WaitForGlobalOperation(string project, Operation operation)
        {
            new GlobalOperation(project, operation).Wait(Service, CommandRuntime);
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
        /// A place to store in progress operations to be waitied on in EndProcessing().
        /// </summary>
        private IList<ComputeOperation> operations = new List<ComputeOperation>();

        /// <summary>
        /// Used by child classes to add operations to wait on.
        /// </summary>
        /// <param name="project">The name of the Google Cloud project</param>
        /// <param name="zone">The name of the zone</param>
        /// <param name="operation">The Operation object to wait on.</param>
        protected void AddOperation(string project, string zone, Operation operation)
        {
            operations.Add(new ZoneOperation(project, zone, operation));
        }

        /// <summary>
        /// Waits on all the operations started by this cmdlet.
        /// </summary>
        protected override void EndProcessing()
        {
            var exceptions = new List<Exception>();
            foreach (ComputeOperation operation in operations)
            {
                try
                {
                    operation.Wait(Service, CommandRuntime);
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

    interface IComputeOperation
    {
        void Wait(ComputeService service, ICommandRuntime runtime);
    }

    abstract class ComputeOperation : IComputeOperation
    {
        public abstract void Wait(ComputeService service, ICommandRuntime runtime);

        protected void WriteWarnings(Operation op, ICommandRuntime runtime)
        {
            if (op.Warnings != null)
            {
                foreach (Operation.WarningsData warning in op.Warnings)
                {
                    runtime.WriteWarning(warning.Message);
                }
            }
        }

        protected void ThrowErrors(Operation op)
        {
            if (op.Error != null)
            {
                Exception e;
                if (op.Error.Errors == null || op.Error.Errors.Count == 0)
                {
                    throw new GoogleApiException("Compute", "Error waiting for zone operation");
                }
                else if (op.Error.Errors.Count == 1)
                {
                    throw new GoogleApiException("Compute",
                        $"Error waiting for zone operation:{op.Error.Errors.First().Message}");
                }
                else
                {
                    throw new AggregateException("Error waiting for zone operation",
                        op.Error.Errors.Select(error => new GoogleApiException("Compute", error.Message)));
                }
            }
        }
    }


    /// <summary>
    /// Container class for all information needed to wait on an operation.
    /// </summary>
    class ZoneOperation : ComputeOperation
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

        public override void Wait(ComputeService service, ICommandRuntime runtime)
        {
            Operation op = Operation;
            var warnings = new List<WarningsData>();
            WriteWarnings(op, runtime);
            while (op.Status != "DONE")
            {
                Thread.Sleep(150);
                ZoneOperationsResource.GetRequest getReq = service.ZoneOperations.Get(Project, Zone, op.Name);
                op = getReq.Execute();
                WriteWarnings(op, runtime);
            }
            ThrowErrors(op);
        }
    }

    class GlobalOperation : ComputeOperation
    {
        public string Project { get; private set; }
        public Operation Operation { get; private set; }

        public GlobalOperation(string project, Operation operation)
        {
            Project = project;
            Operation = operation;
        }

        public override void Wait(ComputeService service, ICommandRuntime runtime)
        {
            Operation op = Operation;
            var warnings = new List<WarningsData>();
            WriteWarnings(op, runtime);
            while (op.Status != "DONE")
            {
                Thread.Sleep(150);
                GlobalOperationsResource.GetRequest getReq = service.GlobalOperations.Get(Project, op.Name);
                op = getReq.Execute();
                WriteWarnings(op, runtime);
            }
            ThrowErrors(op);
        }
    }
}
