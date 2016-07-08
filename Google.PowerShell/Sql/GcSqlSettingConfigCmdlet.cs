// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System.Collections.Generic;
using System.Management.Automation;
using Google.Apis.SQLAdmin.v1beta4.Data;

namespace Google.PowerShell.Sql
{
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


        public enum ActivationPolicy
        {
            ALWAYS,
            NONE
        }

        /// <summary>
        /// <para type="description">
        /// The activation policy specifies when the instance is activated;
        /// it is applicable only when the instance state is RUNNABLE. Can be ALWAYS, or NEVER. 
        /// Defaults to ALWAYS
        /// </para>
        /// </summary>
        [Parameter]
        public ActivationPolicy Policy { get; set; } = ActivationPolicy.ALWAYS;

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
        public string BackupConfigStartTime { get; set; } = "22:00";


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
        public DatabaseFlags[] DatabaseFlag{ get; set; }

        /// <summary>
        /// <para type="description">
        /// The list of external networks that are allowed to connect to the instance using the IP.
        /// In CIDR notation, also known as 'slash' notation (e.g. 192.168.100.0/24).
        /// May include other ipConfiguration params, but unsure.
        /// Defaults to an empty list.
        /// </para>
        /// </summary>
        [Parameter]
        public AclEntry[] IpConfigAuthorizedNetwork { get; set; }

        /// <summary>
        /// <para type="description">
        /// Whether the instance should be assigned an IP address or not.
        /// Defaults to false.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter IpConfigIpv4Enabled { get; set; } = false;

        /// <summary>
        /// <para type="description">
        /// Whether the mysqld should default to “REQUIRE X509” for users connecting over IP.
        /// Defaults to false.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter IpConfigRequireSsl { get; set; } = false;

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
        public SwitchParameter StorageAutoResize { get; set; } = false;

        public enum DataDiskType {
            PD_SSD,
            PD_HDD
        }
        /// <summary>
        /// <para type="description">
        /// The type of data disk: PD_SSD (default) or PD_HDD.
        /// Applies only to Second Generation instances.
        /// </para>
        /// </summary>
        [Parameter]
        public DataDiskType DiskType { get; set; } = DataDiskType.PD_SSD;

        /// <summary>
        /// <para type="description">
        /// Configuration specific to read replica instances. 
        /// Indicates whether replication is enabled or not.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter DatabaseReplicationEnabled { get; set; }

        protected override void ProcessRecord()
        {
            Settings settings = new Settings
            {
                Tier = TierConfig,
                PricingPlan = "PER_USE",
                ActivationPolicy = Policy.ToString(),
                BackupConfiguration = new BackupConfiguration
                {
                    BinaryLogEnabled = BinaryLogEnabled,
                    Enabled = BackupConfigEnabled,
                    StartTime = BackupConfigStartTime
                },
                DataDiskSizeGb = DataDiskSizeGb,
                DatabaseFlags = DatabaseFlag,
                IpConfiguration = new IpConfiguration
                {
                    AuthorizedNetworks = IpConfigAuthorizedNetwork,
                    Ipv4Enabled = IpConfigIpv4Enabled,
                    RequireSsl = IpConfigRequireSsl
                },
                LocationPreference = new LocationPreference
                {
                    FollowGaeApplication = LocationPreferenceFollowGae,
                    Zone = LocationPreferenceZone,
                },
                MaintenanceWindow = new MaintenanceWindow
                {
                    Day = MaintenanceWindowDay,
                    Hour = MaintenanceWindowHour,
                },
                StorageAutoResize = StorageAutoResize,
                DataDiskType = DiskType.ToString(),
                DatabaseReplicationEnabled = DatabaseReplicationEnabled
            };
            WriteObject(settings);
        }
    }
}
