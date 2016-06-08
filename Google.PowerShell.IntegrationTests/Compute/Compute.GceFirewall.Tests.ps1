. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

Get-GceFirewall -Project $project | Remove-GceFirewall -Project $project

Describe "Get-GceFirewall" {
    $r = Get-Random
    $name = "test-get-firewall-$r"
    $name2 = "test-2-get-firewall-$r"
    $allowed = New-GceFirewallAllowed "tcp" "5" "7" |
        New-GceFirewallAllowed "esp"

    Add-GceFirewall $project $name -Allowed $allowed -Description "one for test $r" `
        -SourceTag "alpha" -TargetTag "beta"
        
    Add-GceFirewall $project $name2 -Allowed $allowed -Description "another for test $r" `
        -SourceTag "alpha2" -TargetTag "beta2"

    It "should get one" {
        $firewall = Get-GceFirewall $project $name
        $firewall.Count | Should Be 1
        $firewall.Description | Should Be "one for test $r"
        $firewall.SourceTag | Should Be "alpha"
    }

    It "should get two" {
        $firewall = Get-GceFirewall $project
        $firewall.Count | Should Be 2
    }

    Remove-GceFirewall $project $name
    Remove-GceFirewall $project $name2
}

Describe "New-GceFirewallAllowed" {
    It "should build one" {
        $data = New-GceFirewallAllowed "protocol"
        ($data | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Firewall.AllowedData"
        $data.IPProtocol | Should Be "protocol"
    }

    It "should append to list" {
        [System.Collections.Generic.List[Google.Apis.Compute.v1.Data.Firewall.AllowedData]] $list = @()
        $output = New-GceFirewallAllowed "protocol" -AppendTo $list
        $output | Should Be $null
        $list.Count | Should Be 1
        $list.IPProtocol | Should Be "protocol"
    }

    It "should append to pipeline" {
        $output = New-GceFirewallAllowed "protocol1" |
            New-GceFirewallAllowed "protocol2" |
            New-GceFirewallAllowed "protocol3" "5"
        $output.Count | Should Be 3
        ($output | Get-Member).TypeName | Should Be "Google.Apis.Compute.v1.Data.Firewall.AllowedData"
        $output[2].Ports | Should Be "5"
    }
}

Describe "Add-GceFirewall" {
    $r = Get-Random
    $name = "test-add-firewall-$r"

    $allowed = New-GceFirewallAllowed "tcp" "5" "7" |
        New-GceFirewallAllowed "esp"

    It "should work" {
        Add-GceFirewall $project $name -Allowed $allowed -Description "test Add $r" `
            -SourceRange "192.168.100.0/22" -SourceTag "alpha" -TargetTag "beta"
        $firewall = Get-GceFirewall $project $name
        $firewall.Description | Should Be "test Add $r"
        $firewall.SourceRanges | Should Be "192.168.100.0/22"
        $firewall.SourceTags | Should Be "alpha"
        $firewall.TargetTags | Should Be "beta"
    }

    Remove-GceFirewall $project $name
}

Describe "Remove-GceFirewall" {
    $r = Get-Random
    $name = "test-remove-firewall-$r"
    Add-GceFirewall $project $name

    It "should work" {
        Remove-GceFirewall $project $name
        {Get-GceFirewall $project $name } | Should throw 404
    }
}

Describe "Set-GceFirewall" {
    $r = Get-Random
    $name = "test-set-firewall-$r"
    $name2 = "test-set-firewall2-$r"
    Add-GceFirewall $project $name

    It "should change data" {
        $firewall = Get-GceFirewall $project $name
        $firewall.SourceRanges = "192.168.100.0/24"
        $firewall.SourceTags = "gamma"
        $firewall.TargetTags = "delta"
        Set-GceFirewall $project $firewall
        $updatedFirewall = Get-GceFirewall $project $name
        $updatedFirewall.SourceRanges | Should Be "192.168.100.0/24"
        $updatedFirewall.SourceTags | Should Be "gamma"
        $updatedFirewall.TargetTags | Should Be "delta"
    }

    It "should rename firewall" {
        $firewall = Get-GceFirewall $project $name
        $firewall.Name = $name2
        Set-GceFirewall $project $firewall -Name $name
        { Get-GceFirewall $project $name } | Should throw 404
        $newFirewall = Get-GceFirewall $project $name2
        $newFirewall.Name | Should Be $name2
    }

    Get-GceFirewall $project | Remove-GceFirewall $project
}
