. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

$zone2 = "us-central1-a"
$image = Get-GceImage "debian-cloud" -Family "debian-8"
Get-GceInstance -Project $project | Remove-GceInstance

Describe "Get-GceInstance" {

    $r = Get-Random
    $instance = "gcps-instance-exist-$r"
    $instance2 = "gcps-instance-exist2-$r"
    $instance3 = "gcps-instance-exist3-$r"
    
    $instance, $instance2 | Add-GceInstance -BootDiskImage $image -MachineType "f1-micro"
    Add-GceInstance $instance3 -BootDiskImage $image -MachineType "f1-micro" -Zone $zone2


    It "should fail to return non-existing instances" {
        { Get-GceInstance "gcps-instance-no-exist-$r" } | Should Throw "404"
    }
    
    It "should get one" {
        $result = Get-GceInstance $instance
        ($result | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Instance"
        $result.Name | Should Be $instance
        $result.Kind | Should Be "compute#instance"
    }

    It "should use the pipeline" {
        $instances = $instance, $instance2 | Get-GceInstance
        $instances.Count | Should Be 2
    }

    It "should get only zone" {
        $zoneInstances = Get-GceInstance -Zone $zone
        $zoneInstances.Length | Should Be 2
        $zoneInstances.Kind | Should Be "compute#instance"
        $zoneInstances.Zone | Should Match $zone
    }

    It "should list all instances in a project" {
        $projectInstances = Get-GceInstance
        $projectInstances.Count | Should Be 3
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GceInstance -Project "asdf" } | Should Throw "403"
    }

    It "should get by object" {
        $instanceObj = New-Object Google.Apis.Compute.v1.Data.Instance
        $instanceObj.Name = $instance
        $instanceObj.Zone = "projects/$project/zones/$zone"
        $instanceObj.SelfLink = "projects/$project/zones/$zone/instances/$instance"
        $result = Get-GceInstance $instanceObj
        ($result | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Instance"
        $result.Name | Should Be $instance
        $result.Kind | Should Be "compute#instance"
    }

    # Test that the PropertyByTypeTransformationAttribute works the way we think
    Context "Object Transformer" {
        $projectObj = New-Object Google.Apis.Compute.v1.Data.Project
        $projectObj.Name = $project
        $zoneObj = New-Object Google.Apis.Compute.v1.Data.Zone
        $zoneObj.Name = $zone
        
        It "should get one" {
            $result = Get-GceInstance -Project $projectObj -Zone $zoneObj -Name $instance
            $result.Name | Should Be $instance
            $result.Kind | Should Be "compute#instance"
        }
    }

    It "should return serial port output" {
        $output = Get-GceInstance -Project $project -Zone $zone -Name $instance -SerialPortOutput
        $output | Should Match "$instance\s*kernel"
    }
    
    $templateName = "test-template-get-instance-$r"
    $groupname = "test-group-get-instance-$r"
    Add-GceInstanceTemplate -Name $templateName -MachineType "f1-micro" -BootDiskImage $image
    $template = Get-GceInstanceTemplate $templateName
    Add-GceManagedInstanceGroup $groupName $template 2
    $group = Get-GceManagedInstanceGroup $groupName

    It "should get from instance group" {
        Wait-GceManagedInstanceGroup $groupName
        $instances = Get-GceInstance $group
        $instances.Count | Should Be 2
        ($instances | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Instance"
    }

    $group | Remove-GceManagedInstanceGroup
    $template | Remove-GceInstanceTemplate
    Get-GceInstance -Project $project | Remove-GceInstance
}

Describe "New-GceInstanceConfig" {

    $r = Get-Random
    $instance = "gcps-instance-1-$r"
    $instance2 = "gcps-instance-2-$r"
    $defaultNetwork = Get-GceNetwork "default"
    $attachedDisk = New-GceAttachedDiskConfig $image -Boot

    It "should work" {
        $instanceConfig = New-GceInstanceConfig -Name $instance `
            -MachineType "f1-micro" `
            -Disk $attachedDisk `
            -Network $default `
            -Tag "alpha", "beta"
        $instanceConfig.MachineType | Should Be "f1-micro"
        $instanceConfig.Disks.Boot | Should Be $true
        $instanceConfig.Tags.Items | Should Be @("alpha", "beta")
        $instanceConfig.NetworkInterfaces.Network | Should Match "global/networks/default"
    }

    It "should handle defaults" {
        $instanceConfig = New-GceInstanceConfig -Name $instance -Disk $attachedDisk
        $instanceConfig.NetworkInterfaces.Network | Should Be "global/networks/default"
        $instanceConfig.NetworkInterfaces.AccessConfigs.Type | Should Be "ONE_TO_ONE_NAT"
        $instanceConfig.MachineType | Should Be "n1-standard-1"
    }

    It "should build disk from image" {
        $instanceConfig = New-GceInstanceConfig -Name $instance -MachineType "f1-micro" `
             -DiskImage $image
        $instanceConfig.Disks.Boot | Should Be $true
        $instanceConfig.Disks.AutoDelete | Should Be $true
        $instanceConfig.Disks.InitializeParams.SourceImage | Should Be $image.SelfLink
    }

    $persistantDisk = New-GceDisk "test-new-instanceconfig-$r" $image

    It "should attach disk" {
        $instanceConfig = New-GceInstanceConfig -Name $instance  -MachineType "f1-micro" `
            -BootDisk $persistantDisk
        $instanceConfig.Disks.Boot | Should Be $true
        $instanceConfig.Disks.AutoDelete | Should Be $false
        $instanceConfig.Disks.Source | Should Be $persistantDisk.SelfLink
        $instanceConfig.Disks.InitializeParams | Should BeNullOrEmpty
    }

    Remove-GceDisk $persistantDisk

    It "should use pipeline" {
        $instanceConfigs = $instance, $instance2 |
            New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro"
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
    $instance4 = "gcps-instance-create4-$r"
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

    It "should build with parameters and defaults" {
        Add-GceInstance -Name $instance4 -DiskImage $image
        $runningInstance = Get-GceInstance -Project $project -Zone $zone -Name $instance4
        $runningInstance.Name | Should Be $instance4
        $runningInstance.SelfLink | Should Match $project
        $runningInstance.Zone | Should Match $zone
        $runningInstance.MachineType | Should Match "n1-standard-1"
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
                Add-GceInstance -Project $project -Zone $zone2
        }

        It "should Work" {
            Remove-GceInstance $instance -Zone $zone2
            { Get-GceInstance -Project $project -Zone $zone2 -Name $instance } | Should Throw 404
        }
        
        It "should Work with pipeline" {
            $instance | Remove-GceInstance -Project $project -Zone $zone2
            { Get-GceInstance -Project $project -Zone $zone2 -Name $instance } | Should Throw 404
        }
        
        It "should Work with object pipeline" {
            Get-GceInstance $instance -Project $project -Zone $zone2 | Remove-GceInstance
            { Get-GceInstance -Project $project -Zone $zone2 -Name $instance } | Should Throw 404
        }
    }

    It "should fail removing non existing instances" {
        { Remove-GceInstance -Project $project -Zone $zone -Name $instance } | Should Throw 404
    }
    
    It "should fail removing instance in wrong project" {
        { Remove-GceInstance -Project "asdf" -Zone $zone -Name $instance } | Should Throw 403
    }
}

Describe "Start-GceInstance" {
    $r = Get-Random
    $instance = "gcps-instance-start-$r"

    It "should fail starting wrong project" {
        { Start-GceInstance $instance -Project "asdf"} | Should Throw 403
    }

    It "should fail starting non existing instance" {
        { Start-GceInstance $instance} | Should Throw 404
    }

    $instance |
        New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" |
        Add-GceInstance -Project $project -Zone $zone2
    
    Stop-GceInstance -Project $project -Zone $zone2 -Name $instance

    It "should work" {
        Start-GceInstance -Name $instance -Zone $zone2
        (Get-GceInstance $instance -Zone $zone2).Status | Should Be "RUNNING"
    }

    Stop-GceInstance -Project $project -Zone $zone2 -Name $instance

    It "should work with object pipeline" {
        Get-GceInstance $instance -Zone $zone2 | Start-GceInstance 
        (Get-GceInstance $instance -Zone $zone2).Status | Should Be "RUNNING"
    }

    Remove-GceInstance -Project $project -Zone $zone2 $instance
}

Describe "Stop-GceInstance" {
    $r = Get-Random
    $instance = "gcps-instance-stop-$r"

    It "should fail stoping wrong project" {
        { Stop-GceInstance $instance -Project "asdf"} | Should Throw 403
    }

    It "should fail stoping non existing instance" {
        { Stop-GceInstance $instance} | Should Throw 404
    }

    $instance |
        New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" |
        Add-GceInstance -Project $project -Zone $zone2
    
    It "should work " {
        (Get-GceInstance $instance -Zone $zone2).Status | Should Be "RUNNING"
        Stop-GceInstance $instance -Zone $zone2
        (Get-GceInstance $instance -Zone $zone2).Status | Should Be "TERMINATED"
    }
    
    Start-GceInstance $instance -Zone $zone2

    It "should work with object pipeline" {
        Get-GceInstance $instance -Zone $zone2 | Stop-GceInstance
        (Get-GceInstance $instance -Zone $zone2).Status | Should Be "TERMINATED"
    }

    Remove-GceInstance -Project $project -Zone $zone2 $instance
}

Describe "Restart-GceInstance" {
    $r = Get-Random
    $instance = "gcps-instance-restart-$r"

    It "should fail restarting wrong project" {
        { Restart-GceInstance -Project "asdf" -Zone $zone -Name $instance } | Should Throw 403
    }

    It "should fail restarting non existing instance" {
        { Restart-GceInstance -Project $project -Zone $zone -Name $instance} | Should Throw 404
    }

    $instance |
        New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" |
        Add-GceInstance -Project $project -Zone $zone2

    It "should show restart in log" {
        $before = (Get-Date).ToUniversalTime()
        Restart-GceInstance $instance -Zone $zone2
        Start-Sleep 5
        # Read and parse serial port output to see when the last startup happened.
        $portString = (Get-GceInstance $instance -Zone $zone2 -SerialPortOutput)
        $portLines = $portString -split [System.Environment]::NewLine
        $restartLine = $portLines -match "(\w+)\s+(\d+)\s+(\d+):(\d+):(\d+)\s$instance kernel:" -match "0.000000]" |
            Select-Object -Last 1
        $restartLine -match "(\w+)\s+(\d+)\s(\d+):(\d+):(\d+)"
        $restartTime = [DateTime]::ParseExact($Matches[1..5] -join " ", "MMM d HH mm ss", $null)
        $restartTime -gt $before | Should Be $true
    }
    
    It "should restart using object pipeline" {
        $before = (Get-Date).ToUniversalTime()
        Get-GceInstance $instance -Zone $zone2 | Restart-GceInstance
        Start-Sleep 5
        # Read and parse serial port output to see when the last startup happened.
        $portString = (Get-GceInstance $instance -Zone $zone2 -SerialPortOutput)
        $portLines = $portString -split [System.Environment]::NewLine
        $restartLine = $portLines -match "(\w+)\s+(\d+)\s+(\d+):(\d+):(\d+)\s$instance kernel:" -match "0.000000]" |
            Select-Object -Last 1
        $restartLine -match "(\w+)\s+(\d+)\s(\d+):(\d+):(\d+)"
        $restartTime = [DateTime]::ParseExact($Matches[1..5] -join " ", "MMM d HH mm ss", $null)
        $restartTime -gt $before | Should Be $true
    }

    Remove-GceInstance -Project $project -Zone $zone2 $instance
}

Describe "Set-GceInstance" {
    $r = Get-Random
    $instance = "gcps-instance-set-$r"
    
    It "should fail changing wrong project" {
        { Set-GceInstance $instance -Project "asdf" -AddTag "alpha" } | Should Throw 403
    }

    It "should fail changing non-existing instance" {
        { Set-GceInstance $instance -AddTag "alpha" } | Should Throw 404
    }

    $instance |
        New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" -Metadata @{"k" = "v"} -Tag "beta" |
        Add-GceInstance -Project $project -Zone $zone2

    It "should change tags" {
        Set-GceInstance $instance -Zone $zone2 -RemoveTag "beta" -AddTag "alpha"
        (Get-GceInstance -Project $project -Zone $zone2 $instance).Tags.Items | Should Be "alpha"
    }

    It "should change metadata with pipeline" {
        # Test adding and removing
        Get-GceInstance $instance -Zone $zone2 |
            Set-GceInstance -RemoveMetadata "k" -AddMetadata @{"newKey" = "newValue"}

        $instanceObj = Get-GceInstance -Project $project -Zone $zone2 $instance
        $instanceObj.Metadata.Items.Key | Should Be "newKey"
        $instanceObj.Metadata.Items.Value | Should Be "newValue"

        # Test removing only
        Set-GceInstance -Project $project -Zone $zone2 $instance -RemoveMetadata "newKey"

        $instanceObj = Get-GceInstance -Project $project -Zone $zone2 $instance
        $instanceObj.Metadata.Items.Count | Should Be 0

        # Test adding only
        Set-GceInstance -Project $project -Zone $zone2 $instance -AddMetadata @{"newKey2" = "newValue2"}

        $instanceObj = Get-GceInstance -Project $project -Zone $zone2 $instance
        $instanceObj.Metadata.Items.Key | Should Be "newKey2"
        $instanceObj.Metadata.Items.Value | Should Be "newValue2"

    }

    It "should change AccessConfigs" {

        # Find the existing values
        $instanceObj = Get-GceInstance -Project $project -Zone $zone2 $instance
        $interfaceName = $instanceObj.NetworkInterfaces.Name
        $configName = $instanceObj.NetworkInterfaces.AccessConfigs.Name
        
        # Build a new AccessConfig
        [Google.Apis.Compute.v1.Data.AccessConfig] $newConfig = @{}
        $newConfig.Kind = "ONE_TO_ONE_NAT"
        $newConfig.Name = "NewConfig$r"

        # Test adding and deleting
        Set-GceInstance -Project $project -Zone $zone2 $instance -NetworkInterface $interfaceName `
            -RemoveAccessConfig $configName -AddAccessConfig $newConfig

        $instanceObj = Get-GceInstance -Project $project -Zone $zone2 $instance
        $instanceObj.NetworkInterfaces.AccessConfigs.Name | Should Be "NewConfig$r"
    }

    Context "With Disk" {
        BeforeAll {
            $newDiskName = "attach-disk-test-$r"
            $newDiskName2 = "attach-disk-test2-$r"
            $newDiskName3 = "attach-disk-test3-$r"
            $newDisk = New-GceDisk -Project $project -Zone $zone2 -DiskName $newDiskName -Size 1
            $newDisk2 = New-GceDisk -Project $project -Zone $zone2 -DiskName $newDiskName2 -Size 1
            $newDisk3 = New-GceDisk -Project $project -Zone $zone2 -DiskName $newDiskName3 -Size 1
            $attachedDisk3 = New-GceAttachedDiskConfig $newDisk3 -DeviceName $newDiskName3
        }

        It "should change Disk" {
            Set-GceInstance  $instance -Project $project -Zone $zone2 -AddDisk $newDiskName,
                $newDisk2, $attachedDisk3
            $instanceObj = Get-GceInstance -Project $project -Zone $zone2 $instance
            $instanceObj.Disks.Count | Should Be 4
            ($instanceObj.Disks | Where {$_.DeviceName -eq $newDiskName}).Count | Should Be 1

            Set-GceInstance -Project $project -Zone $zone2 $instance -RemoveDisk $newDiskName,
                $newDiskName2, $newDiskName3
            (Get-GceInstance -Project $project -Zone $zone2 $instance).Disks.Count | Should Be 1
        }

        AfterAll {
            $newDisk, $newDisk2, $newDisk3 | Remove-GceDisk
        }
    }

    Remove-GceInstance -Project $project -Zone $zone2 $instance
}

Reset-GCloudConfig $oldActiveConfig $configName
