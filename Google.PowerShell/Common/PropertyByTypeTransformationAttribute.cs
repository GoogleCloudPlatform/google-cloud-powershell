using Google.Apis.Compute.v1.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// If the argument is of the target type, replace it with the value of the given property, otherwise it
    /// does no transformation.
    /// </summary>
    /// <example>
    /// <code>
    /// [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
    /// public string ZoneName { get; set; }
    /// </code>
    /// Transforms any Zone objects given to the ZoneName Parameter into zoneObject.Name
    /// </example>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class PropertyByTypeTransformationAttribute : ArgumentTransformationAttribute
    {
        /// <summary>
        /// The target Type.
        /// </summary>
        public Type TypeToTransform { get; set; }

        /// <summary>
        /// The name of the Property/Field to return.
        /// </summary>
        public string Property { get; set; }

        public override object Transform(EngineIntrinsics engineIntrinsics, object inputData)
        {
            //If it is a PSObject where the base object is of the target type
            if (inputData is PSObject && TypeToTransform.IsInstanceOfType((inputData as PSObject).BaseObject))
            {
                return (inputData as PSObject).Properties[Property].Value;
            }
            //If it is the target type
            else if (TypeToTransform.IsInstanceOfType(inputData))
            {
                return new PSObject(inputData).Properties[Property].Value;
            }
            else
            {
                return inputData;
            }
        }
    }
}
