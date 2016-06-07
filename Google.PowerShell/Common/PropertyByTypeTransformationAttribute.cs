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
    /// This attribute allows a parameter to accept multiple types by replacing a target type with the value
    /// of a specified property. If the argument is not of the target type, it will not be changed.
    /// </summary>
    /// <example>
    /// <code>
    /// [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
    /// public string ZoneName { get; set; }
    /// </code>
    /// Transforms any Zone objects given to the ZoneName Parameter into zoneObject.Name
    /// </example>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class PropertyByTypeTransformationAttribute : ArgumentTransformationAttribute
    {
        /// <summary>
        /// The target Type that will be transformed. e.g. A Google.Aips.Compute.v1.Zone object.
        /// </summary>
        public Type TypeToTransform { get; set; }

        /// <summary>
        /// The name of the Property/Field to return. e.g. Name, a property of the type Zone
        /// </summary>
        public string Property { get; set; }

        public override object Transform(EngineIntrinsics engineIntrinsics, object inputData)
        {
            // Treat PSObjects with a base type of the target type as the target type.
            if (inputData is PSObject && TypeToTransform.IsInstanceOfType((inputData as PSObject).BaseObject))
            {
                return (inputData as PSObject).Properties[Property].Value;
            }
            // Target type case
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
