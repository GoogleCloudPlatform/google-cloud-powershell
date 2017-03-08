// Copyright 2015-2016 Google Inc. All Rights Reserved.
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
    /// Add-GceInstanceTemplate cmdlets to link to service accounts and define scopes. These scopes in turn let
    /// your instances access Google Cloud Platform resources.
    /// If no service account email is specified, the cmdlet will use the default service account email.
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GceServiceAccountConfig serviceaccount@gserviceaccount.com -BigQuery -BigtableData Read
    ///   </code>
    ///   <para>
    ///   Creates a scope on the serviceaccount@gserviceaccount.com service account that can make BigQuery queries
    ///   and read bigtable data.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GceServiceAccountConfig -BigQuery -BigtableData Read</code>
    ///   <para>
    ///   Creates a scope on the default service account that can make BigQuery queries and read bigtable data.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/instances#resource)">
    /// [Instance resource definition]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/access/service-accounts#compute_engine_default_service_account)">
    /// [Default Service Account email]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GceServiceAccountConfig", DefaultParameterSetName = ParameterSetNames.FromFlags)]
    [OutputType(typeof(ServiceAccount))]
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
        /// The cmdlet will use the default service account from this project if no email is given.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The email of the service account to link to.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = false, ParameterSetName = ParameterSetNames.FromScopeUris)]
        [Parameter(Position = 0, Mandatory = false, ValueFromPipeline = true,
            ParameterSetName = ParameterSetNames.FromFlags)]
        [ValidateNotNullOrEmpty]
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
            if (Email == null)
            {
                string projectNumber = GetProjectNumber(Project);
                Email = $"{projectNumber}-compute@developer.gserviceaccount.com";
            }
            switch (ParameterSetName)
            {
                case ParameterSetNames.FromScopeUris:
                    _scopeUrisList.AddRange(ScopeUri);
                    break;
                case ParameterSetNames.FromFlags:
                    WriteObject(BuildFromFlags());
                    break;
                default:
                    throw UnknownParameterSetException;
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
        /// Mapping of parameters to their default values.
        /// Because this cmdlet uses MyInvocation.BoundParameters to get the parameter values, we can't just
        /// set the respective porperties to their default values, as they would not be bound.
        /// </summary>
        private static readonly IDictionary<string, object> s_defaultParameterValues =
            new Dictionary<string, object>
            {
                {"CloudLogging", ReadWrite.Write},
                {"CloudMonitoring", ReadWrite.Write},
                {"ServiceControl", true},
                {"ServiceManagement", true},
                {"Storage", ReadWrite.Read}
            };

        /// <summary>
        /// Maps parameter names to a sub dictionary. each sub dictionary maps parameter values to their scope
        /// strings.
        /// </summary>
        /// <example>
        /// If BigtableAdmin is a bound parameter, and is bound to the value BigTableAdminEnum.Tables, you can
        /// get the related sope with 
        /// <code>string scope = NamevalueScopeMap["BigtableAdmin"][BigTableAdminEnum.Tables]</code>
        /// </example>
        private static readonly IDictionary<string, IDictionary<object, string>> s_nameValueScopeMap =
            new Dictionary<string, IDictionary<object, string>>
            {
                {
                    "BigQuery", new Dictionary<object, string>
                    {
                        {SwitchParameter.Present, "bigquery"}
                    }
                },
                {
                    "BigtableAdmin", new Dictionary<object, string>
                    {
                        {BigTableAdminEnum.Tables, "bigtable.admin.table"},
                        {BigTableAdminEnum.Full, "bigtable.admin"}
                    }
                },
                {
                    "BigtableData", new Dictionary<object, string>
                    {
                        {ReadWrite.Read, "bigtable.data.readonly"},
                        {ReadWrite.ReadWrite, "bigtable.data"}
                    }
                },
                {
                    "CloudDatastore", new Dictionary<object, string>
                    {
                        {SwitchParameter.Present, "datastore"}
                    }
                },
                {
                    "CloudLogging", new Dictionary<object, string>
                    {
                        {ReadWrite.Write, "logging.write"},
                        {ReadWrite.Read, "logging.read"},
                        {ReadWrite.Full, "logging.admin"}
                    }
                },
                {
                    "CloudMonitoring", new Dictionary<object, string>
                    {
                        {ReadWrite.Write, "monitoring.write"},
                        {ReadWrite.Read, "monitoring.read"},
                        {ReadWrite.Full, "monitoring"}
                    }
                },
                {
                    "CloudPubSub", new Dictionary<object, string>
                    {
                        {SwitchParameter.Present, "pubsub"}
                    }
                },
                {
                    "CloudSQL", new Dictionary<object, string>
                    {
                        {SwitchParameter.Present, "sqlservice.admin"}
                    }
                },
                {
                    "Compute", new Dictionary<object, string>
                    {
                        {ReadWrite.Read, "compute.readonly"},
                        {ReadWrite.ReadWrite, "compute"}
                    }
                },
                {
                    "ServiceControl", new Dictionary<object, string>
                    {
                        {true, "servicecontrol"}
                    }
                },
                {
                    "ServiceManagement", new Dictionary<object, string>
                    {
                        {true, "service.management"}
                    }
                },
                {
                    "Storage", new Dictionary<object, string>
                    {
                        {ReadWrite.Read, "devstorage.read_only"},
                        {ReadWrite.Write, "devstorage.write_only"},
                        {ReadWrite.ReadWrite, "devstorage.read_write"},
                        {ReadWrite.Full, "devstorage.full_control"}
                    }
                },
                {
                    "TaskQueue", new Dictionary<object, string>
                    {
                        {SwitchParameter.Present, "taskqueue"}
                    }
                },
                {
                    "UserInfo", new Dictionary<object, string>
                    {
                        {SwitchParameter.Present, "userinfo.email"}
                    }
                }
            };

        /// <summary>
        /// Creates a ServiceAccount object from the email and uses the given flags to add scopes.
        /// </summary>
        private ServiceAccount BuildFromFlags()
        {
            var scopes = new List<string>();
            if (ScopeUri != null)
            {
                scopes.AddRange(ScopeUri);
            }

            // Add scopes for bound parameters.
            foreach (KeyValuePair<string, object> boundParameter in MyInvocation.BoundParameters)
            {
                AddScope(scopes, boundParameter);
            }

            // Add scopes for parameters with default values that were not bound.
            foreach (KeyValuePair<string, object> defaultParameter in s_defaultParameterValues)
            {
                if (!MyInvocation.BoundParameters.ContainsKey(defaultParameter.Key))
                {
                    AddScope(scopes, defaultParameter);
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
        /// <param name="scopes">List of scope uri strings to append to.</param>
        /// <param name="parameter">A KeyValuePair containing the name of the parameter as the key and the 
        /// value of the parameter as the value</param>
        private static void AddScope(List<string> scopes, KeyValuePair<string, object> parameter)
        {
            const string baseUri = "https://www.googleapis.com/auth/";

            string scopeType = parameter.Key;
            if (s_nameValueScopeMap.ContainsKey(scopeType))
            {
                if (s_nameValueScopeMap[scopeType].ContainsKey(parameter.Value))
                {
                    string scopeString = s_nameValueScopeMap[scopeType][parameter.Value];
                    scopes.Add($"{baseUri}{scopeString}");
                }
            }
        }
    }
}
