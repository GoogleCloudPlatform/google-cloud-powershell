// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System.Collections.Generic;
using System.Management.Automation;
using Google.Apis.SQLAdmin.v1beta4.Data;

namespace Google.PowerShell.Sql
{
    /// <summary>
    /// <para type="synopsis">
    /// Wraps the replica configuration and setting configuration together for New-GcSqlInstanceConfig
    /// </para>
    /// <para type="description"> 
    /// Object that is passed into New-GcSqlInstanceConfig in order to create or update instances.
    /// Contains the settings config made with New-GcSqlSettingConfig
    /// and the replica configuration made with New-GcSqlInstanceReplicaConfig
    /// </para>
    /// </summary>
    public class OverallSettings
    {
        /// <summary>
        /// <para type="description">
        /// The Settings created by New-GcSqlSettingConfig
        /// </para>
        /// </summary>
        public Settings SettingConfig { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ReplicaConfiguration created by New-GcSqlInstanceReplicaConfig
        /// and passed into New-GcSqlSettingConfig.
        /// </para>
        /// </summary>
        public ReplicaConfiguration ReplicaConfig { get; set; }

        /// <summary>
        /// <para type="description">
        /// Wraps the two configurations together.
        /// </para>
        /// </summary>
        public OverallSettings(Settings newSettingConfig, ReplicaConfiguration newReplicaConfig)
        {
            SettingConfig = newSettingConfig;
            ReplicaConfig = newReplicaConfig;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Makes a new Google Cloud SQL Instance Settings configuration for a second generation instance.
    /// </para>
    /// <para type="description"> 
    /// Creates a settings configuration specified by the passed in parameters. 
    /// Meant to be only for Second generation instances.
    /// Can be pipelined into New-GcSqlInstanceConfig.
    /// 
    /// WARNING: If a parameter passed in is bad, or wrong, this cmdlet will not error.
    /// Instead it will error once you try to update or add an instance in your project.
    /// Please be careful with inputs.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcSqlSettingConfig")]
    public class NewGcSqlSettingConfigCmdlet : GcSqlCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The tier of service for this instance, for example D1, D2.
        /// Pricing information is available at https://cloud.google.com/sql/pricing.
        /// Get-GcSqlTiers will also tell you what tiers are available for your project.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string TierConfig { get; set; }

        /// <summary>
        /// <para type="description">
        /// The activation policy specifies when the instance is activated;
        /// it is applicable only when the instance state is RUNNABLE. Can be ALWAYS, or NEVER. 
        /// Defaults to ALWAYS
        /// </para>
        /// </summary>
        [Parameter]
        public string ActivationPolicy = "ALWAYS";

        /// <summary>
        /// <para type="description">
        /// Whether binary log is enabled.
        /// If backup configuration is disabled, binary log must be disabled as well.
        /// Defaults to true;
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public bool BinaryLogEnabled = true;

        /// <summary>
        /// <para type="description">
        /// Whether the backup configuration is enabled or not.
        /// Defaults to true;
        /// </para>
        /// </summary>
        [Parameter]
        public bool BackupConfigEnabled = true;

        /// <summary>
        /// <para type="description">
        /// Start time for the daily backup configuration in UTC timezone in the 24 hour format - HH:MM
        /// Defaults to 22:00
        /// </para>
        /// </summary>
        [Parameter]
        public string BackupConfigStartTime = "22:00";


        /// <summary>
        /// <para type="description">
        /// The size of data disk, in GB. The data disk size minimum is 10 GB. 
        /// Applies only to Second generation instances.
        /// Defaults to 10
        /// </para>
        /// </summary>
        [Parameter]
        public long DataDiskSizeGb = 10;

        /// <summary>
        /// <para type="description">
        /// The database flags passed to the instance at startup.
        /// Defaults to an empty list.
        /// </para>
        /// </summary>
        [Parameter]
        public List<DatabaseFlags> DatabaseFlagList { get; set; }

        /// <summary>
        /// <para type="description">
        /// The list of external networks that are allowed to connect to the instance using the IP.
        /// In CIDR notation, also known as 'slash' notation (e.g. 192.168.100.0/24).
        /// May include other ipConfiguration params, but unsure.
        /// Defaults to an empty list.
        /// </para>
        /// </summary>
        [Parameter]
        public List<AclEntry> IpConfigAuthorizedNetworks { get; set; }

        /// <summary>
        /// <para type="description">
        /// Whether the instance should be assigned an IP address or not.
        /// Defaults to false.
        /// </para>
        /// </summary>
        [Parameter]
        public bool IpConfigIpv4Enabled = false;

        /// <summary>
        /// <para type="description">
        /// Whether the mysqld should default to “REQUIRE X509” for users connecting over IP.
        /// Defaults to false.
        /// </para>
        /// </summary>
        [Parameter]
        public bool IpConfigRequireSsl = false;

        /// <summary>
        /// <para type="description">
        /// The AppEngine application to follow, it must be in the same region as the Cloud SQL instance.
        /// </para>
        /// </summary>
        [Parameter]
        public string LocationPreferenceFollowGae { get; set; }

        /// <summary>
        /// <para type="description">
        /// The preferred Compute Engine Zone (e.g. us-central1-a, us-central1-b, etc.).
        /// </para>
        /// </summary>
        [Parameter]
        public string LocationPreferenceZone { get; set; }

        /// <summary>
        /// <para type="description">
        /// Day of the week (1-7) starting monday that the instance may be restarted for maintenance purposes.
        /// Applies only to Second Generation instances.
        /// Defaults to 5 (Friday).
        /// </para>
        /// </summary>
        [Parameter]
        public int MaintenanceWindowDay { get; set; }

        /// <summary>
        /// <para type="description">
        /// Hour of day (0-23) that the instance may be restarted for maintenance purposes.
        /// Applies only to Second Generation instances.
        /// Defaults to 22;
        /// </para>
        /// </summary>
        [Parameter]
        public int MaintenanceWindowHour { get; set; }



        /// <summary>
        /// <para type="description">
        /// Configuration to increase storage size automatically.
        /// The default value is false.
        /// Applies only to Second Generation instances.
        /// </para>
        /// </summary>
        [Parameter]
        public bool StorageAutoResize = false;

        /// <summary>
        /// <para type="description">
        /// The type of data disk: PD_SSD (default) or PD_HDD.
        /// Applies only to Second Generation instances.
        /// </para>
        /// </summary>
        [Parameter]
        public string DataDiskType = "PD_SSD";

        /// <summary>
        /// <para type="description">
        /// Configuration specific to read replica instances. 
        /// Indicates whether replication is enabled or not.
        /// </para>
        /// </summary>
        [Parameter]
        public bool DatabaseReplicationEnabled { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ReplicaConfiguration created by New-GcSqlInstanceReplicaConfig
        /// </para>
        /// </summary>
        [Parameter(ValueFromPipeline = true)]
        public ReplicaConfiguration ReplicaConfig { get; set; }

        protected override void ProcessRecord()
        {
            Settings settings = new Settings();
            settings.Tier = TierConfig;
            settings.PricingPlan = "PER_USE";
            settings.ActivationPolicy = ActivationPolicy;
            settings.BackupConfiguration = new BackupConfiguration();
            settings.BackupConfiguration.BinaryLogEnabled = BinaryLogEnabled;
            settings.BackupConfiguration.Enabled = BackupConfigEnabled;
            settings.BackupConfiguration.StartTime = BackupConfigStartTime;
            settings.DataDiskSizeGb = DataDiskSizeGb;
            settings.DatabaseFlags = DatabaseFlagList;
            settings.IpConfiguration = new IpConfiguration();
            settings.IpConfiguration.AuthorizedNetworks = IpConfigAuthorizedNetworks;
            settings.IpConfiguration.Ipv4Enabled = IpConfigIpv4Enabled;
            settings.IpConfiguration.RequireSsl = IpConfigRequireSsl;
            settings.LocationPreference = new LocationPreference();
            settings.LocationPreference.FollowGaeApplication = LocationPreferenceFollowGae;
            settings.LocationPreference.Zone = LocationPreferenceZone;
            settings.MaintenanceWindow = new MaintenanceWindow();
            settings.MaintenanceWindow.Day = MaintenanceWindowDay;
            settings.MaintenanceWindow.Hour = MaintenanceWindowHour;
            settings.StorageAutoResize = StorageAutoResize;

            if (DataDiskType != null)
            {
                settings.DataDiskType = DataDiskType;
            }
            else 
            {
                settings.DatabaseReplicationEnabled = DatabaseReplicationEnabled;
            }
            OverallSettings FullConfig = new OverallSettings(settings, ReplicaConfig);

            WriteObject(FullConfig);
        }
    }
}
