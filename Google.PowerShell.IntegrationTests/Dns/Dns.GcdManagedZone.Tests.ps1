. $PSScriptRoot\..\Dns\GcdCmdlets.ps1

Describe "Get-GcdManagedZone" {
    BeforeAll {
        Remove-AllManagedZone($project)
    }
    AfterAll {
        Remove-AllManagedZone($project)
    }

    It "should fail to return ManagedZones of non-existent project" {
        { Get-GcdManagedZone -DnsProject $nonExistProject } | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdManagedZone -DnsProject $accessErrProject } | Should Throw "403"
    }

    It "should fail to return non-existent ManagedZones of existing project" {
        { Get-GcdManagedZone -DnsProject $project -ManagedZone $nonExistManagedZone } | Should Throw "404"
    }

    It "should list exactly 0 ManagedZones in project" {
        (Get-GcdManagedZone -DnsProject $project).Count | Should Be 0
    }

    # Create 2 test zones
    gcloud dns managed-zones create --dns-name=$dnsName1 --description=$testDescrip1 $testZone1 --project=$project
    gcloud dns managed-zones create --dns-name=$dnsName2 --description=$testDescrip2 $testZone2 --project=$project

    It "should work and list the 2 ManagedZones just created" {
        $zones = Get-GcdManagedZone -DnsProject $project
        $zones.Count | Should Be 2

        ($zones | Get-Member).TypeName | Should Match $managedZoneType
        $zones.Kind | Should Match $managedZoneKind

        (($zones.Description -contains $testDescrip1) -and ($zones.Description -contains $testDescrip2)) | Should Match $true
        (($zones.DnsName -contains $dnsName1) -and ($zones.DnsName -contains $dnsName2)) | Should Match $true
        (($zones.Name -contains $testZone1) -and ($zones.Name -contains $testZone2)) | Should Match $true
    }

    It "should work and retrieve ManagedZone testZone2" {
        $zones = Get-GcdManagedZone -DnsProject $project -ManagedZone $testZone2
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
        { Add-GcdManagedZone -DnsProject $nonExistProject -Name $testZone1 -DnsName $dnsName1 } | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Add-GcdManagedZone -DnsProject $accessErrProject -Name $testZone1 -DnsName $dnsName1 } | Should Throw "403"
    }

    It "should fail to create a new ManagedZone with an invalid name" {
        { Add-GcdManagedZone -DnsProject $project -Name "1invalid-zone" -DnsName $dnsName1 } | Should Throw "400"
    }

    It "should fail to create a new ManagedZone with an invalid DNS name" {
        { Add-GcdManagedZone -DnsProject $project -Name $testZone1 -DnsName "invalid-dns" } | Should Throw "400"
    }

    It "should fail to create a new ManagedZone with an invalid (too long) description" {
        $longString = "*" * 1025
        { Add-GcdManagedZone -DnsProject $project -Name $testZone1 -DnsName $dnsName1 -Description $longString } | Should Throw "400"
    }

    It "should create and return 1 zone" {
        $newZone = Add-GcdManagedZone -DnsProject $project -Name $testZone1 -DnsName $dnsName1
        $newZone.GetType().FullName | Should Match $managedZoneType
        $newZone.Description | Should Match ""
        $newZone.DnsName | Should Match $dnsName1
        $newZone.Kind | Should Match $managedZoneKind
        $newZone.Name | Should Match $testZone1
    }

    It "should fail to create a new ManagedZone with the same name as an existing one" {
        { Add-GcdManagedZone -DnsProject $project -Name $testZone1 -DnsName $dnsName1 } | Should Throw "409"
    }

    # Create second zone for testing with dns name that lacks ending period (should be auto-added by cmdlet)
    Add-GcdManagedZone -DnsProject $project -Name $testZone2 -DnsName $dnsName2 -Description $testDescrip2

    It "should work and have Get-GcdManagedZone retrieve the correct details of both created zones" {
        (Get-GcdManagedZone -DnsProject $project).Count | Should Be 2

        $zone1 = Get-GcdManagedZone -DnsProject $project -ManagedZone $testZone1
        $zone1.GetType().FullName | Should Match $managedZoneType
        $zone1.Description | Should Match ""
        $zone1.DnsName | Should Match $dnsName1
        $zone1.Kind | Should Match $managedZoneKind
        $zone1.Name | Should Match $testZone1

        $zone2 = Get-GcdManagedZone -DnsProject $project -ManagedZone $testZone2
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
        { Remove-GcdManagedZone -DnsProject $nonExistProject -ManagedZone $testZone1 } | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Remove-GcdManagedZone -DnsProject $accessErrProject -ManagedZone $testZone1 } | Should Throw "403"
    }

    It "should fail to delete a non-existent ManagedZone in an existent project" {
        { Remove-GcdManagedZone -DnsProject $project -ManagedZone $nonExistManagedZone } | Should Throw "404"
    }

    # Create two zones for testing 
    Add-GcdManagedZone -DnsProject $project -Name $testZone1 -DnsName $dnsName1
    Add-GcdManagedZone -DnsProject $project -Name $testZone2 -DnsName $dnsName2

    It "should delete only the first zone created and output nothing" {
        Remove-GcdManagedZone -DnsProject $project -ManagedZone $testZone1 | Should Be $null

        { Get-GcdManagedZone -DnsProject $project -ManagedZone $testZone1 } | Should Throw "404"
        (Get-GcdManagedZone -DnsProject $project).Count | Should Be 1
        (Get-GcdManagedZone -DnsProject $project).Name | Should Match $testZone2
    }
    
    # Recreate deleted zone for testing 
    Add-GcdManagedZone -DnsProject $project -Name $testZone1 -DnsName $dnsName1

    It "should delete all (2) zones in project using pipeline input" {
        (Get-GcdManagedZone -DnsProject $project | Remove-GcdManagedZone -DnsProject $project) | Should Be $null

        Get-GcdManagedZone -DnsProject $project | Should Be $null
    }

    # Create a 2 zones, 1 non-empty, for testing
    Add-GcdManagedZone -DnsProject $project -Name $testZone1 -DnsName $dnsName1
    Add-GcdManagedZone -DnsProject $project -Name $testZone2 -DnsName $dnsName2
    Add-GcdChange -DnsProject $project -Zone $testZone1 -Add $testRrsetA,$testRrsetCNAME

    It "should fail to delete a non-empty ManagedZone when -Force is not specified" {
        { Remove-GcdManagedZone -DnsProject $project -ManagedZone $testZone1 } | Should Throw "400"
    }

    It "should delete a non-empty zone when -Force is specified" {
        Remove-GcdManagedZone -DnsProject $project -ManagedZone $testZone1 -Force | Should Be $null

        { Get-GcdManagedZone -DnsProject $project -ManagedZone $testZone1 } | Should Throw "404"
        (Get-GcdManagedZone -DnsProject $project).Count | Should Be 1
        (Get-GcdManagedZone -DnsProject $project).Name | Should Match $testZone2
    }
}

