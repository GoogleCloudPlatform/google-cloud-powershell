using Google.Apis.Compute.v1.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Google.PowerShell.ComputeEngine
{
    public abstract class GceConcurrentCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project of this command.
        /// </para>
        /// </summary>
        public abstract string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The zone of this command.
        /// </para>
        /// </summary>
        public abstract string Zone { get; set; }

        /// <summary>
        /// A place to store in progress operations to be waitied on in EndProcessing().
        /// </summary>
        protected IList<Operation> operations = new List<Operation>();
        
        /// <summary>
        /// Waits on all the operations stared by this Cmdlet.
        /// </summary>
        protected override void EndProcessing()
        {
            IList<Exception> exceptions = new List<Exception>();
            foreach (Operation operation in operations)
            {
                try
                {
                    WaitForZoneOperation(Service, Project, Zone, operation);
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
}
