// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.PowerShell.Common;
using Google.Apis.SQLAdmin.v1beta4;
using System.Text.RegularExpressions;
using Google.Apis.SQLAdmin.v1beta4.Data;
using System.Threading;
using System.Management.Automation;

namespace Google.PowerShell.Sql
{
    /// <summary>
    /// Base class for Google Cloud SQL-based cmdlets. 
    /// </summary>
    public abstract class GcSqlCmdlet : GCloudCmdlet
    {
        //The service for the Google Cloud SQL API
        public SQLAdminService Service { get; private set; }

        public GcSqlCmdlet()
        {
            Service = new SQLAdminService(GetBaseClientServiceInitializer());
        }

        /// <summary>
        /// Library method to wait for an SQL operation to finish.
        /// </summary>
        /// <param name="op">
        /// The SQL operation we want to wait for.
        /// </param>
        /// <returns>
        /// The finished operation resource.
        /// </returns>
        public Operation WaitForSqlOperation(Operation op)
        {
            int delay;
            switch (op.OperationType)
            {
                case "CREATE":
                case "FAILOVER":
                case "RECREATE_REPLICA":
                case "RESTORE_VOLUME":
                case "CREATE_REPLICA":
                    {
                        delay = 30000;
                        break;
                    }
                case "UPDATE":
                case "DELETE":
                case "IMPORT":
                case "EXPORT":
                    {
                        delay = 5000;
                        break;
                    }
                default:
                    {
                        delay = 150;
                        break;
                    }
            }
            Random rnd = new Random();
            int randProc = rnd.Next();
            ProgressRecord progress =
                new ProgressRecord(
                    randProc,
                    $"Waiting for operation '{op.OperationType}' to finish. May take multiple minutes.",
                    "Waiting");
            progress.PercentComplete = 0;
            WriteProgress(progress);
            int percentComplete = 1;
            while (op.Status != "DONE")
            {
                if (op.Error != null)
                {
                    WriteWarning(op.Error.ToString());
                    return op;
                }
                Thread.Sleep(delay);
                progress.PercentComplete = percentComplete;
                WriteProgress(progress);
                percentComplete = (percentComplete + 1) % 100;
                OperationsResource.GetRequest request = Service.Operations.Get(op.TargetProject, op.Name);
                op = request.Execute();
            }
            progress.RecordType = ProgressRecordType.Completed;
            progress.PercentComplete = 100;
            WriteProgress(progress);
            return op;
        }
    }
}
