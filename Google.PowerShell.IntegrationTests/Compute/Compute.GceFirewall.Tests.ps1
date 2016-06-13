. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldConfig, $newConfigName = Set-GCloudConfig

Get-GceFirewall -Project $project | Remove-GceFirewall -Project $project

Describe "Get-GceFirewall" {
    $r = Get-Random
    $name = "test-get-firewall-$r"
    $name2 = "test-2-get-firewall-$r"
    $allowed = New-GceFirewallProtocol "tcp" -Port 5, 7 |
        New-GceFirewallProtocol "esp"

    Add-GceFirewall $name -Allowed $allowed -Description "one for test $r" `
        -SourceTag "alpha" -TargetTag "beta"
        
    Add-GceFirewall $name2 -Allowed $allowed -Description "another for test $r" `
        -SourceTag "alpha2" -TargetTag "beta2"

    It "should get one firewall" {
        $firewall = Get-GceFirewall $name
        $firewall.Count | Should Be 1
        $firewall.Description | Should Be "one for test $r"
        $firewall.SourceTags | Should Be "alpha"
    }

    It "should get all project firewalls" {
        $firewall = Get-GceFirewall
        $firewall.Count | Should Be 2
        $firewall.Allowed.Count | Should Be 4
        ($firewall.Allowed.IPProtocol -match "esp").Count | Should Be 2
        $firewall.SourceTags -contains "alpha2" | Should Be $true
        $firewall.SourceTags -contains "alpha" | Should Be $true
        $firewall.TargetTags -contains "beta" | Should Be $true
        $firewall.TargetTags -contains "beta2" | Should Be $true
        
    }

    Remove-GceFirewall $name
    Remove-GceFirewall $name2
}

Describe "New-GceFirewallProtocol" {
    It "should build one" {
        $data = New-GceFirewallProtocol "protocol"
        ($data | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Firewall+AllowedData"
        $data.IPProtocol | Should Be "protocol"
    }

    It "should append to pipeline" {
        $output = New-GceFirewallProtocol "protocol1" |
            New-GceFirewallProtocol "protocol2" |
            New-GceFirewallProtocol "protocol3" -Port "5"
        $output.Count | Should Be 3
        ($output | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Firewall+AllowedData"
        $output[2].Ports | Should Be "5"
    }
}

Describe "Add-GceFirewall" {
    $r = Get-Random
    $name = "test-add-firewall-$r"

    $allowed = New-GceFirewallProtocol "tcp" -Port "5", "7" |
        New-GceFirewallProtocol "esp"

    It "should work" {
        $firewall = Add-GceFirewall $name -Allowed $allowed -Description "test Add $r" `
            -SourceRange "192.168.100.0/22", "2001:db8::/48" -SourceTag "alpha" -TargetTag "beta"
        $firewall.Description | Should Be "test Add $r"
        $firewall.SourceRanges.Count | Should Be 2
        $firewall.SourceRanges -contains "192.168.100.0/22" | Should Be $true
        $firewall.SourceRanges -contains "2001:db8::/48" | Should Be $true
        $firewall.SourceTags | Should Be "alpha"
        $firewall.TargetTags | Should Be "beta"
    }

    Remove-GceFirewall $name
}

Describe "Remove-GceFirewall" {
    $r = Get-Random
    $name = "test-remove-firewall-$r"
    New-GceFirewallProtocol "tcp" -Port 5, 7 |
        Add-GceFirewall $name

    It "should work" {
        Remove-GceFirewall $name
        {Get-GceFirewall $name } | Should throw 404
    }

    It "should throw on non-existant firewall" {
        { Remove-GceFirewall $name } | Should throw 404
    }
}

Describe "Set-GceFirewall" {
    $r = Get-Random
    $name = "test-set-firewall-$r"
    New-GceFirewallProtocol "tcp" -Port 5, 7 |
        Add-GceFirewall $name

    It "should change data" {
        $firewall = Get-GceFirewall $name
        $firewall.SourceRanges = [string[]] "192.168.100.0/24"
        $firewall.SourceTags = [string[]] "gamma"
        $firewall.TargetTags = [string[]] "delta"
        Set-GceFirewall $firewall

        $updatedFirewall = Get-GceFirewall $name
        $updatedFirewall.SourceRanges | Should Be "192.168.100.0/24"
        $updatedFirewall.SourceTags | Should Be "gamma"
        $updatedFirewall.TargetTags | Should Be "delta"
    }

    Remove-GceFirewall $name
}

Reset-GCloudConfig $oldConfig $newConfigName
