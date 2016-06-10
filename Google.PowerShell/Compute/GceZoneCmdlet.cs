using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    public abstract class GceZoneCmdlet : GceProjectCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        protected override void BeginProcessing()
        {
            base.BeginProcessing();

            if (Zone == null)
            {
                Zone = CloudSdkSettings.GetDefaultZone();
                if (Zone == null)
                {
                    throw new PSInvalidOperationException(
                        "Parameter Zone was not specified and has no default value.");
                }
            }
        }
    }

    public abstract class GceProjectCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        protected override void BeginProcessing()
        {
            if (Project == null)
            {
                Project = CloudSdkSettings.GetDefaultProject();
                if (Project == null)
                {
                    throw new PSInvalidOperationException(
                        "Parameter Project was not specified and has no default value.");
                }
            }
        }
    }

    public abstract class GceZoneConcurrentCmdlet : GceConcurrentCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project that owns the instances.
        /// </para>
        /// </summary>
        [Parameter]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone in which the instance resides.
        /// </para>
        /// </summary>
        [Parameter]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
        public string Zone { get; set; }

        protected override void BeginProcessing()
        {
            if (Project == null)
            {
                Project = CloudSdkSettings.GetDefaultProject();
                if (Project == null)
                {
                    throw new PSInvalidOperationException(
                        "Parameter Project was not specified and has no default value.");
                }
            }

            if (Zone == null)
            {
                Zone = CloudSdkSettings.GetDefaultZone();
                if (Zone == null)
                {
                    throw new PSInvalidOperationException(
                        "Parameter Zone was not specified and has no default value.");
                }
            }
        }
    }
}