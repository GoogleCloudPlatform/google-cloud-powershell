. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"
$zone = "us-central1-f"
$zone2 = "us-central1-a"

$image = "projects/debian-cloud/global/images/debian-8-jessie-v20160511"

Get-GceInstance -Project $project -Zone $zone | Remove-GceInstance -Project $project -Zone $zone
Get-GceInstance -Project $project -Zone $zone2 | Remove-GceInstance -Project $project -Zone $zone2

Describe "Get-GceInstance" {

    $r = Get-Random
    $instance = "gcps-instance-exist-$r"
    $instance2 = "gcps-instance-exist2-$r"
    $instance3 = "gcps-instance-exist3-$r"

    @($instance, $instance2) |
        New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" |
        Add-GceInstance -Project $project -Zone $zone

    $instance3 |
        New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" |
        Add-GceInstance -Project $project -Zone $zone2

    <#TODO: Find a test that consistantly succeds for groups
    Context "Instance Group" {

        $instanceGroupName = "instance-group-$r"
        #TODO: Replace with powershell cmdlet
        gcloud compute instance-groups managed create $instanceGroupName `
            --size 1 --template instance-template-1

        It "should get one" {
            $result = Get-GceInstance -Project $project -Zone $zone -InstanceGroup $instanceGroupName
            $result.Name | Should Match $instanceGroupName
            $result.Name | Should Not Be $instanceGroupName
            $result.Kind | Should Be "compute#instance"
            $result.Count | Should Be 1
        }

        It "should fail for wrong instance group" {
            { Get-GceInstance -Project $project -Zone $zone -InstanceGroup "fake" } | Should Throw 404
        }

        It "should fail for wrong project" {
            { Get-GceInstance -Project "asdf" -Zone $zone -InstanceGroup "fake" } | Should Throw 403
        }

        gcloud compute instance-groups managed delete $instanceGroupname -q
    }#>


    It "should fail to return non-existing instances" {
        {
            Get-GceInstance -Project $project -Zone $zone -Name "gcps-instance-no-exist-$r"
        } | Should Throw "404"
    }
    
    It "should get one" {
        $result = Get-GceInstance -Project $project -Zone $zone -Name $instance
        ($result | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Instance"
        $result.Name | Should Be $instance
        $result.Kind | Should Be "compute#instance"
    }

    It "should use the pipeline" {
        $instances = @($instance, $instance2) | Get-GceInstance -Project $project -Zone $zone
        $instances.Count | Should Be 2
    }

    It "should get only zone" {
        $zoneInstances = Get-GceInstance -Project $project -Zone $zone
        $zoneInstances.Length | Should Be 2
        $zoneInstances.Kind | Should Be "compute#instance"
        $zoneInstances.Zone | Should Match $zone
    }

    It "should list all instances in a project" {
        $projectInstances = Get-GceInstance -Project $project
        $projectInstances.Count | Should Be 3
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GceInstance -Project "asdf" } | Should Throw "403"
    }

    Context "Object Transformer" {
        $projectObj = New-Object Google.Apis.Compute.v1.Data.Project
        $projectObj.Name = $project
        $zoneObj = New-Object Google.Apis.Compute.v1.Data.Zone
        $zoneObj.Name = $zone
        $instanceObj = New-Object Google.Apis.Compute.v1.Data.Instance
        $instanceObj.Name = $instance
        
        It "should get one" {
            $result = Get-GceInstance -Project $projectObj -Zone $zoneObj -Name $instanceObj
            $result.Name | Should Be $instance
            $result.Kind | Should Be "compute#instance"
        }
    }
    
    $instance, $instance2 | Remove-GceInstance -Project $project -Zone $zone
    Remove-GceInstance -Project $project -Zone $zone2 -Name $instance3
}

Describe "New-GceInstanceConfig" {

    $r = Get-Random
    $instance = "gcps-instance-1-$r"
    $instance2 = "gcps-instance-2-$r"

    It "should work" {
        $instanceConfig = New-GceInstanceConfig -Name $instance `
            -MachineType "f1-micro" `
            -Disk @{"boot"=$true; "initializeParams" = @{"sourceImage" = $image}} `
            -NetworkInterface @{"network"="global/networks/fake"} `
            -Tag "Testing", "AnotherTag"
        $instanceConfig.MachineType | Should Be "f1-micro"
        $instanceConfig.Disks.Boot | Should Be $true
        $instanceConfig.Tags.Items | Should Be @("Testing", "AnotherTag")
        $instanceConfig.NetworkInterfaces.Network | Should Be "global/networks/fake"
    }

    It "should handle defaults" {
        $instanceConfig = New-GceInstanceConfig -Name $instance -MachineType "f1-micro" `
            -Disk @{"boot"=$true; "initializeParams" = @{"sourceImage" = $image}}
        $instanceConfig.NetworkInterfaces.Network | Should Be "global/networks/default"
        $instanceConfig.NetworkInterfaces.AccessConfigs.Type | Should Be "ONE_TO_ONE_NAT"
    }

    It "should build disk from image" {
        $instanceConfig = New-GceInstanceConfig -Name $instance -MachineType "f1-micro" `
             -DiskImage $image
        $instanceConfig.Disks.Boot | Should Be $true
        $instanceConfig.Disks.AutoDelete | Should Be $true
        $instanceConfig.Disks.InitializeParams.SourceImage | Should Be $image
    }

    It "should attach disk" {
        $instanceConfig = New-GceInstanceConfig -Name $instance  -MachineType "f1-micro" `
            -DiskSource "someDisk"
        $instanceConfig.Disks.Boot | Should Be $true
        $instanceConfig.Disks.AutoDelete | Should Be $false
        $instanceConfig.Disks.Source | Should Be "someDisk"
        $instanceConfig.Disks.InitializeParams | Should BeNullOrEmpty
    }

    It "should use pipeline" {
        $instanceConfigs = $instance, $instance2 | New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro"
        $instanceConfigs.Count | Should Be 2
        ($instanceConfigs.Name | Where {$_ -eq $instance}).Count | Should Be 1
        ($instanceConfigs.Name | Where {$_ -eq $instance2}).Count | Should Be 1
    }
}

