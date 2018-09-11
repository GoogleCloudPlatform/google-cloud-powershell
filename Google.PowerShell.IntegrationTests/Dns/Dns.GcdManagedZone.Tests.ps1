. $PSScriptRoot\..\Dns\GcdCmdlets.ps1
Install-GcloudCmdlets
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcdManagedZone" {
    BeforeAll {
        Remove-AllManagedZone($project)
    }
    AfterAll {
        Remove-AllManagedZone($project)
    }

    It "should fail to return ManagedZones of non-existent project" {
        { Get-GcdManagedZone -Project $nonExistProject } | Should Throw "403"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdManagedZone -Project $accessErrProject } | Should Throw "403"
    }

    It "should fail to return non-existent ManagedZones of existing project" {
        { Get-GcdManagedZone -Project $project $nonExistManagedZone } | Should Throw "404"
    }

    It "should list exactly 0 ManagedZones in project" {
        (Get-GcdManagedZone -Project $project).Count | Should Be 0
    }

    # Create 2 test zones
    gcloud dns managed-zones create --dns-name=$dnsName1 --description=$testDescrip1 $testZone1 --project=$project 2>$null
    gcloud dns managed-zones create --dns-name=$dnsName2 --description=$testDescrip2 $testZone2 --project=$project 2>$null

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
        $zones = Get-GcdManagedZone -Project $project $testZone2
        $zones.GetType().FullName | Should Match $managedZoneType
        $zones.Description | Should Match $testDescrip2
        $zones.DnsName | Should Match $dnsName2
        $zones.Kind | Should Match $managedZoneKind
        $zones.Name | Should Match $testZone2
    }
}

Describe "Add-GcdManagedZone" {
    BeforeAll {
        Remove-AllManagedZone($project)
    }
    AfterAll {
        Remove-AllManagedZone($project)
    }

    It "should fail to create a ManagedZone in a non-existent project" {
        { Add-GcdManagedZone -Project $nonExistProject $testZone1 $dnsName1 } | Should Throw "403"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Add-GcdManagedZone -Project $accessErrProject $testZone1 $dnsName1 } | Should Throw "403"
    }

    It "should create and return 1 zone" {
        $newZone = Add-GcdManagedZone -Project $project $testZone1 $dnsName1
        $newZone.GetType().FullName | Should Match $managedZoneType
        $newZone.Description | Should Match ""
        $newZone.DnsName | Should Match $dnsName1
        $newZone.Kind | Should Match $managedZoneKind
        $newZone.Name | Should Match $testZone1
    }

    It "should fail to create a new ManagedZone with the same name as an existing one" {
        { Add-GcdManagedZone -Project $project $testZone1 $dnsName1 } | Should Throw "409"
    }

    It "should work and have Get-GcdManagedZone retrieve the correct details of both created zones" {
        # Create second zone for testing with dns name that lacks ending period (should be auto-added by cmdlet)
        Add-GcdManagedZone -Project $project $testZone2 $dnsName2 $testDescrip2

        (Get-GcdManagedZone -Project $project).Count | Should Be 2

        $zone1 = Get-GcdManagedZone -Project $project -ManagedZone $testZone1
        $zone1.GetType().FullName | Should Match $managedZoneType
        $zone1.Description | Should Match ""
        $zone1.DnsName | Should Match $dnsName1
        $zone1.Kind | Should Match $managedZoneKind
        $zone1.Name | Should Match $testZone1

        $zone2 = Get-GcdManagedZone -Project $project -ManagedZone $testZone2
        $zone2.GetType().FullName | Should Match $managedZoneType
        $zone2.Description | Should Match $testDescrip2
        $zone2.DnsName | Should Match $dnsName2
        $zone2.Kind | Should Match $managedZoneKind
        $zone2.Name | Should Match $testZone2
    }
}

Describe "Remove-GcdManagedZone" {
    BeforeAll {
        Remove-AllManagedZone($project)
    }
    AfterAll {
        Remove-AllManagedZone($project)
    }

    It "should fail to delete a ManagedZone in a non-existent project" {
        { Remove-GcdManagedZone -Project $nonExistProject $testZone1 } | Should Throw "403"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Remove-GcdManagedZone -Project $accessErrProject $testZone1 } | Should Throw "403"
    }

    It "should fail to delete a non-existent ManagedZone in an existent project" {
        { Remove-GcdManagedZone -Project $project $nonExistManagedZone } | Should Throw "404"
    }

    # Create two zones for testing 
    Add-GcdManagedZone -Project $project -Name $testZone1 -DnsName $dnsName1
    Add-GcdManagedZone -Project $project -Name $testZone2 -DnsName $dnsName2

    It "should delete only the first zone created and output nothing" {
        Remove-GcdManagedZone -Project $project $testZone1 | Should Be $null

        { Get-GcdManagedZone -Project $project -ManagedZone $testZone1 } | Should Throw "404"
        (Get-GcdManagedZone -Project $project).Count | Should Be 1
        (Get-GcdManagedZone -Project $project).Name | Should Match $testZone2
    }
    
    # Recreate deleted zone for testing 
    Add-GcdManagedZone -Project $project -Name $testZone1 -DnsName $dnsName1

    It "should delete all (2) zones in project using pipeline input" {
        (Get-GcdManagedZone -Project $project | Remove-GcdManagedZone -Project $project) | Should Be $null

        Get-GcdManagedZone -Project $project | Should Be $null
    }

    # Create a 2 zones, 1 non-empty, for testing
    Add-GcdManagedZone -Project $project -Name $testZone1 -DnsName $dnsName1
    Add-GcdManagedZone -Project $project -Name $testZone2 -DnsName $dnsName2
    Add-GcdChange -Project $project -Zone $testZone1 -Add $testRrsetA,$testRrsetCNAME

    It "should delete a non-empty zone when -Force is specified" {
        Remove-GcdManagedZone -Project $project $testZone1 -Force | Should Be $null

        { Get-GcdManagedZone -Project $project -ManagedZone $testZone1 } | Should Throw "404"
        (Get-GcdManagedZone -Project $project).Count | Should Be 1
        (Get-GcdManagedZone -Project $project).Name | Should Match $testZone2
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
