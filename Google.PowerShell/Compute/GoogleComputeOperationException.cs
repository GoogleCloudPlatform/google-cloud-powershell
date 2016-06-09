// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Linq;
using Google.Apis.Compute.v1.Data;

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
