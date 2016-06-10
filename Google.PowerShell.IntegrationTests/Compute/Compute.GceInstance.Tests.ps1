. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets


$project = "gcloud-powershell-testing"
$zone = "us-central1-f"
$zone2 = "us-central1-a"

# parse the configurations list, creating objects with properties named by the first line of output.
$configList = gcloud config configurations list
$oldActiveConfig = $configList -split [System.Environment]::NewLine |
     % {$_ -split "\s+" -join ","} | ConvertFrom-Csv | Where {$_.IS_ACTIVE -match "True"}

$configRandom = Get-Random
$configName = "testing$configRandom"
gcloud config configurations create $configName
gcloud config configurations activate $configName
gcloud config set core/account $oldActiveConfig.ACCOUNT
gcloud config set core/project $project
gcloud config set compute/zone $zone


$image = "projects/debian-cloud/global/images/debian-8-jessie-v20160511"

Get-GceInstance -Zone $zone | Remove-GceInstance
Get-GceInstance -Zone $zone2 | Remove-GceInstance -Zone $zone2

Describe "Get-GceInstance" {

    $r = Get-Random
    $instance = "gcps-instance-exist-$r"
    $instance2 = "gcps-instance-exist2-$r"
    $instance3 = "gcps-instance-exist3-$r"

    @($instance, $instance2) |
        New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" |
        Add-GceInstance

    $instance3 |
        New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" |
        Add-GceInstance -Zone $zone2


    It "should fail to return non-existing instances" {
        {
            Get-GceInstance -Name "gcps-instance-no-exist-$r"
        } | Should Throw "404"
    }
    
    It "should get one" {
        $result = Get-GceInstance -Name $instance
        ($result | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Instance"
        $result.Name | Should Be $instance
        $result.Kind | Should Be "compute#instance"
    }

    It "should use the pipeline" {
        $instances = @($instance, $instance2) | Get-GceInstance
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

    # Test that the PropertyByTypeTransformationAttribute works the way we think
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

    It "should return serial port output" {
        $output = Get-GceInstance -Name $instance -SerialPortOutput
        $output | Should Match "$instance run-startup-scripts"
    }
    
    $instance, $instance2 | Remove-GceInstance
    Remove-GceInstance -Zone $zone2 -Name $instance3
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
            -Tag "alpha", "beta"
        $instanceConfig.MachineType | Should Be "f1-micro"
        $instanceConfig.Disks.Boot | Should Be $true
        $instanceConfig.Tags.Items | Should Be @("alpha", "beta")
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
    $instanceConfig = New-GceInstanceConfig -Name $instance -DiskImage $image -MachineType "f1-micro"
    $instanceConfig2 = New-GceInstanceConfig -Name $instance2 -DiskImage $image -MachineType "f1-micro"
    $instanceConfig3 = New-GceInstanceConfig -Name $instance3 -DiskImage $image -MachineType "f1-micro"

    It "should work" {
        Add-GceInstance -Instance $instanceConfig
        $runningInstance = Get-GceInstance -Name $instance
        $runningInstance.Name | Should Be $instance
    }

    It "should use pipeline" {
        $instanceConfig2, $instanceConfig3 | Add-GceInstance
        $runningInstances = $instance2, $instance3 | Get-GceInstance
        $runningInstances.Count | Should Be 2
    }

    It "should throw on wrong project" {
        { Add-GceInstance -Project "asdf" -Instance $instanceConfig } | Should Throw 403
    }

    $instance, $instance2, $instance3 | Remove-GceInstance
}

Describe "Remove-GceInstance" {

    $r = Get-Random
    $instance = "gcps-instance-remove-$r"

    Context "Real Remove" {
        BeforeEach {
             $instance |
                New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" |
                Add-GceInstance
        }

        It "should Work" {
            Remove-GceInstance -Name $instance
            { Get-GceInstance -Name $instance } | Should Throw 404
        }
        
        It "should Work with pipeline" {
            $instance | Remove-GceInstance 
            { Get-GceInstance -Name $instance } | Should Throw 404
        }
    }

    It "should fail removing non existing instances" {
        { Remove-GceInstance -Name $instance } | Should Throw 404
    }
    
    It "should fail removing instance in wrong project" {
        { Remove-GceInstance -Project "asdf" -Name $instance } | Should Throw 403
    }
}

Describe "Start-GceInstance" {
    $r = Get-Random
    $instance = "gcps-instance-start-$r"

    It "should fail starting wrong project" {
        { Start-GceInstance -Project "asdf" -Name $instance } | Should Throw 403
    }

    It "should fail starting non existing instance" {
        { Start-GceInstance -Name $instance} | Should Throw 404
    }

    $instance |
        New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" |
        Add-GceInstance
    
    Stop-GceInstance -Name $instance

    It "should work" {
        Start-GceInstance -Name $instance
        (Get-GceInstance $instance).Status | Should Be "RUNNING"
    }

    Remove-GceInstance $instance
}

Describe "Stop-GceInstance" {
    $r = Get-Random
    $instance = "gcps-instance-stop-$r"

    It "should fail stoping wrong project" {
        { Stop-GceInstance -Project "asdf" -Name $instance } | Should Throw 403
    }

    It "should fail stoping non existing instance" {
        { Stop-GceInstance -Name $instance} | Should Throw 404
    }

    $instance |
        New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" |
        Add-GceInstance
    
    It "should work " {
        (Get-GceInstance $instance).Status | Should Be "RUNNING"
        Stop-GceInstance -Name $instance
        (Get-GceInstance $instance).Status | Should Be "TERMINATED"
    }

    Remove-GceInstance $instance
}

Describe "Restart-GceInstance" {
    $r = Get-Random
    $instance = "gcps-instance-restart-$r"

    It "should fail restarting wrong project" {
        { Restart-GceInstance -Project "asdf" -Name $instance } | Should Throw 403
    }

    It "should fail restarting non existing instance" {
        { Restart-GceInstance -Name $instance} | Should Throw 404
    }

    $instance |
        New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" |
        Add-GceInstance

    It "should show restart in log" {
        $before = (Get-Date).ToUniversalTime()
        Restart-GceInstance -Name $instance
        Start-Sleep 5
        # Read and parse serial port output to see when the last startup happened.
        $portString = (Get-GceInstance $instance -SerialPortOutput)
        $portLines = $portString -split [System.Environment]::NewLine
        $restartLine = $portLines -match "(\w+)\s+(\d+)\s+(\d+):(\d+):(\d+)\s$instance kernel:" -match "0.000000]" |
            Select-Object -Last 1
        $restartLine -match "(\w+)\s+(\d+)\s(\d+):(\d+):(\d+)"
        $month, $day, $hour, $minute, $second = $matches.1, $matches.2, $matches.3, $matches.4, $matches.5
        $restartTime = [DateTime]::ParseExact("$month $day $hour $minute $second", "MMM d HH mm ss", $null)
        $restartTime -gt $before | Should Be $true
    }

    Remove-GceInstance $instance
}

Describe "Set-GceInstance" {
    $r = Get-Random
    $instance = "gcps-instance-set-$r"
    
    It "should fail changing wrong project" {
        { Set-GceInstance -Project "asdf" -Instance $instance -AddTag "alpha" } | Should Throw 403
    }

    It "should fail changinon existing instance" {
        { Set-GceInstance -Instance $instance -AddTag "alpha" } |
            Should Throw 404
    }

    $instance |
        New-GceInstanceConfig -DiskImage $image -MachineType "f1-micro" -Metadata @{"k" = "v"} -Tag "beta" |
        Add-GceInstance

    It "should change tags" {
        Set-GceInstance -Instance $instance -RemoveTag "beta" -AddTag "alpha"
        (Get-GceInstance $instance).Tags.Items | Should Be "alpha"
    }

    It "should change metadata" {
        # Test adding and removing
        Set-GceInstance $instance -RemoveMetadata "k" -AddMetadata @{"newKey" = "newValue"}

        $instanceObj = Get-GceInstance $instance
        $instanceObj.Metadata.Items.Key | Should Be "newKey"
        $instanceObj.Metadata.Items.Value | Should Be "newValue"

        # Test removing only
        Set-GceInstance $instance -RemoveMetadata "newKey"

        $instanceObj = Get-GceInstance $instance
        $instanceObj.Metadata.Items.Count | Should Be 0

        # Test adding only
        Set-GceInstance $instance -AddMetadata @{"newKey2" = "newValue2"}

        $instanceObj = Get-GceInstance $instance
        $instanceObj.Metadata.Items.Key | Should Be "newKey2"
        $instanceObj.Metadata.Items.Value | Should Be "newValue2"

    }

    It "should change AccessConfigs" {

        # Find the existing values
        $instanceObj = Get-GceInstance $instance
        $interfaceName = $instanceObj.NetworkInterfaces.Name
        $configName = $instanceObj.NetworkInterfaces.AccessConfigs.Name
        
        # Build a new AccessConfig
        [Google.Apis.Compute.v1.Data.AccessConfig] $newConfig = @{}
        $newConfig.Kind = "ONE_TO_ONE_NAT"
        $newConfig.Name = "NewConfig$r"

        # Test adding and deleting
        Set-GceInstance $instance -NetworkInterface $interfaceName `
            -DeleteAccessConfig $configName -NewAccessConfig $newConfig

        $instanceObj = Get-GceInstance $instance
        $instanceObj.NetworkInterfaces.AccessConfigs.Name | Should Be "NewConfig$r"
    }

    Context "With Disk" {
        $newDiskName = "attach-disk-test-$r"
        $newDisk = New-GceDisk -Project $project -Zone $zone -DiskName $newDiskName -Size 1

        It "should change Disk" {
            Set-GceInstance $instance -AddDisk $newDiskName
            $instanceObj = Get-GceInstance $instance
            $instanceObj.Disks.Count | Should Be 2
            ($instanceObj.Disks | Where {$_.DeviceName -eq $newDiskName}).Count | Should Be 1

            Set-GceInstance $instance -DetachDisk $newDiskName
            (Get-GceInstance $instance).Disks.Count | Should Be 1
        }

        Remove-GceDisk -DiskName $newDiskName -Force
    }

    Remove-GceInstance $instance
}

gcloud config configurations activate $oldActiveConfig.NAME
gcloud config configurations delete $configName -q
