using Google.Apis.Compute.v1.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// Container exception for Operation.ErrorData.
    /// </summary>
    public class GoogleComputeOperationException : Exception
    {
        public Operation.ErrorData OperationError { get; private set; }

        public GoogleComputeOperationException(Operation.ErrorData errorData) :
            this(errorData?.Errors?.First()?.Message ?? "Unknown error", errorData)
        {
        }

        public GoogleComputeOperationException(string message, Operation.ErrorData errorData) : base(message)
        {
            OperationError = errorData;
        }
    }
}
