. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GceNetwork" {
    $r = Get-Random
    $newNetwork = "test-network-$r"
    gcloud compute networks create $newNetwork 2>$null

    It "should fail for wrong project" {
        { Get-GceNetwork -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant network" {
        { Get-GceNetwork "non-existant-network" } | Should Throw 404
    }

    It "should list by project" {
        $networks = Get-GceNetwork
        $networks.Count | Should Be 2
        ($networks | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Network"
    }

    It "should get by name" {
        $network = Get-GceNetwork "default"
        $network.Count | Should Be 1
        ($network | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Network"
    }

    gcloud compute networks delete $newNetwork -q 2>$null
}

Reset-GCloudConfig $oldActiveConfig $configName
