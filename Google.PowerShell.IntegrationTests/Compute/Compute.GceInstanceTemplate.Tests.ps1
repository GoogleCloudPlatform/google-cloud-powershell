. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$image = Get-GceImage -Family "windows-2012-r2"
$machineType = "f1-micro"

# Delete all instance templates for the current project.
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
        ($template | gm).TypeName | ForEach-Object { $_ | Should Be "Google.Apis.Compute.v1.Data.InstanceTemplate" }
        $template.Count | Should Be 1
        $template.Name | Should Be $name
        $template.Properties.MachineType | Should Be $machineType
    }

    It "should get both" {
        $templates = Get-GceInstanceTemplate
        $templates.Count | Should Be 2
        ($templates.Name -match $name).Count | Should Be 1
        ($templates.Name -match $name2).Count | Should Be 1
    }

    Get-GceInstanceTemplate | Remove-GceInstanceTemplate
}

Describe "Add-GceInstanceTemplate" {
    
    $r = Get-Random
    $name = "test-add-template-$r"
    $name2 = "test-add-template2-$r"
    $name3 = "test-add-template3-$r"

    $serviceAccount = New-GceServiceAccountConfig default -BigQuery

    It "should work" {
        Add-GceInstanceTemplate $name $machineType -BootDiskImage $image -CanIpForward `
            -Metadata @{"key" = "value"} -Description "desc" -Network default -NoExternalIp -Preemptible `
            -TerminateOnMaintenance -Tag alpha, beta -ServiceAccount $serviceAccount
        $template = Get-GceInstanceTemplate $name
        $template.Name | Should Be $name
        $prop = $template.Properties
        $prop.CanIpForward | Should Be $true
        $prop.Description | Should Be "desc"
        $prop.Disks.Count | Should Be 1
        $prop.Disks.Boot | Should Be $true
        $prop.Disks.InitializeParams.SourceImage -match $image | Should Be $false
        $prop.Disks.InitializeParams.SourceImage -match "windows" | Should Be $true
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

    It "should work with subnet" {
        $r = Get-Random
        $newNetwork = "test-network-$r"
        $templateName = "test-add-gceinstancetemplate-$r"

        try {
            # Create a network and extract out subnet that corresponds to region "us-central1".
            $newNetwork = "test-network-$r"
            gcloud compute networks create $newNetwork 2>$null
            $region = "us-central1"
            $zone = "us-central1-a"
            $network = Get-GceNetwork $newNetwork
            $subnet = $network.Subnetworks | Where-Object {$_.Contains($region)}
            $subnet -match "subnetworks/([^/]*)" | Should Be $true
            $subnetName = $Matches[1]

            Add-GceInstanceTemplate -Name $templateName -BootDiskImage $image -Region $region -Network $network -Subnet $subnetName
            $template = Get-GceInstanceTemplate $templateName
            $template.Properties.NetworkInterfaces.Network | Should Match $newNetwork
            $template.Properties.NetworkInterfaces.Subnetwork | Should Match $subnetName
        }
        finally {
            gcloud compute networks delete $newNetwork -q 2>$null
        }
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

    It "should work with attached disk configs" {
        $diskConfigs = New-GceAttachedDiskConfig -SourceImage $image -Boot
        Add-GceInstanceTemplate $name3 -Disk $diskConfigs
        $template = Get-GceInstanceTemplate $name3
        $template.Name | Should Be $name3
        $prop = $template.Properties
        $prop.Disks.Count | Should Be 1
        $prop.Disks.Boot | Should Be $true
        $prop.Disks.InitializeParams.SourceImage -match "windows" | Should Be $true
        $prop.MachineType | Should Be "n1-standard-1"
    }

    Get-GceInstanceTemplate | Remove-GceInstanceTemplate
}

Describe "Remove-GceInstanceTemplate" {
    
    $r = Get-Random
    $name = "test-remove-template-$r"

    It "should fail on nonexistant template" {
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

        It "should work from pipeline" {
            $name | Remove-GceInstanceTemplate
            { Get-GceInstanceTemplate $name } | Should Throw 404
        }

        It "should work with object" {
            $object = Get-GceInstanceTemplate $name
            Remove-GceInstanceTemplate $object
            { Get-GceInstanceTemplate $name } | Should Throw 404
        }

        It "should work with object from pipeline" {
            Get-GceInstanceTemplate $name | Remove-GceInstanceTemplate
            { Get-GceInstanceTemplate $name } | Should Throw 404
        }
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
