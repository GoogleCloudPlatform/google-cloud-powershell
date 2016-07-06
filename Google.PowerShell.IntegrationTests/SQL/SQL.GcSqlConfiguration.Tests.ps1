. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

Describe "New-GcSqlInstanceReplicaConfig" {
    It "shouldn't require any parameters" {
        $config = New-GcSqlInstanceReplicaConfig
        $config.MysqlReplicaConfiguration.ConnectRetryInterval | Should Be 60
        $config.MysqlReplicaConfiguration.CaCertificate | Should BeNullOrEmpty
    }

    It "should be able to update the default value" {
        $config = New-GcSqlInstanceReplicaConfig -MySqlRetryInterval 100
        $config.MysqlReplicaConfiguration.ConnectRetryInterval | Should Be 100
    }

    It "should be able to take in other parameters" {
        $config = New-GcSqlInstanceReplicaConfig -MySqlVerifyCertificate $true -MySqlCaCert "Certificate"
        $config.MysqlReplicaConfiguration.VerifyServerCertificate | Should Be true
        $config.MysqlReplicaConfiguration.CaCertificate | Should Be "Certificate"
    }
}

Describe "New-GcSqlSettingConfig" {

    It "should be able to instantiate itself with just Tier passed in" {
        $settingObject = New-GcSqlSettingConfig "D1"
        $setting = $settingObject.SettingConfig
        $setting.Tier | Should Be "D1"
        $setting.DataDiskSizeGb | Should Be 10
        $setting.DataDiskType | Should be "PD_SSD"
        $setting.DatabaseFlags | Should BeNullOrEmpty
    }

    It "should be able to accept parameters with defaults and adjust accordingly" {
        $settingObject = New-GcSqlSettingConfig "D1" -DataDiskSizeGb 11
        $settingObject.SettingConfig.DataDiskSizeGb | Should Be 11
    }

    It "should be able to take in something that isn't default" {
        $settingObject = New-GcSqlSettingConfig "D1" -MaintenanceWindowDay 1
        $settingObject.SettingConfig.MaintenanceWindow.Day = 1
    }

    It "should be able to take in database flags" {
        $settingObject = New-GcSqlSettingConfig "D1" `
            -DatabaseFlagList `
            @{"Name" = "binlog_checksum"; "Value" = "NONE"},@{"Name" = "ft_max_word_len"; "Value" = 12}
        $flags = $settingObject.SettingConfig.DatabaseFlags
        $flags.Count | Should Be 2
        $flag = $flags | Select-Object -first 1
        $flag.Name | Should Be "binlog_checksum"
        $flag.Value | Should be "NONE"
    }

    It "should be able to take in an IP Configuration Network" {
        $settingObject = New-GcSqlSettingConfig "D1" `
            -IpConfigAuthorizedNetworks `
            @{"ExpirationTimeRaw" = "2012-11-15T16:19:00.094Z"; "Kind" = "sql::aclEntry"; "Name" = "test"; }, `
            @{"ExpirationTimeRaw" = "2012-11-15T16:19:00.094Y"; "Kind" = "sql::aclEntry"; "Name" = "test2"; }
        $networks = $settingObject.SettingConfig.IpConfiguration.AuthorizedNetworks
        $networks.Count | Should Be 2
        $network = $networks | Select-Object -first 1
        $network.Name | Should Be "test"
    }

    It "should be able to take in an InstanceReplicaConfig" {
        $config = New-GcSqlInstanceReplicaConfig -MySqlVerifyCertificate $true
        $settingObject = New-GcSqlSettingConfig "D1" -ReplicaConfig $config
        $settingObject.ReplicaConfig.MysqlReplicaConfiguration.VerifyServerCertificate | Should Be true
    }

    It "should be able to take in an InstanceReplicaConfig from the pipeline" {
        $settingObject = New-GcSqlInstanceReplicaConfig -MySqlCaCert "Certificate" | New-GcSqlSettingConfig "D1"
        $settingObject.ReplicaConfig.MysqlReplicaConfiguration.CaCertificate | Should Be "Certificate"
    }

    It "should be able to take in an InstanceReplicaConfig from the pipeline and other parameters" {
        $settingObject = New-GcSqlInstanceReplicaConfig -MySqlCaCert "Certificate2" | New-GcSqlSettingConfig "D1" -MaintenanceWindowDay 1
        $settingObject.ReplicaConfig.MysqlReplicaConfiguration.CaCertificate | Should Be "Certificate2"
        $settingObject.SettingConfig.MaintenanceWindow.Day = 1
    }

    It "shouldn't have an InstanceReplicaConfig if it wasn't passed one" {
        $settingObject = New-GcSqlSettingConfig "D1"
        $settingObject.ReplicaConfig | Should BeNullOrEmpty
    }

}

Describe "New-GcSqlInstanceConfig" {
    It "should be able to instantiate with just the most basic information" {
        $settingObject = New-GcSqlSettingConfig "D1"
        $config = New-GcSqlInstanceConfig "test-inst" -SettingObject $settingObject
        $config.Settings.Tier | Should Be "D1"
        $config.Name | Should Be "test-inst"
        $config.Region | Should Be "us-central1"
    }

    It "should be able to replace the region" {
        $settingObject = New-GcSqlSettingConfig "D1"
        $config = New-GcSqlInstanceConfig "test-inst" -Region "us-central2" -SettingObject $settingObject
        $config.Region | Should Be "us-central2"
    }

    It "should be able to take in a pipelined Setting Object" {
        $config = New-GcSqlSettingConfig "D2" | New-GcSqlInstanceConfig "test-inst"
        $config.Settings.Tier | Should Be "D2"
    }

    It "should be able to get the relevant information from the SettingObject" {
        $settingObject = New-GcSqlInstanceReplicaConfig -MySqlCaCert "Certificate2" | New-GcSqlSettingConfig "D1" -MaintenanceWindowDay 1
        $config = New-GcSqlInstanceConfig "test-inst" -SettingObject $settingObject
        $config.Settings.Tier | Should Be "D1"
        $config.ReplicaConfiguration.MySqlReplicaConfiguration.CaCertificate | Should Be "Certificate2"
    }
}
