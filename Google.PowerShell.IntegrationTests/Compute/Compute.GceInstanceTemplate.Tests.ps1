. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$image = "projects/windows-cloud/global/images/family/windows-2012-r2"
$machineType = "f1-micro"

Get-GceInstanceTemplate | Remove-GceInstanceTemplate

Describe "Get-GceInstanceTemplate" {

    $r = Get-Random
    $name = "test-get-template-$r"
    $name2 = "test-get-template2-$r"
    It "should fail not existing" {
        { Get-GceInstanceTemplate -Name $name } | Should Throw 404
    }

    It "should fail wrong project" {
        { Get-GceInstanceTemplate $name -Project "asdf" } | Should Throw 403
    }

    Add-GceInstanceTemplate -Name $name -MachineType $machineType -BootDiskImage $image
    Add-GceInstanceTemplate -Name $name2 -MachineType n1-standard-1 -BootDiskImage $image

    It "should get one" {
        $template = Get-GceInstanceTemplate $name
        $template.Count | Should Be 1
        $template.Name | Should Be $name
        $template.Properties.MachineType | Should Be $machineType
    }

    It "should get both" {
        $templates = Get-GceInstanceTemplate
        $templates.Count | Should Be 2
    }

    Get-GceInstanceTemplate | Remove-GceInstanceTemplate
}

Describe "New-GceServiceAccountConfig" {
    $email = "email@address.tld"

    It "should get defaults" {
        $account = New-GceServiceAccountConfig -Email $email
        $account.Scopes.Count | Should Be 5
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

    It "should set bigquery" {
        $account = New-GceServiceAccountConfig $email -Storage None -CloudLogging None -CloudMonitoring None `
            -ServiceControl $false -ServiceManagement $false -BigQuery
        $account.Scopes -match "bigquery" | Should Be $true
    }
}

Describe "Add-GceInstanceTemplate" {
    
    $r = Get-Random
    $name = "test-add-template-$r"
    $nmae2 = "test-add-template2-$r"
    $serviceAccount = New-GceServiceAccountConfig default -BigQuery

    It "should work" {
        Add-GceInstanceTemplate $name -MachineType $machineType -BootDiskImage $image -CanIpForward `
            -Metadata @{"key" = "value"} -Description "desc" -Network default -NoExternalIp -Preemptible `
            -Killable -TerminateOnMaintenance -Tag alpha, beta -ServiceAccount $serviceAccount
        $template = Get-GceInstanceTemplate $name
        $template.Name | Should Be $name
        $prop = $template.Properties
        $prop.CanIpForward | Should Be $true
        $prop.Description | Should Be "desc"
        $prop.Disks.Count | Should Be 1
        $prop.Disks.Boot | Should Be $true
        $prop.Disks.InitializeParams.SourceImage -match $image | Should Be $false
        $prop.Disks.InitializeParams.SourceImage -match "windows-server-2012" | Should Be $true
        $prop.MachineType | Should Be $machineType
        $prop.Metadata.Items.Count | Should Be 1
        $prop.Metadata.Items.Key | Should Be "key"
        $prop.Metadata.Items.Value | Should Be "value"
        $prop.NetworkInterfaces.Network -match "default" | Should Be $true
        $prop.NetworkInterfaces.AccessConfigs | Should Be $null
        $prop.Scheduling.AutomaticRestart | Should Be $false
        $prop.Scheduling.OnHostMaintenance | Should Be "TERMINATE"
        $prop.Scheduling.Preemptible | Should Be $true
        $prop.Tags.Items.Count | Should Be 2
        $prop.ServiceAccounts.Email | Should Be "default"
        ($prop.ServiceAccounts.Scopes -match "bigquery").Count | Should Be 1
    }

    It "should work with object" {
        $oldTemplate = Get-GceInstanceTemplate $name
        $oldTemplate.Name = $name2
        Add-GceInstanceTemplate -Object $oldTemplate
        $newTemplate = Get-GceInstanceTemplate $name2
        $newTemplate.Name | Should Be $name2
        $newTemplate.Properties | Should Not Be $oldTemplate.Properties
        (Compare-Object $newTemplate.Properties $oldTemplate.Properties).Count | Should Be 0
    }

    Get-GceInstanceTemplate | Remove-GceInstanceTemplate
}

Describe "Remove-GceInstanceTemplate" {
    
    $r = Get-Random
    $name = "test-remove-template-$r"

    It "should fail" {
        { Remove-GceInstanceTemplate $name } | Should Throw 404
    }
    
    Context "Real Remove" {
        BeforeEach {
            Add-GceInstanceTemplate -Name $name -MachineType $machineType -BootDiskImage $image
        }

        It "should work" {
            Remove-GceInstanceTemplate $name
            { Get-GceInstanceTemplate $name } | Should Throw 404
        }

        It "should work with object" {
            Get-GceInstanceTemplate $name | Remove-GceInstanceTemplate
            { Get-GceInstanceTemplate $name } | Should Throw 404
        }
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
