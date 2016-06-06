// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets information about Google Compute Engine VM Instances.
    /// </para>
    /// <para type="description">
    /// Gets information about VM instances.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceInstance")]
    public class GetGceInstanceCmdlet : GceCmdlet
    {

        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = false)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = false,
            ValueFromPipeline = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Instance))]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// A filter to send along with the request. This has the name of the property to filter on, either eq
        /// or ne, and a constant to test against.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string Filter { get; set; }

        protected override void ProcessRecord()
        {
            IEnumerable<Instance> output;
            if (String.IsNullOrEmpty(Zone))
            {
                output = GetAllProjectInstances();

            }
            else if (String.IsNullOrEmpty(Name))
            {
                output = GetZoneInstances();
            }
            else
            {
                output = new Instance[] { GetExactInstance() };
            }

            foreach (Instance instance in output)
            {
                WriteObject(instance);
            }
        }

        private IEnumerable<Instance> GetAllProjectInstances()
        {
            string pageToken = null;
            do
            {
                var aggListRequest = Service.Instances.AggregatedList(Project);
                aggListRequest.Filter = Filter;
                aggListRequest.PageToken = pageToken;
                var aggList = aggListRequest.Execute();
                string nextPageToken = aggList.NextPageToken;
                var instances = aggList.Items.Values
                    .Where(l => l.Instances != null)
                    .SelectMany(l => l.Instances);
                foreach (Instance instance in instances)
                {
                    yield return instance;
                }
            }
            while (pageToken != null);
        }

        private IEnumerable<Instance> GetZoneInstances()
        {
            string pageToken = null;
            do
            {

                InstancesResource.ListRequest listRequest = Service.Instances.List(Project, Zone);
                listRequest.Filter = Filter;
                listRequest.PageToken = pageToken;
                InstanceList response = listRequest.Execute();

                if (response.Items != null)
                {
                    foreach (Instance instance in response.Items)
                    {
                        if (instance != null)
                        {
                            yield return instance;
                        }
                    }
                }
                pageToken = response.NextPageToken;
            }
            while (pageToken != null);
        }

        private Instance GetExactInstance()
        {
            InstancesResource.GetRequest getRequest = Service.Instances.Get(Project, Zone, Name);
            return getRequest.Execute();
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates and starts a Google Compute Engine VM instance.
    /// </para>
    /// <para type="description">
    /// Creates and starts a Google Compute Engine VM instance. Use New-GceInstanceConfig to create an instance
    /// description.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceInstance")]
    public class AddGceInstanceCmdlet : GceConcurrentCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project that will own the instances.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance will reside.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The description of the instance to create, as generated by New-GceInstanceConfig.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ValueFromPipeline = true)]
        public Instance Instance { get; set; }

        protected override void ProcessRecord()
        {
            if (Instance.MachineType.Split('/', '\\').Length < 2)
            {
                if (Instance.MachineType.Contains("custom"))
                {
                    Instance.MachineType = $"zones/{Zone}/machineTypes/{Instance.MachineType}";
                }
                else
                {
                    Instance.MachineType =
                        $"projects/{Project}/zones/{Zone}/machineTypes/{Instance.MachineType}";
                }
            }

            InstancesResource.InsertRequest request = Service.Instances.Insert(Instance, Project, Zone);
            Operation operation = request.Execute();
            AddOperation(Project, Zone, operation);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes a Google Compute Engine VM instance.
    /// </para>
    /// <para type="description">
    /// Deletes a Google Compute Engine VM instance.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceInstance")]
    public class RemoveGceInstanceCmdlet : GceConcurrentCmdlet
    {

        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance to delete.
        /// </para>
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ValueFromPipeline = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Instance))]
        public string Name { get; set; }
        protected override void ProcessRecord()
        {
            var request = Service.Instances.Delete(Project, Zone, Name);
            var operation = request.Execute();
            AddOperation(Project, Zone, operation);
        }
    }
}
