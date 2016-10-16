// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System.Collections.Generic;
using System.Management.Automation;
using Google.Apis.SQLAdmin.v1beta4.Data;

namespace Google.PowerShell.Sql
{
    // TODO(chrsmith): Figure out if this actually works in context of Add-GcSqlInstance

    /// <summary>
    /// <para type="synopsis">
    /// Creates a configuration for a replicaConfiguration. 
    /// </para>
    /// <para type="description"> 
    /// Creates a configuration for a replicaConfiguration. 
    /// Can be pipelined into New-GcSqlInstanceConfig.
    /// </para>
    /// <para type="description">
    /// NOTE: If any parameter is incorrect/invalid, this cmdlet not fail. You will only
    /// receive an error once you try to update or add an instance with this configuration to your
    /// project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GcSqlInstanceReplicaConfig</code>
    ///   <para>Creates a basic replica configuration resource with default values.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GcSqlInstanceReplicaConfig -MySqlRetryInterval 10</code>
    ///   <para>Creates a basic replica configuration resource with a retry interval of 10 seconds.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/tools/powershell/docs/sql/setup)">
    ///   [Setting up Instances]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/tools/powershell/docs/sql/replica)">
    ///   [Replica Instances]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/sql/docs/replication/)">
    ///   [Replication Options]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/sql/docs/replication/tips)">
    ///   [Replication Requirements and Tips]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcSqlInstanceReplicaConfig")]
    [OutputType(typeof(ReplicaConfiguration))]
    public class NewGcSqlInstanceReplicaConfigCmdlet : GcSqlCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Specifies if the replica is the failover target.  If the field is set to true the
        /// replica will be designated as a failover replica. In case the master instance fails,
        /// the replica instance will be promoted as the new master instance.
        /// </para>
        /// <para type="description"> 
        /// Only one replica can be specified as failover target, and the replica has to be in
        /// different zone with the master instance.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter FailoverTarget { get; set; }

        /// <summary>
        /// <para type="description">
        /// PEM representation of the trusted CA’s x509 certificate.
        /// </para>
        /// </summary>
        [Parameter]
        public string MySqlCaCert { get; set; }

        /// <summary>
        /// <para type="description">
        /// PEM representation of the slave’s x509 certificate.
        /// </para>
        /// </summary>
        [Parameter]
        public string MySqlClientCert { get; set; }

        /// <summary>
        /// <para type="description">
        ///  PEM representation of the slave’s private key. 
        ///  The corresponding public key is encoded in the client’s certificate.
        /// </para>
        /// </summary>
        [Parameter]
        public string MySqlClientKey { get; set; }

        /// <summary>
        /// <para type="description">
        /// Seconds to wait between connect retries. The default is 60 seconds.
        /// </para>
        /// </summary>
        [Parameter]
        public int MySqlRetryInterval { get; set; } = 60;

        /// <summary>
        /// <para type="description">
        /// Path to a SQL dump file in Google Cloud Storage from which the slave instance is to be created. 
        /// The URI is in the form "gs://bucketName/fileName". Compressed gzip files (.gz) are also supported. 
        /// Dumps should have the binlog co-ordinates from which replication should begin. 
        /// This can be accomplished by setting --master-data to 1 when using mysqldump.
        /// </para>
        /// </summary>
        [Parameter]
        public string MySqlDumpPath { get; set; }

        /// <summary>
        /// <para type="description">
        /// Interval in milliseconds between replication heartbeats.
        /// Defaults to 20 seconds.
        /// </para>
        /// </summary>
        [Parameter]
        public long MySqlHeartbeatPeriod { get; set; } = 20000;

        /// <summary>
        /// <para type="description">
        /// The password for the replication connection.
        /// </para>
        /// </summary>
        [Parameter]
        public string MySqlPassword { get; set; }

        /// <summary>
        /// <para type="description">
        /// A list of permissible ciphers to use for SSL encryption.
        /// </para>
        /// </summary>
        [Parameter]
        public string MySqlSslCipher { get; set; }

        /// <summary>
        /// <para type="description">
        /// The username for the replication connection.
        /// </para>
        /// </summary>
        [Parameter]
        public string MySqlUser { get; set; }

        /// <summary>
        /// <para type="description">
        /// Whether or not to check the master’s Common Name value
        /// in the certificate that it sends during the SSL handshake.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter MySqlVerifyCertificate { get; set; }

        protected override void ProcessRecord()
        {
            ReplicaConfiguration config = new ReplicaConfiguration
            {
                FailoverTarget = FailoverTarget,
                MysqlReplicaConfiguration = new MySqlReplicaConfiguration
                {
                    CaCertificate = MySqlCaCert,
                    ClientCertificate = MySqlClientCert,
                    ClientKey = MySqlClientKey,
                    ConnectRetryInterval = MySqlRetryInterval,
                    DumpFilePath = MySqlDumpPath,
                    MasterHeartbeatPeriod = MySqlHeartbeatPeriod,
                    Password = MySqlPassword,
                    SslCipher = MySqlSslCipher,
                    Username = MySqlUser,
                    VerifyServerCertificate = MySqlVerifyCertificate,
                }
            };
            WriteObject(config);
        }
    }
}
