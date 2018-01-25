. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Get-GceFirewall -Project $project | Remove-GceFirewall

Describe "Get-GceFirewall" {
    $r = Get-Random
    $name = "test-get-firewall-$r"
    $name2 = "test-2-get-firewall-$r"
    $allowed = New-GceFirewallProtocol "tcp" -Port 5, 7 |
        New-GceFirewallProtocol "esp"

    Add-GceFirewall -Project $project $name -Allowed $allowed -Description "one for test $r" `
        -SourceTag "alpha" -TargetTag "beta"
        
    Add-GceFirewall -Project $project $name2 -Allowed $allowed -Description "another for test $r" `
        -SourceTag "alpha2" -TargetTag "beta2"

    It "should get one firewall" {
        $firewall = Get-GceFirewall -Project $project $name
        $firewall.Count | Should Be 1
        $firewall.Description | Should Be "one for test $r"
        $firewall.SourceTags | Should Be "alpha"
    }

    It "should infer project from gcloud config" {
        $firewall = Get-GceFirewall $name
        $firewall.Count | Should Be 1
        $firewall.Description | Should Be "one for test $r"
        $firewall.SourceTags | Should Be "alpha"
    }

    It "should get all project firewalls" {
        $firewall = Get-GceFirewall -Project $project
        $firewall.Count | Should Be 2
        $firewall.Allowed.Count | Should Be 4
        ($firewall.Allowed.IPProtocol -match "esp").Count | Should Be 2
        $firewall.SourceTags -contains "alpha2" | Should Be $true
        $firewall.SourceTags -contains "alpha" | Should Be $true
        $firewall.TargetTags -contains "beta" | Should Be $true
        $firewall.TargetTags -contains "beta2" | Should Be $true
        
    }

    Remove-GceFirewall -Project $project $name
    Remove-GceFirewall -Project $project $name2
}

Describe "New-GceFirewallProtocol" {
    It "should build one" {
        $data = New-GceFirewallProtocol "protocol"
        ($data | Get-Member).TypeName | ForEach-Object { $_ | Should Be "Google.Apis.Compute.v1.Data.Firewall+AllowedData" }
        $data.IPProtocol | Should Be "protocol"
    }

    It "should build one with port ranges" {
        $data = New-GceFirewallProtocol "protocol" -Port 80..443
        $data.Ports[0] | Should Be 80-443

        $data = New-GceFirewallProtocol "protocol" -Port 80-443
        $data.Ports[0] | Should Be 80-443
    }

    It "should append to pipeline" {
        $output = New-GceFirewallProtocol "protocol1" |
            New-GceFirewallProtocol "protocol2" |
            New-GceFirewallProtocol "protocol3" -Port "5"
        $output.Count | Should Be 3
        ($output | Get-Member).TypeName | ForEach-Object { $_ | Should Be "Google.Apis.Compute.v1.Data.Firewall+AllowedData" }
        $output[2].Ports | Should Be "5"
    }
}

Describe "Add-GceFirewall" {
    $allowed = New-GceFirewallProtocol "tcp" -Port "5", "7" |
        New-GceFirewallProtocol "esp"

    It "should work" {
        $r = Get-Random
        $name = "test-add-firewall-$r"

        try {
            $firewall = Add-GceFirewall -Project $project $name -Allowed $allowed -Description "test Add $r" `
                -SourceRange "192.168.100.0/22", "192.168.100.0/30" -SourceTag "alpha" -TargetTag "beta"

            $firewall.Name | Should Be $name
            $firewall.Description | Should Be "test Add $r"
            $firewall.SourceRanges.Count | Should Be 2
            $firewall.SourceRanges -contains "192.168.100.0/22" | Should Be $true
            $firewall.SourceRanges -contains "192.168.100.0/30" | Should Be $true
            $firewall.SourceTags | Should Be "alpha"
            $firewall.TargetTags | Should Be "beta"
            $firewall.Network | Should Match "default"

            $onlineFirewall = Get-GceFirewall -Project $project $name
            $onlineFirewall.Description | Should Be "test Add $r"
            $onlineFirewall.Network | Should Match "default"
        }
        finally {
            Remove-GceFirewall -Project $project $name
        }
    }

    It "should works with simple network name" {
        $r = Get-Random
        $newNetwork = "test-network-$r"
        $firewallName = "test-add-gcefirewall-$r"

        try {
            gcloud compute networks create $newNetwork 2>$null
            $firewall = Add-GceFirewall $firewallName -Allowed $allowed -Description "one for test $r" `
                -SourceTag "alpha" -TargetTag "beta" -Network $newNetwork

            $firewall.Name | Should Be $firewallName
            $firewall.Network | Should Match $netNetwork
            $firewall.Description | Should Be "one for test $r"

            $onlineFirewall = Get-GceFirewall $firewallName
            $onlineFirewall.Network | Should Match $newNetwork
            $onlineFirewall.Description | Should Be "one for test $r"
        }
        finally {
            Remove-GceFirewall $firewallName
            gcloud compute networks delete $newNetwork -q 2>$null
        }
    }
}

Describe "Remove-GceFirewall" {
    $r = Get-Random
    $name = "test-remove-firewall-$r"

    It "should fail for wrong project" {
        { Remove-GceFirewall $name -Project "asdf" } | Should throw 403
    }
    It "should throw on non-existant firewall" {
        { Remove-GceFirewall $name } | Should throw 404
    }

    Context "real remove" {
        BeforeEach {
            New-GceFirewallProtocol "tcp" -Port 5, 7 |
                Add-GceFirewall $name
        }

        It "should work" {
            Remove-GceFirewall $name
            { Get-GceFirewall $name } | Should throw 404
        }

        It "should take pipeline object" {
            Get-GceFirewall $name | Remove-GceFirewall
            { Get-GceFirewall $name } | Should throw 404
        }
    }
}

Describe "Set-GceFirewall" {
    $r = Get-Random
    $name = "test-set-firewall-$r"
    New-GceFirewallProtocol "tcp" -Port 5, 7 |
        Add-GceFirewall -Project $project $name

    It "should change data" {
        $firewall = Get-GceFirewall -Project $project $name
        $firewall.SourceRanges = [string[]] "192.168.100.0/24"
        $firewall.SourceTags = [string[]] "gamma"
        $firewall.TargetTags = [string[]] "delta"
        Set-GceFirewall -Project $project $firewall

        $updatedFirewall = Get-GceFirewall -Project $project $name
        $updatedFirewall.SourceRanges | Should Be "192.168.100.0/24"
        $updatedFirewall.SourceTags | Should Be "gamma"
        $updatedFirewall.TargetTags | Should Be "delta"
    }

    Remove-GceFirewall -Project $project $name
}

Reset-GCloudConfig $oldActiveConfig $configName
