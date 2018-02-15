// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using System;
using System.Linq;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// Container exception for Operation.ErrorData.
    /// </summary>
    public class GoogleComputeOperationException : Exception
    {
        public Operation.ErrorData OperationError { get; private set; }

        public GoogleComputeOperationException(Operation.ErrorData errorData) :
            this(GetErrorMessage(errorData), errorData)
        {
        }

        /// <summary>
        /// Gets the first error message, or "Unknown error" if there is none.
        /// </summary>
        private static string GetErrorMessage(Operation.ErrorData errorData)
        {
            if (errorData?.Errors != null && errorData.Errors.Count > 0)
            {
                return errorData.Errors.First().Message;
            }
            else
            {
                return "Unknown error";
            }
        }

        public GoogleComputeOperationException(string message, Operation.ErrorData errorData) : base(message)
        {
            OperationError = errorData;
        }
    }
}
