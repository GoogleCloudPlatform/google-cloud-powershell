$path = $PSScriptRoot

while($child -eq $null) {
    $child = Get-ChildItem GcloudCmdlets.ps1 -Recurse -Path $path 
    $path = Split-Path $path
}

. $child.FullName
Install-GcloudCmdlets
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "New-GceServiceAccountConfig" {
    $email = "email@address.tld"

    It "should get defaults" {
        $account = New-GceServiceAccountConfig -Email $email
        $account.Scopes.Count | Should Be 5
        ($account.Scopes -match "logging.write").Count | Should Be 1
        ($account.Scopes -match "monitoring.write").Count | Should Be 1
        ($account.Scopes -match "servicecontrol").Count | Should Be 1
        ($account.Scopes -match "service.management").Count | Should Be 1
        ($account.Scopes -match "devstorage.read_only").Count | Should Be 1
    }

    It "should use default service account if -Email is not provided" {
        $account = New-GceServiceAccountConfig
        $account.Scopes.Count | Should Be 5
        ($account.Scopes -match "logging.write").Count | Should Be 1
        ($account.Scopes -match "monitoring.write").Count | Should Be 1
        ($account.Scopes -match "servicecontrol").Count | Should Be 1
        ($account.Scopes -match "service.management").Count | Should Be 1
        ($account.Scopes -match "devstorage.read_only").Count | Should Be 1
        $account.Email | Should Match "-compute@developer.gserviceaccount.com"
    }

    It "should get none" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false
        $account.Scopes.Count | Should Be 0
    }

    It "should set bigquery" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -BigQuery
        $account.Scopes -match "bigquery" | Should Be $true
    }

    It "should set bigtable admin tables" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -BigTableAdmin Tables
        $account.Scopes -match "bigtable.admin.table" | Should Be $true
    }

    It "should set bigtable admin full" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -BigTableAdmin Full
        $account.Scopes -match "bigtable.admin" | Should Be $true
        $account.Scopes -match "bigtable.admin.table" | Should BeNullOrEmpty 
    }

    It "should set bigtable data read" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -BigTableData Read
        $account.Scopes -match "bigtable.data.readonly" | Should Be $true
    }

    It "should set bigtable data readwrite" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -BigTableData ReadWrite
        $account.Scopes -match "bigtable.data" | Should Be $true
        $account.Scopes -match "bigtable.data.readonly" | Should BeNullOrEmpty 
    }

    It "should set cloud data store" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -CloudDataStore
        $account.Scopes -match "datastore" | Should Be $true
    }

    It "should set cloud logging write" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -CloudLogging Write
        $account.Scopes -match "logging.write" | Should Be $true
    }

    It "should set cloud logging read" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -CloudLogging Read
        $account.Scopes -match "logging.read" | Should Be $true
    }

    It "should set cloud logging full" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -CloudLogging Full
        $account.Scopes -match "logging.admin" | Should Be $true
    }

    It "should set cloud monitoring write" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None `
            -ServiceControl $false -ServiceManagement $false -CloudMonitoring Write
        $account.Scopes -match "monitoring.write" | Should Be $true
    }

    It "should set cloud monitoring read" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None `
            -ServiceControl $false -ServiceManagement $false -CloudMonitoring Read
        $account.Scopes -match "monitoring.read" | Should Be $true
    }

    It "should set cloud monitoring full" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None `
            -ServiceControl $false -ServiceManagement $false -CloudMonitoring Full
        $account.Scopes -match "monitoring" | Should Be $true
        $account.Scopes -match "monitoring.read" | Should BeNullOrEmpty 
        $account.Scopes -match "monitoring.write" | Should BeNullOrEmpty 
    }

    It "should set cloud pub/sub" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -CloudPubSub
        $account.Scopes -match "pubsub" | Should Be $true
    }

    It "should set cloud sql" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -CloudSql
        $account.Scopes -match "sqlservice.admin" | Should Be $true
    }

    It "should set compute read" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -Compute Read
        $account.Scopes -match "compute.readonly" | Should Be $true
    }

    It "should set compute readwrite" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -Compute ReadWrite
        $account.Scopes -match "compute" | Should Be $true
        $account.Scopes -match "compute.readonly" | Should BeNullOrEmpty 
    }

    It "should set storage read" {
        $account = New-GceServiceAccountConfig $email -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -Storage Read
        $account.Scopes -match "devstorage.read_only" | Should Be $true
    }

    It "should set storage write" {
        $account = New-GceServiceAccountConfig $email -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -Storage Write
        $account.Scopes -match "devstorage.write_only" | Should Be $true
    }

    It "should set storage read/write" {
        $account = New-GceServiceAccountConfig $email -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -Storage ReadWrite
        $account.Scopes -match "devstorage.read_write" | Should Be $true
    }

    It "should set storage full" {
        $account = New-GceServiceAccountConfig $email -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -Storage Full
        $account.Scopes -match "devstorage.full_control" | Should Be $true
    }

    It "should set task queue" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -TaskQueue
        $account.Scopes -match "taskqueue" | Should Be $true
    }

    It "should set user info" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -UserInfo
        $account.Scopes -match "userinfo.email" | Should Be $true
    }

    It "should set arbitrary uri" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -ScopeUri "scopeuri1", "scopeuri2"
        $account.Scopes.Count | Should Be 2
        ($account.Scopes -match "scopeuri1").Count | Should Be 1
        ($account.Scopes -match "scopeuri2").Count | Should Be 1
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
