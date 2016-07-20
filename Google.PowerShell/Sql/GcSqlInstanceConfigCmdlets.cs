// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.SQLAdmin.v1beta4.Data;
using Google.Apis.SQLAdmin.v1beta4;
using Google.PowerShell.Common;

namespace Google.PowerShell.Sql
{
    /// TODO: Update the configuration cmdlets once the patch/update problem is figured out.
    /// Currently they are only meant for Add-GcSqlInstance

    /// <summary>
    /// <para type="synopsis">
    /// Makes a new Google Cloud SQL instance description.
    /// </para>
    /// <para type="description"> 
    /// Makes a new Google Cloud SQL instance description.
    /// Use Add-GcSqlInstance to instantiate the instance within a project.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcSqlInstanceConfig")]
    public class NewGcSqlInstanceConfigCmdlet : GcSqlCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project ID of the project containing the Cloud SQL instance. The Google apps domain is prefixed if applicable.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the Cloud SQL instance. This does not include the project ID. 
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The user settings. Can be created with New-GcSqlSettingConfig.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        public Settings SettingConfig { get; set; }


        /// <summary>
        /// <para type="description">
        /// The database engine type and version.
        /// The databaseVersion can not be changed after instance creation.
        /// Can be MYSQL_5_5, MYSQL_5_6 or MYSQL_5_7.
        /// Defaults to MYSQL_5_6.
        /// MYSQL_5_7 is applicable only to Second Generation instances.
        /// </para>
        /// </summary>
        [Parameter]
        public string DatabaseVer { get; set; } = "MYSQL_5_6";

        /// <summary>
        /// <para type="description">
        /// The name of the instance which will act as master in the replication setup.
        /// </para>
        /// </summary>
        [Parameter]
        public string MasterInstanceName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the failover replica. If specified at instance creation, a failover replica is created for the instance.
        /// This property is applicable only to Second Generation instances.
        /// </para>
        /// </summary>
        [Parameter]
        public string FailoverReplica { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ReplicaConfiguration created by New-GcSqlInstanceReplicaConfig
        /// </para>
        /// </summary>
        [Parameter(ValueFromPipeline = true)]
        public ReplicaConfiguration ReplicaConfig { get; set; }

        /// <summary>
        /// <para type="description">
        ///  The geographical region.
        ///  Can be us-central(FIRST_GEN instances only),
        ///  us-central1(SECOND_GEN instances only), asia-east1 or europe-west1.
        ///  Defaults to 
        ///  us-central1 depending on the instance type.
        ///  The region can not be changed after instance creation.
        ///  
        /// Defaults to us-central1
        /// </para>
        /// </summary>
        [Parameter]
        public string Region { get; set; } = "us-central1";

        protected override void ProcessRecord()
        {
            DatabaseInstance instance = new DatabaseInstance
            {
                ReplicaConfiguration = ReplicaConfig,
                Settings = SettingConfig,
                MasterInstanceName = MasterInstanceName,
                Name = Name,
                Region = Region,
                BackendType = "SECOND_GEN",
                Project = Project,
                DatabaseVersion = DatabaseVer,
                InstanceType = "CLOUD_SQL_INSTANCE",
                Kind = "sql#instance",
                State = "RUNNABLE"
            };
            if (FailoverReplica != null) {
                instance.FailoverReplica = new DatabaseInstance.FailoverReplicaData
                {
                    Name = FailoverReplica
                };
            }
            
            WriteObject(instance);
        }
    }
}