Describe "Add-GceInstance" {

    $r = Get-Random
    $instance = "gcps-instance-create-$r"
    $instance2 = "gcps-instance-create2-$r"
    $instance3 = "gcps-instance-create3-$r"
    $instanceConfig = New-GceInstanceConfig -Name $instance -DiskImage $image -MachineType "f1-micro"
    $instanceConfig2 = New-GceInstanceConfig -Name $instance2 -DiskImage $image -MachineType "f1-micro"
    $instanceConfig3 = New-GceInstanceConfig -Name $instance3 -DiskImage $image -MachineType "f1-micro"

    It "should work" {
        Add-GceInstance -Project $project -Zone $zone -Instance $instanceConfig
        $runningInstance = Get-GceInstance -Project $project -Zone $zone -Name $instance
        $runningInstance.Name | Should Be $instance
    }

    It "should use pipeline" {
        $instanceConfig2, $instanceConfig3 | Add-GceInstance -Project $project -Zone $zone
        $runningInstances = $instance2, $instance3 | Get-GceInstance -Project $project -Zone $zone
        $runningInstances.Count | Should Be 2
    }

    It "should throw on wrong project" {
        { Add-GceInstance -Project "asdf" -Zone $zone -Instance $instanceConfig } | Should Throw 403
    }

    $instance, $instance2, $instance3 | Remove-GceInstance -Project $project -Zone $zone
}

Describe "Remove-GceInstance" {

    $r = Get-Random
    $instance = "gcps-instance-remove-$r"

    Context "Real Remove" {
        BeforeEach {
             $instance |
                New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" |
                Add-GceInstance -Project $project -Zone $zone
        }

        It "Should Work" {
            Remove-GceInstance -Project $project -Zone $zone -Name $instance
            { Get-GceInstance -Project $project -Zone $zone -Name $instance } | Should Throw 404
        }
        
        It "Should Work with pipeline" {
            $instance | Remove-GceInstance -Project $project -Zone $zone 
            { Get-GceInstance -Project $project -Zone $zone -Name $instance } | Should Throw 404
        }
    }

    It "Should fail removing non existing instances" {
        { Remove-GceInstance -Project $project -Zone $zone -Name $instance } | Should Throw 404
    }
    
    It "Should fail removing instance in wrong project" {
        { Remove-GceInstance -Project "asdf" -Zone $zone -Name $instance } | Should Throw 403
    }
}
