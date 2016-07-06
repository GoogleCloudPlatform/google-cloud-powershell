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
        public OverallSettings SettingObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the instance which will act as master in the replication setup.
        /// </para>
        /// </summary>
        [Parameter]
        public string MasterInstanceName { get; set; }

        /// <summary>
        /// <para type="description">
        ///  The geographical region.
        ///  Can be us-central(FIRST_GEN instances only),
        ///  us-central1(SECOND_GEN instances only), asia-east1 oreurope-west1.
        ///  Defaults to 
        ///  us-central1 depending on the instance type.
        ///  The region can not be changed after instance creation.
        /// </para>
        /// </summary>
        [Parameter]
        public string Region = "us-central1";

        protected override void ProcessRecord()
        {
            DatabaseInstance instance = new DatabaseInstance();
            instance.ReplicaConfiguration = SettingObject.ReplicaConfig;
            instance.Settings = SettingObject.SettingConfig;
            instance.MasterInstanceName = MasterInstanceName;
            instance.Name = Name;
            instance.Region = Region;
            WriteObject(instance);
        }
    }
}
