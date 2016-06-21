// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Creates a new ServiceAccount object.
    /// </para>
    /// <para type="description">
    /// Creates a new ServiceAccount object. These objects are used by New-GceInstanceConfig and 
    /// Add-GceInstanceTemplate cmdlets to link to service accounts and define scopes.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GceServiceAccountConfig", DefaultParameterSetName = ParameterSetNames.FromFlags)]
    public class NewGceServiceAccountConfigCmdlet : GCloudCmdlet
    {
        private class ParameterSetNames
        {
            public const string FromFlags = "FromFlags";
            public const string FromScopeUris = "FromScopeUris";
        }

        /// <summary>
        /// Enum used by BigtableAdmin parameter.
        /// </summary>
        public enum BigTableAdminEnum
        {
            None,
            Tables,
            Full
        }

        /// <summary>
        /// Various possible Read and Write scopes. Not values are legal for all parameters.
        /// </summary>
        public enum ReadWrite
        {
            None,
            Read,
            Write,
            ReadWrite,
            Full
        }

        /// <summary>
        /// <para type="description">
        /// The email of the service account to link to.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.FromScopeUris)]
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.FromFlags)]
        public string Email { get; set; }

        /// <summary>
        /// <para type="description">
        /// A uri of a scope to add to this service account. When added from the pipeline, all pipeline scopes
        /// will be added to a single ServiceAccount.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.FromScopeUris)]
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public string[] ScopeUri { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, adds the BigQuery scope.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public SwitchParameter BigQuery { get; set; }

        /// <summary>
        /// <para type="description">
        /// The type of Bigtable Admin scope. Defaults to None. Also accepts Tables and Full
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public BigTableAdminEnum BigtableAdmin { get; set; }

        /// <summary>
        /// <para type="description">
        /// The type of Bigtable Data scope. Defaults to None. Also accepts Read and ReadWrite.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        [ValidateSet("None", "Read", "ReadWrite")]
        public ReadWrite BigtableData { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, adds the Cloud Datastore scope.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public SwitchParameter CloudDatastore { get; set; }

        /// <summary>
        /// <para type="description">
        /// The type of Cloud Logging API scope to add. Defaults to Write. Also accepts None, Read and Full.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        [ValidateSet("None", "Read", "Write", "Full")]
        public ReadWrite CloudLogging { get; set; }

        /// <summary>
        /// <para type="description">
        /// The type of Cloud Monitoring scope to add. Defaults to Write. Also accepts None, Read and Full.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        [ValidateSet("None", "Read", "Write", "Full")]
        public ReadWrite CloudMonitoring { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, adds the Cloud Pub/Sub scope.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public SwitchParameter CloudPubSub { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, adds the Cloud SQL scope.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public SwitchParameter CloudSQL { get; set; }

        /// <summary>
        /// <para type="description">
        /// The value of the Compute scope to add. Defaults to None. Also accepts Read and ReadWrite.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        [ValidateSet("None", "Read", "ReadWrite")]
        public ReadWrite Compute { get; set; }

        /// <summary>
        /// <para type="description">
        /// If true, adds the Service Control scope. Defaults to true.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public bool ServiceControl { get; set; }

        /// <summary>
        /// <para type="description">
        /// If true, adds the Service Management scope. Defaults to true.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public bool ServiceManagement { get; set; }

        /// <summary>
        /// <para type="description">
        /// The type of Storage scope to add. Defaults to Read. Also accepts None, Write, ReadWrite and Full.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public ReadWrite Storage { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, adds the Task queue scope.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public SwitchParameter TaskQueue { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, adds the User info scope.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public SwitchParameter UserInfo { get; set; }

        /// <summary>
        /// Used to collect scopes from the pipeline to be used in EndProcessing.
        /// </summary>
        private readonly List<string> _scopeUrisList = new List<string>();

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.FromScopeUris:
                    _scopeUrisList.AddRange(ScopeUri);
                    break;
                case ParameterSetNames.FromFlags:
                    WriteObject(BuildFromFlags());
                    break;
                default:
                    throw new PSInvalidOperationException($"{ParameterSetName} is not a valid parameter set");
            }
        }

        /// <summary>
        /// If there are collected scopes, create a new service account from them.
        /// </summary>
        protected override void EndProcessing()
        {
            if (_scopeUrisList.Count > 0)
            {
                WriteObject(new ServiceAccount
                {
                    Email = Email,
                    Scopes = _scopeUrisList
                });
            }
            base.EndProcessing();
        }

        /// <summary>
        /// Because we are looking at bound parameters, we can't just set parameters to their default values
        /// </summary>
        private readonly Dictionary<string, object> _defaultParameterValues = new Dictionary<string, object>
        {
            {"cloudlogging", ReadWrite.Write},
            {"cloudmonitoring", ReadWrite.Write },
            {"servicecontrol", true },
            {"servicemanagement", true },
            {"storage", ReadWrite.Read }

        };

        /// <summary>
        /// Mapping of parameter and value to scope string.
        /// </summary>
        private readonly Dictionary<string, IDictionary<object, string>> _scopeUriMap =
            new Dictionary<string, IDictionary<object, string>>
            {
                {
                    "bigquery", new Dictionary<object, string>
                    {
                        {SwitchParameter.Present, "bigquery"}
                    }
                },
                {
                    "bigtableadmin", new Dictionary<object, string>
                    {
                        {BigTableAdminEnum.Tables, "bigtable.admin.table"},
                        {BigTableAdminEnum.Full, "bigtable.admin"}
                    }
                },
                {
                    "bigtabledata", new Dictionary<object, string>
                    {
                        {ReadWrite.Read, "bigtable.data.readonly"},
                        {ReadWrite.ReadWrite, "bigtable.data"}
                    }
                },
                {
                    "clouddatastore", new Dictionary<object, string>
                    {
                        {SwitchParameter.Present, "datastore"}
                    }
                },
                {
                    "cloudlogging", new Dictionary<object, string>
                    {
                        {ReadWrite.Write, "logging.write"},
                        {ReadWrite.Read, "logging.read"},
                        {ReadWrite.Full, "logging.admin"}
                    }
                },
                {
                    "cloudmonitoring", new Dictionary<object, string>
                    {
                        {ReadWrite.Write, "monitoring.write"},
                        {ReadWrite.Read, "monitoring.read"},
                        {ReadWrite.Full, "monitoring"}
                    }
                },
                {
                    "cloudpubsub", new Dictionary<object, string>
                    {
                        {SwitchParameter.Present, "pubsub"}
                    }
                },
                {
                    "cloudsql", new Dictionary<object, string>
                    {
                        {SwitchParameter.Present, "sqlservice.admin"}
                    }
                },
                {
                    "compute", new Dictionary<object, string>
                    {
                        {ReadWrite.Read, "compute.readonly"},
                        {ReadWrite.ReadWrite, "compute"}
                    }
                },
                {
                    "servicecontrol", new Dictionary<object, string>
                    {
                        {true, "servicecontrol"}
                    }
                },
                {
                    "servicemanagement", new Dictionary<object, string>
                    {
                        {true, "service.management"}
                    }
                },
                {
                    "storage", new Dictionary<object, string>
                    {
                        {ReadWrite.Read, "devstorage.read_only"},
                        {ReadWrite.Write, "devstorage.write_only"},
                        {ReadWrite.ReadWrite, "devstorage.read_write"},
                        {ReadWrite.Full, "devstorage.full_control"}
                    }
                },
                {
                    "taskqueue", new Dictionary<object, string>
                    {
                        {SwitchParameter.Present, "taskqueue"}
                    }
                },
                {
                    "userinfo", new Dictionary<object, string>
                    {
                        {SwitchParameter.Present, "userinfo.email"}
                    }
                }
            };

        /// <summary>
        /// Creates a ServiceAccount object from the email and uses the given flags to add scopes.
        /// </summary>
        /// <returns></returns>
        private ServiceAccount BuildFromFlags()
        {
            var scopes = new List<string>();
            if (ScopeUri != null)
            {
                scopes.AddRange(ScopeUri);
            }

            foreach (KeyValuePair<string, object> boundParameter in MyInvocation.BoundParameters)
            {
                AddScope(boundParameter, scopes);
            }

            foreach (KeyValuePair<string, object> defaultParameter in _defaultParameterValues)
            {
                if (!MyInvocation.BoundParameters.ContainsKey(defaultParameter.Key))
                {
                    AddScope(defaultParameter, scopes);
                }
            }

            return new ServiceAccount
            {
                Email = Email,
                Scopes = scopes
            };
        }

        /// <summary>
        /// Adds the scope uri of the parameter, if it has one.
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="scopes"></param>
        private void AddScope(KeyValuePair<string, object> parameter, List<string> scopes)
        {
            const string baseUri = "https://www.googleapis.com/auth/";

            string scopeType = parameter.Key.ToLower();
            if (_scopeUriMap.ContainsKey(scopeType))
            {
                if (_scopeUriMap[scopeType].ContainsKey(parameter.Value))
                {
                    string scopeString = _scopeUriMap[scopeType][parameter.Value];
                    scopes.Add($"{baseUri}{scopeString}");
                }
            }
        }
    }
}
