. $PSScriptRoot\..\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"
$zone = "us-central1-f"
$zone2 = "us-central1-a"

Describe "Get-GceInstance" {

    $r = Get-Random
	$noExistInstance = "gcps-instance-no-exist-$r"
	$existantInstance = "gcps-instance-exist-$r"
	$existantInstance2 = "gcps-instance-exist2-$r"
	$existantInstance3 = "gcps-instance-exist3-$r"

    $existantInstance, $existantInstance2 |
		New-GceInstanceConfig -DiskImage "projects/debian-cloud/global/images/debian-8-jessie-v20160511" |
		Add-GceInstance -Project $project -Zone $zone
	$existantInstance3 |
		New-GceInstanceConfig -DiskImage "projects/debian-cloud/global/images/debian-8-jessie-v20160511" |
		Add-GceInstance -Project $project -Zone $zone2

	It "should fail to return non-existing instances" {
        { Get-GceInstance -Project $project -Zone $zone -Name $noExistInstance } | Should Throw "404"
    }

    It "should get one" {
        $instance = Get-GceInstance -Project $project -Zone $zone -Name $existantInstance
		($instance | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Instance"
		$instance.Name | Should Be $existantInstance
		$instance.Kind | Should Be "compute#instance"
    }

	It "should use the pipeline" {
	    $instances = @($existantInstance, $existantInstance2) | Get-GceInstance -Project $project -Zone $zone
		$instances.Count | Should Be 2
	}

    It "should get only zone" {
		$zoneInstances = Get-GceInstance -Project $project -Zone $zone | Where {$_.Name -in $existantInstance, $existantInstance2, $existantInstance3}
        $zoneInstances.Length | Should Be 2
		$zoneInstances.Kind | Should Be "compute#instance"
		$zoneInstances.Zone | Should Match $zone
    }

    It "should list all instances in a project" {
        $projectInstances = Get-GceInstance -Project $project| Where {$_.Name -in $existantInstance, $existantInstance2, $existantInstance3}
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
		$instanceObj.Name = $existantInstance
		
		It "should get one" {
			$instance = Get-GceInstance -Project $projectObj -Zone $zoneObj -Name $instanceObj
			$instance.Name | Should Be $existantInstance
			$instance.Kind | Should Be "compute#instance"
		}
	}
	
	$existantInstance, $existantInstance2 | Remove-GceInstance -Project $project -Zone $zone
	Remove-GceInstance -Project $project -Zone $zone2 -Name $existantInstance3

}

Describe "New-GceInstanceConfig" {

    $r = Get-Random
	$instance = "gcps-instance-1-$r"
	$instance2 = "gcps-instance-2-$r"

    It "should work" {
		$instanceConfig = New-GceInstanceConfig -Name $instance `
			-MachineType "projects/$project/zones/$zone/machineTypes/n1-standard-2" `
			-Disk @{"boot"=$true; "initializeParams" = @{"sourceImage" = "projects/debian-cloud/global/images/debian-8-jessie-v20160511"}} `
			-NetworkInterface @{"network"="global/networks/fake"} `
			-Tag "Testing"
		$instanceConfig.MachineType | Should Be "projects/$project/zones/$zone/machineTypes/n1-standard-2"
		$instanceConfig.Disks.Boot | Should Be $true
		$instanceConfig.Tags.Items | Should Be "Testing"
		$instanceConfig.NetworkInterfaces.Network | Should Be "global/networks/fake"
    }

    It "should handle defaults" {
		$instanceConfig = New-GceInstanceConfig -Name $instance `
			-Disk @{"boot"=$true; "initializeParams" = @{"sourceImage" = "projects/debian-cloud/global/images/debian-8-jessie-v20160511"}}
		$instanceConfig.MachineType | Should Be "n1-standard-4"
		$instanceConfig.NetworkInterfaces.Network | Should Be "global/networks/default"
		$instanceConfig.NetworkInterfaces.AccessConfigs.Type | Should Be "ONE_TO_ONE_NAT"
    }

    It "should build disk from image" {
		$instanceConfig = New-GceInstanceConfig -Name $instance -DiskImage "someImage"
		$instanceConfig.Disks.Boot | Should Be $true
		$instanceConfig.Disks.AutoDelete | Should Be $true
		$instanceConfig.Disks.InitializeParams.SourceImage | Should Be "someImage"
    }

    It "should attach disk" {
		$instanceConfig = New-GceInstanceConfig -Name $instance -DiskSource "someDisk"
		$instanceConfig.Disks.Boot | Should Be $true
		$instanceConfig.Disks.AutoDelete | Should Be $false
		$instanceConfig.Disks.Source | Should Be "someDisk"
		$instanceConfig.Disks.InitializeParams | Should BeNullOrEmpty
    }

	It "should use pipeline" {
		$instanceConfigs = $instance, $instance2 | New-GceInstanceConfig -DiskImage "someImage"
		$instanceConfigs.Count | Should Be 2
		($instanceConfigs.Name | Where {$_ -eq $instance}).Count | Should Be 1
		$instanceConfigs.MachineType | Should Be "n1-standard-4"
	}
}

Describe "Add-GceInstance" {

    $r = Get-Random
	$instance = "gcps-instance-create-$r"
	$instance2 = "gcps-instance-create2-$r"
	$instance3 = "gcps-instance-create3-$r"
	$instanceConfig = New-GceInstanceConfig -Name $instance -DiskImage "projects/debian-cloud/global/images/debian-8-jessie-v20160511"
	$instanceConfig2 = New-GceInstanceConfig -Name $instance2 -DiskImage "projects/debian-cloud/global/images/debian-8-jessie-v20160511"
	$instanceConfig3 = New-GceInstanceConfig -Name $instance3 -DiskImage "projects/debian-cloud/global/images/debian-8-jessie-v20160511"

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

    It "should throw 403" {
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
				New-GceInstanceConfig -DiskImage "projects/debian-cloud/global/images/debian-8-jessie-v20160511" |
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

	It "Should Throw 404" {
		{ Remove-GceInstance -Project $project -Zone $zone -Name $instance } | Should Throw 404
	}
	
	It "Should Throw 403" {
		{ Remove-GceInstance -Project "asdf" -Zone $zone -Name $instance } | Should Throw 403
	}
}