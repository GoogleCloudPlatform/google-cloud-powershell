. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$image = "projects/windows-cloud/global/images/family/windows-2012-r2"
$machineType = "f1-micro"
$r = Get-Random
$templateName = "test-managed-instance-groups-$r"

Get-GceManagedInstanceGroup | Remove-GceManagedInstanceGroup
Get-GceInstanceTemplate | Remove-GceInstanceTemplate
Add-GceInstanceTemplate -Name $templateName -MachineType $machineType -BootDiskImage $image
$template = Get-GceInstanceTemplate

(gcloud compute target-pools create test-pool 2>&1) -match "Created \[(.*)\]"
$poolUrl = $Matches[1]

Describe "Get-GceManagedInstanceGroup" {
    $groupName1 = "test-get-managed-instance-group-$r"
    $groupName2 = "test-get-managed-instance-group-2-$r"
    $groupName3 = "test-get-managed-instance-group-3-$r"
    Add-GceManagedInstanceGroup $groupName1 $template 1
    Add-GceManagedInstanceGroup $groupName2 $template 0
    Add-GceManagedInstanceGroup $groupName3 $template 0 -Zone "us-east1-b"

    It "should fail for illegal project" {
        { Get-GceManagedInstanceGroup -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant group" {
        { Get-GceManagedInstanceGroup -Name "not-a-real-group" } | Should Throw 404
    }

    It "should get 3 project groups" {
        $groups = Get-GceManagedInstanceGroup
        $groups.Count | Should Be 3
        ($groups | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.InstanceGroupManager"
        $groups.Name | Should Match "test-get-managed-instance-group-"
    }

    It "should get 2 zone groups" {
        $groups = Get-GceManagedInstanceGroup -Zone "us-central1-f"
        $groups.Count | Should Be 2
        ($groups | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.InstanceGroupManager"
        $groups.Name | Should Match "test-get-managed-instance-group-"
        $groups.Name | Should Not Match $groupName3
    }

    It "should get 1 group by name" {
        $group = Get-GceManagedInstanceGroup $groupName1
        $group.Count | Should Be 1
        ($group | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.InstanceGroupManager"
        $group.Name | Should Be $groupName1
    }

    It "should get 1 group by uri" {
        $uri = (Get-GceManagedInstanceGroup $groupName1).SelfLink
        $group = Get-GceManagedInstanceGroup -Uri $uri
        $group.Count | Should Be 1
        ($group | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.InstanceGroupManager"
        $group.Name | Should Be $groupName1
    }

    It "should get 1 group by object" {
        $group = (Get-GceManagedInstanceGroup $groupName1) | Get-GceManagedInstanceGroup
        $group.Count | Should Be 1
        ($group | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.InstanceGroupManager"
        $group.Name | Should Be $groupName1
    }

    It "should get instance status" {
        $instances = Get-GceManagedInstanceGroup $groupName1 -InstanceStatus
        ($instances | Get-Member).Typename | Should Be "Google.Apis.Compute.v1.Data.ManagedInstance"
        $instances.CurrentAction | Should Not Be $null
        $instances.Instance | Should Match $groupName1
    }

    Get-GceManagedInstanceGroup | Remove-GceManagedInstanceGroup
}

Describe "Add-GceManagedInstanceGroup" {
    $groupName1 = "test-add-managed-instance-group-$r"

    AfterEach {
        Get-GceManagedInstanceGroup | Remove-GceManagedInstanceGroup
    }

    It "should work minimally" {
        Add-GceManagedInstanceGroup $groupName1 $template 0
        $group = Get-GceManagedinstanceGroup $groupname1
        $group.Count | Should Be 1
        $group.Name | Should Be $groupName1
        $group.TargetSize | Should Be 0
        $group.Description | Should BeNullOrEmpty
        $group.BaseInstanceName | Should Be $groupName1
        $group.TargetPools.Count | Should Be 0
        $group.NamedPorts.Count | Should Be 0
    }

    It "should work with all parameters" {
        Add-GceManagedInstanceGroup $groupName1 $template 0 -BaseinstanceName "test-base-name" `
            -Description "A Test Group" -TargetPool $poolUrl -PortName "named1" -PortNumber 88
        $group = Get-GceManagedinstanceGroup $groupname1
        $group.Name | Should Be $groupName1
        $group.TargetSize | Should Be 0
        $group.Description | Should Be "A Test Group"
        $group.BaseInstanceName | Should Be "test-base-name"
        $group.TargetPools | Should Be $poolUrl
        $group.NamedPorts.Name | Should Be "named1"
        $group.NamedPorts.Port | Should Be 88
    }
}

Describe "Remove-GceManagedInstanceGroup" {

    $groupName1 = "test-remove-managed-instance-group-$r"
    
    It "should fail for wrong project" {
        { Remove-GceManagedInstanceGroup $groupName1 -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant group" {
        { Remove-GceManagedInstanceGroup $groupName1 } | Should Throw 404
    }

    Context "Real Remove" {
        BeforeEach {
            Add-GceManagedInstanceGroup $groupName1 $template 0
        }

        It "should work with name" {
            Remove-GceManagedInstanceGroup $groupName1
            { Get-GceManagedInstanceGroup $groupName1 } | Should Throw 404
        }

        It "should work with name over pipeline" {
            $groupName1 | Remove-GceManagedInstanceGroup 
            { Get-GceManagedInstanceGroup $groupName1 } | Should Throw 404
        }

        It "should work with object" {
            $obj = Get-GceManagedInstanceGroup $groupName1
            Remove-GceManagedInstanceGroup $obj
            { Get-GceManagedInstanceGroup $groupName1 } | Should Throw 404
        }

        It "should work with object over pipeline" {
            Get-GceManagedInstanceGroup $groupName1 | Remove-GceManagedInstanceGroup
            { Get-GceManagedInstanceGroup $groupName1 } | Should Throw 404
        }
    }
}

Describe "Set-GceManagedInstanceGroup" {
    $groupName1 = "test-set-managed-instance-group-$r"

    Add-GceManagedInstanceGroup $groupName1 $template 0

    It "should set size" {
        Set-GceManagedInstanceGroup $groupName1 -Size 3
        $group = Get-GceManagedInstanceGroup $groupName1
        $group.TargetSize | Should Be 3
    }

    It "should set pool" {
        Set-GceManagedInstanceGroup $groupName1 -TargetPoolUri $poolUrl
        $group = Get-GceManagedInstanceGroup $groupName1
        $group.TargetPools | Should Be $poolUrl
    }

    It "should set template" {

    }
}

gcloud compute target-pools delete test-pool -q 2>$null
Get-GceInstanceTemplate | Remove-GceInstanceTemplate

Reset-GCloudConfig $oldActiveConfig $configName
