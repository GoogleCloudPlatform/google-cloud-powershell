. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GceNetwork" {

    It "should fail for wrong project" {
        { Get-GceNetwork -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant network" {
        { Get-GceNetwork "non-existant-network" } | Should Throw 404
    }

    It "should list by project" {
        $networks = Get-GceNetwork
        $networks.Count | Should Be 1
        ($networks | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Network"
    }

    It "should get by name" {
        $network = Get-GceNetwork "default"
        $network.Count | Should Be 1
        ($network | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Network"
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
