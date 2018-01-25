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
        ($networks | Get-Member).TypeName | ForEach-Object { $_ | Should Be "Google.Apis.Compute.v1.Data.Network" }
    }

    It "should get by name" {
        $network = Get-GceNetwork "default"
        $network.Count | Should Be 1
        ($network | Get-Member).TypeName | ForEach-Object { $_ | Should Be "Google.Apis.Compute.v1.Data.Network" }

        $network = Get-GceNetwork "default", $newNetwork
        $network.Count | Should Be 2
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

Describe "Remove-GceNetwork" {
    It "should work" {
        $r = Get-Random
        $networkName = "gcp-remove-network-$r"
        $secondNetworkName = "gcp-remove-network2-$r"
        $thirdNetworkName = "gcp-remove-network3-$r"
        New-GceNetwork $networkName
        New-GceNetwork $secondNetworkName
        New-GceNetwork $thirdNetworkName

        Get-GceNetwork -Network $networkName | Should Not BeNullOrEmpty
        Get-GceNetwork -Network $secondNetworkName | Should Not BeNullOrEmpty
        Get-GceNetwork -Network $thirdNetworkName | Should Not BeNullOrEmpty

        # Remove a single network.
        Remove-GceNetwork -Network $networkName
        { Get-GceNetwork -Network $networkName -ErrorAction Stop } | Should Throw "not found"

        # Remove an array of networks.
        Remove-GceNetwork -Network $secondNetworkName, $thirdNetworkName
        { Get-GceNetwork -Network $secondNetworkName -ErrorAction Stop } | Should Throw "not found"
        { Get-GceNetwork -Network $thirdNetworkName -ErrorAction Stop } | Should Throw "not found"
    }

    It "should work with object" {
        $r = Get-Random
        $networkName = "gcp-remove-network-$r"
        $secondNetworkName = "gcp-remove-network2-$r"
        New-GceNetwork $networkName
        New-GceNetwork $secondNetworkName

        $networks = Get-GceNetwork -Network $networkName, $secondNetworkName
        $networks.Count | Should Not BeNullOrEmpty

        Get-GceNetwork -Network $networkName, $secondNetworkName | Remove-GceNetwork
        { Get-GceNetwork -Network $networkName -ErrorAction Stop } | Should Throw "not found"
    }

    It "should work with pipeline" {
        $r = Get-Random
        $networkName = "gcp-remove-network-$r"
        New-GceNetwork -Network $networkName
        Get-GceNetwork -Network $networkName | Should Not BeNullOrEmpty

        # Remove through pipeline
        Get-GceNetwork -Network $networkName | Remove-GceNetwork
        { Get-GceNetwork -Network $networkName -ErrorAction Stop } | Should Throw "not found"
    }

    It "should throw error for non-existent network" {
        { Remove-GceNetwork -Network "non-existent-network-powershell-testing" -ErrorAction Stop } |
            Should Throw "not exist"
    }

    It "should throw error for invalid network name" {
        { Remove-GceNetwork -Network "!!" -ErrorAction Stop } | Should Throw "Parameter validation failed"
    }

    It "should not remove network if -WhatIf is used" {
        $r = Get-Random
        $networkName = "gcp-remove-network-$r"
        New-GceNetwork -Network $networkName
        Get-GceNetwork -Network $networkName | Should Not BeNullOrEmpty

        # Network should not be removed.
        Remove-GceNetwork -Network $networkName -WhatIf
        Get-GceNetwork -Network $networkName | Should Not BeNullOrEmpty

        Remove-GceNetwork -Network $networkName
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
