. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

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
        $config = New-GcSqlInstanceReplicaConfig -MySqlVerifyCertificate -MySqlCaCert "Certificate"
        $config.MysqlReplicaConfiguration.VerifyServerCertificate | Should Be true
        $config.MysqlReplicaConfiguration.CaCertificate | Should Be "Certificate"
    }
}

Describe "New-GcSqlSettingConfig" {

    It "should be able to instantiate itself with just Tier passed in" {
        $setting = New-GcSqlSettingConfig "D1"
        $setting.Tier | Should Be "D1"
        $setting.DataDiskSizeGb | Should Be 50
        $setting.DataDiskType | Should be "PD_SSD"
        $setting.DatabaseFlags | Should BeNullOrEmpty
    }

    It "should be able to accept parameters with defaults and adjust accordingly" {
        $setting = New-GcSqlSettingConfig "D1" -DataDiskSizeGb 11
        $setting.DataDiskSizeGb | Should Be 11
    }

    It "should be able to take in something that isn't default" {
        $setting = New-GcSqlSettingConfig "D1" -MaintenanceWindowDay 1
        $setting.MaintenanceWindow.Day = 1
    }

    It "should be able to take in database flags" {
        $setting = New-GcSqlSettingConfig "D1" `
            -DatabaseFlag `
            @{"Name" = "binlog_checksum"; "Value" = "NONE"},@{"Name" = "ft_max_word_len"; "Value" = 12}
        $flags = $setting.DatabaseFlags
        $flags.Count | Should Be 2
        $flag = $flags | Select-Object -first 1
        $flag.Name | Should Be "binlog_checksum"
        $flag.Value | Should be "NONE"
    }

    It "should be able to take in an IP Configuration Network" {
        $setting = New-GcSqlSettingConfig "D1" `
            -IpConfigAuthorizedNetwork `
            @{"ExpirationTimeRaw" = "2012-11-15T16:19:00.094Z"; "Kind" = "sql::aclEntry"; "Name" = "test"; }, `
            @{"ExpirationTimeRaw" = "2012-11-15T16:19:00.094Y"; "Kind" = "sql::aclEntry"; "Name" = "test2"; }
        $networks = $setting.IpConfiguration.AuthorizedNetworks
        $networks.Count | Should Be 2
        $network = $networks | Select-Object -first 1
        $network.Name | Should Be "test"
    }
}

Describe "New-GcSqlInstanceConfig" {
    It "should be able to instantiate with just the most basic information" {
        $setting = New-GcSqlSettingConfig "D1"
        $config = New-GcSqlInstanceConfig "test-inst" -SettingConfig $setting
        $config.Settings.Tier | Should Be "D1"
        $config.Name | Should Be "test-inst"
    }

    It "should be able to replace the region" {
        $setting = New-GcSqlSettingConfig "D1"
        $config = New-GcSqlInstanceConfig "test-inst" -Region "us-central2" -SettingConfig $setting
        $config.Region | Should Be "us-central2"
    }

    It "should be able to take in a pipelined Setting" {
        $config = New-GcSqlSettingConfig "D2" | New-GcSqlInstanceConfig "test-inst"
        $config.Settings.Tier | Should Be "D2"
    }

    It "should be able to take in a pipelined replica config" {
        $setting = New-GcSqlSettingConfig "D2" 
        $config = New-GcSqlInstanceReplicaConfig -MySqlVerifyCertificate -MySqlCaCert "Certificate"`
            | New-GcSqlInstanceConfig "test-inst" -SettingConfig $setting
        $config.Settings.Tier | Should Be "D2"
        $config.ReplicaConfiguration.MysqlReplicaConfiguration.CaCertificate | Should Be "Certificate"
    }

}

Reset-GCloudConfig $oldActiveConfig $configName
