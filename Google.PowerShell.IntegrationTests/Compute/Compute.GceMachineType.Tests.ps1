. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GceMachineType" {
    $r = Get-Random

    It "should fail for wrong project" {
        { Get-GceMachineType -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant network" {
        { Get-GceMachineType "non-existant-network" } | Should Throw 404
    }

    It "should list by project" {
        $machineTypes = Get-GceMachineType
        $machineTypes.Count | Should BeGreaterThan 1
        ($machineTypes | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.MachineType"
        $machineTypes.SelfLink | Should Match $project
        ($machineTypes.Zone -ne $zone) | Should Not BeNullOrEmpty
    }

    It "should list by zone" {
        $machineTypes = Get-GceMachineType -Zone $zone
        $machineTypes.Count | Should BeGreaterThan 1
        ($machineTypes | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.MachineType"
        $machineTypes.SelfLink | Should Match $project
        $machineTypes.Zone | Should Be $zone
    }

    It "should get by name" {
        $machineType = Get-GceMachineType "f1-micro"
        $machineType.Count | Should Be 1
        ($machineType | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.MachineType"
        $machineType.SelfLink | Should Match $project
        $machineType.Zone | Should Be $zone
        $machineType.Name | Should Be "f1-micro"
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
