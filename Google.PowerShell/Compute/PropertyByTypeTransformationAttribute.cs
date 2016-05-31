using Google.Apis.Compute.v1.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Google.PowerShell.Compute
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class PropertyByTypeTransformationAttribute : ArgumentTransformationAttribute
    {
        public Type Type { get; set; } 
        public string Property { get; set; }
        public override object Transform(EngineIntrinsics engineIntrinsics, object inputData)
        {
            if(inputData is PSObject && Type.IsInstanceOfType((inputData as PSObject).BaseObject))
            {
                return (inputData as PSObject).Properties[Property].Value;
            }
            else if(Type.IsInstanceOfType(inputData))
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
