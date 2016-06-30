. $PSScriptRoot\..\Dns\GcdCmdlets.ps1

Describe "Get-GcdManagedZone" {
    BeforeAll {
        Remove-AllManagedZone($project)
    }
    AfterAll {
        Remove-AllManagedZone($project)
    }

    It "should fail to return ManagedZones of non-existent project" {
        { Get-GcdManagedZone -Project $nonExistProject } | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdManagedZone -Project $accessErrProject } | Should Throw "403"
    }

    It "should fail to return non-existent ManagedZones of existing project" {
        { Get-GcdManagedZone -Project $project -ManagedZone $nonExistManagedZone } | Should Throw "404"
    }

    It "should list exactly 0 ManagedZones in project" {
        (Get-GcdManagedZone -Project $project).Count | Should Be 0
    }

    # Create 2 test zones
    gcloud dns managed-zones create --dns-name=$dnsName1 --description=$testDescrip1 $testZone1 --project=$project
    gcloud dns managed-zones create --dns-name=$dnsName2 --description=$testDescrip2 $testZone2 --project=$project

    It "should work and list the 2 ManagedZones just created" {
        $zones = Get-GcdManagedZone -Project $project
        $zones.Count | Should Be 2

        ($zones | Get-Member).TypeName | Should Match $managedZoneType
        $zones.Kind | Should Match $managedZoneKind

        (($zones.Description -contains $testDescrip1) -and ($zones.Description -contains $testDescrip2)) | Should Match $true
        (($zones.DnsName -contains $dnsName1) -and ($zones.DnsName -contains $dnsName2)) | Should Match $true
        (($zones.Name -contains $testZone1) -and ($zones.Name -contains $testZone2)) | Should Match $true
    }

    It "should work and retrieve ManagedZone testZone2" {
        $zones = Get-GcdManagedZone -Project $project -ManagedZone $testZone2
        $zones.GetType().FullName | Should Match $managedZoneType
        $zones.Description | Should Match $testDescrip2
        $zones.DnsName | Should Match $dnsName2
        $zones.Kind | Should Match $managedZoneKind
        $zones.Name | Should Match $testZone2
    }
}
