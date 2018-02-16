. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$image = Get-GceImage -Family "windows-2012-r2"
$machineType = "f1-micro"
$r = Get-Random
$templateName = "test-managed-instance-groups-$r"

Get-GceManagedInstanceGroup | Remove-GceManagedInstanceGroup
Get-GceInstanceTemplate | Remove-GceInstanceTemplate
Add-GceInstanceTemplate -Name $templateName -MachineType $machineType -BootDiskImage $image
$template = Get-GceInstanceTemplate $templateName

gcloud compute target-pools create test-pool --region us-central1 2>$null
$poolUrl = (Get-GceTargetPool -Name test-pool).SelfLink

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

    It "should get the groups of the project" {
        $groups = Get-GceManagedInstanceGroup
        $groups.Count | Should Be 3
        ($groups | Get-Member).TypeName | ForEach-Object { $_ | Should Be "Google.Apis.Compute.v1.Data.InstanceGroupManager" }
        $groups.Name | ForEach-Object { $_ | Should Match "test-get-managed-instance-group-" }
    }

    It "should get the groups of a zone" {
        $groups = Get-GceManagedInstanceGroup -Zone "us-central1-f"
        $groups.Count | Should Be 2
        ($groups | Get-Member).TypeName | ForEach-Object { $_ | Should Be "Google.Apis.Compute.v1.Data.InstanceGroupManager" }
        $groups.Name | ForEach-Object { $_ | Should Match "test-get-managed-instance-group-" }
        $groups.Name | ForEach-Object { $_ | Should Not Match $groupName3 }
    }

    It "should get 1 group by name" {
        $group = Get-GceManagedInstanceGroup $groupName1
        $group.Count | Should Be 1
        ($group | Get-Member).TypeName | ForEach-Object { $_ | Should Be "Google.Apis.Compute.v1.Data.InstanceGroupManager" }
        $group.Name | ForEach-Object { $_ | Should Be $groupName1 }
    }

    It "should get 1 group by uri" {
        $uri = (Get-GceManagedInstanceGroup $groupName1).SelfLink
        $group = Get-GceManagedInstanceGroup -Uri $uri
        $group.Count | Should Be 1
        ($group | Get-Member).TypeName | ForEach-Object { $_ | Should Be "Google.Apis.Compute.v1.Data.InstanceGroupManager" }
        $group.Name | ForEach-Object { $_ | Should Be $groupName1 }
    }

    It "should get 1 group by object" {
        $group = (Get-GceManagedInstanceGroup $groupName1) | Get-GceManagedInstanceGroup
        $group.Count | Should Be 1
        ($group | Get-Member).TypeName | ForEach-Object { $_ | Should Be "Google.Apis.Compute.v1.Data.InstanceGroupManager" }
        $group.Name | ForEach-Object { $_ | Should Be $groupName1 }
    }

    It "should get instance status" {
        $instances = Get-GceManagedInstanceGroup $groupName1 -InstanceStatus
        ($instances | Get-Member).Typename | ForEach-Object { $_ | Should Be "Google.Apis.Compute.v1.Data.ManagedInstance" }
        $instances.CurrentAction | ForEach-Object { $_ | Should Not BeNullOrEmpty }
        $instances.Instance | ForEach-Object { $_ | Should Match $groupName1 }
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
    $templateName2 = "test-set-managed-instance-group-$r"
    Add-GceInstanceTemplate -Name $templateName2 -MachineType $machineType -BootDiskImage $image
    $template2 = Get-GceInstanceTemplate -Name $templateName2
    
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
        Set-GceManagedInstanceGroup $groupName1 -Template $template2
        $group = Get-GceManagedInstanceGroup $groupName1
        $group.InstanceTemplate | Should Be $template2.SelfLink
    }

    # Wait for instances to get to a normal state before running tests that affect the instances.
    Wait-GceManagedInstanceGroup $groupName1

    It "should abandon instances" {
        $instances = Get-GceInstance -ManagedGroupName $groupName1
        $instanceToAbandon = $instances[0]
        $instanceToAbandon | Set-GceManagedInstanceGroup $groupName1 -Abandon

        # Wait for the instance to be abandoned.
        Wait-GceManagedInstanceGroup $groupName1

        $instanceStatus = Get-GceManagedInstanceGroup $groupName1 -InstanceStatus
        $instanceStatus.Instance | Should Not Be $instanceToAbandon.SelfLink
        $group = Get-GceManagedInstanceGroup $groupName1
        $group.TargetSize | Should Be 2
        { Get-GceInstance $instanceToAbandon } | Should Not Throw 404
        $instanceToAbandon | Remove-GceInstance
    }

    It "should delete instances" {
        $instances = Get-GceInstance -ManagedGroupName $groupName1
        $instanceToDelete = $instances[0]
        $instanceToDelete | Set-GceManagedInstanceGroup $groupName1 -Delete
        $instanceStatus = Get-GceManagedInstanceGroup $groupName1 -InstanceStatus

        ($instanceStatus | Where { $_.Instance -eq $instanceToDelete.SelfLink }).CurrentAction | Should Be DELETING
        $group = Get-GceManagedInstanceGroup $groupName1
        $group.TargetSize | Should Be 1

        # Wait for the instance to be deleted.
        Wait-GceManagedInstanceGroup $groupName1

        $instanceStatus = Get-GceManagedInstanceGroup $groupName1 -InstanceStatus
        $instanceStatus.Instance | Should Not Be $instanceToAbandon.SelfLink
        { Get-GceInstance $instanceToDelete } | Should Throw 404
    }

    It "should recreate instances" {
        $instanceToRecreate = Get-GceInstance -ManagedGroupName $groupName1
        $instanceToRecreate | Set-GceManagedInstanceGroup $groupName1 -Recreate
        $instanceStatus = Get-GceManagedInstanceGroup $groupName1 -InstanceStatus
        $instanceStatus.Instance | Should Be $instanceToRecreate.SelfLink
        $instanceStatus.CurrentAction | Should Be RECREATING
        $group = Get-GceManagedInstanceGroup $groupName1
        $group.TargetSize | Should Be 1
    }

    Remove-GceManagedInstanceGroup $groupName1
    Remove-GceInstanceTemplate $templateName2
}

Describe "Wait-GceManagedInstanceGroup" {
    $groupName1 = "test-wait-managed-instance-group-$r"
    Add-GceManagedInstanceGroup $groupName1 $template 0

    It "should fail with non-existant groupname" {
        { Wait-GceManagedInstanceGroup "group-not-exist" } | Should Throw 404
    }

    It "should work with no instances" {
        { Wait-GceManagedInstanceGroup $groupName1 } | Should Not Throw
    }

    It "should wait for resize" {
        Set-GceManagedInstanceGroup $groupName1 -Size 1
        (Get-GceManagedInstanceGroup $groupName1 -InstanceStatus).CurrentAction | Should Not Be NONE

        Wait-GceManagedInstanceGroup $groupName1

        (Get-GceManagedInstanceGroup $groupName1 -InstanceStatus).CurrentAction | Should Be NONE
    }

    It "should warn on timeout" {
        Get-GceInstance -ManagedGroupName $groupName1 | Set-GceManagedInstanceGroup $groupName1 -Delete
        Wait-GceManagedInstanceGroup $groupName1 0 3>&1 | Should Match "Wait-GceManagedInstanceGroup timed out"
    }

    Remove-GceManagedInstanceGroup $groupName1
}

gcloud compute target-pools delete test-pool --region us-central1 -q 2>$null
Get-GceInstanceTemplate | Remove-GceInstanceTemplate

Reset-GCloudConfig $oldActiveConfig $configName
