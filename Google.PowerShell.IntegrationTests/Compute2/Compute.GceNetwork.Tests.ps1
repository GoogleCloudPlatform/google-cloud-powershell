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
        $networks.Count -ge 2 | Should Be $true
        ($networks | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Network"
    }

    It "should get by name" {
        $network = Get-GceNetwork "default"
        $network.Count | Should Be 1
        ($network | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Network"
    }

    gcloud compute networks delete $newNetwork -q 2>$null
}

Describe "New-GceNetwork" {
    It "should work" {
        $r = Get-Random
        $networkName = "gcp-new-gce-network-$r"
        $description = "My network $r"

        try {
            $network = New-GceNetwork $networkName -Description $description
            $network.Name | Should Match $networkName
            $network.Description | Should BeExactly $description
            $network.Subnetworks | Should BeNullOrEmpty

            $onlineNetwork = Get-GceNetwork $networkName
            $onlineNetwork.Name | Should Match $networkName
            $onlineNetwork.Description | Should BeExactly $description
            $network.Subnetworks | Should BeNullOrEmpty
        }
        finally {
            gcloud compute networks delete $networkName -q 2>$null
        }
    }

    It "should create a auto subnet network" {
        $r = Get-Random
        $networkName = "gcp-new-gce-network-$r"
        $description = "My network $r"

        try {
            $network = New-GceNetwork $networkName -AutoSubnet
            $network.Name | Should Match $networkName
            $network.Subnetworks | Should Not BeNullOrEmpty

            $onlineNetwork = Get-GceNetwork $networkName
            $onlineNetwork.Name | Should Match $networkName
            $network.Subnetworks | Should Not BeNullOrEmpty
        }
        finally {
            gcloud compute networks delete $networkName -q 2>$null
        }
    }

    It "should create a legacy network" {
        $r = Get-Random
        $networkName = "gcp-new-gce-network-$r"
        $description = "My network $r"

        try {
            $network = New-GceNetwork $networkName -IPv4Range 192.168.0.0/16
            $network.Name | Should Match $networkName
            $network.IpV4Range | Should Be "192.168.0.0/16"
            $network.Subnetworks | Should BeNullOrEmpty

            $onlineNetwork = Get-GceNetwork $networkName
            $onlineNetwork.Name | Should Match $networkName
            $onlineNetwork.IpV4Range | Should Be "192.168.0.0/16"
            $network.Subnetworks | Should BeNullOrEmpty
        }
        finally {
            gcloud compute networks delete $networkName -q 2>$null
        }
    }

    It "should throw error for invalid network name" {
        { New-GceNetwork "!!" -ErrorAction Stop } |
            Should Throw "Cannot validate argument on parameter 'Name'"
    }

    It "should throw error for existing network" {
        $r = Get-Random
        $networkName = "gcp-new-gce-network-$r"
        $description = "My network $r"

        try {
            $network = New-GceNetwork $networkName -IPv4Range 192.168.0.0/16

            { New-GceNetwork $networkName -ErrorAction Stop } |
                Should Throw "already exists"
        }
        finally {
            gcloud compute networks delete $networkName -q 2>$null
        }
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
