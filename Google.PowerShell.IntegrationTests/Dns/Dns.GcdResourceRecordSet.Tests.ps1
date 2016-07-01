. $PSScriptRoot\..\Dns\GcdCmdlets.ps1

Describe "Get-GcdResourceRecordSet" {
    BeforeAll {
        Remove-AllManagedZone($project)
    }
    AfterAll {
        Remove-AllManagedZone($project)
    }

    It "should fail to return ResourceRecordSets of non-existent project" {
        { Get-GcdResourceRecordSet -DnsProject $nonExistProject -Zone $testZone1 } | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdResourceRecordSet -DnsProject $accessErrProject -Zone $testZone1 } | Should Throw "403"
    }

    It "should fail to return ResourceRecordSets of non-existent ManagedZone of existing project" {
        { Get-GcdResourceRecordSet -DnsProject $project -Zone $nonExistManagedZone } | Should Throw "404"
    }

    # Create zone for testing 
    gcloud dns managed-zones create --dns-name=$dnsName1 --description="testing zone, 1" $testZone1 --project=$project

    # Add a new A-type record to the test zone
    Add-GcdChange -DnsProject $project -Zone $testZone1 -Add $testRrset1

    It "should work and retrieve 3 ResourceRecordSets (2 from creation, 1 added)" {
        $rrsets = Get-GcdResourceRecordSet -DnsProject $project -Zone $testZone1
        $rrsets.Count | Should Be 3

        # The object type, Kind, and Name should be the same for all ResourceRecordSets
        ($rrsets | Get-Member).TypeName | Should Match $rrsetType
        $rrsets.Kind | Should Match $rrsetKind
        $rrsets.Name | Should Match $dnsName

        $rrsets.Type -contains "SOA" | Should Match $true
        $rrsets.Type -contains "NS" | Should Match $true
        $rrsets.Type -contains "A" | Should Match $true

        $rrsets.Rrdatas -contains $rrdata1 | Should Match $true
    }

    # Delete the previously added record to empty the ManagedZone (remove all non-NS/SOA records)
    Add-GcdChange -DnsProject $project -Zone $testZone1 -Remove $testRrset1

    It "should work and retrieve only the original 2 ResourceRecordSets" {
        $rrsets = Get-GcdResourceRecordSet -DnsProject $project -Zone $testZone1
        $rrsets.Count | Should Be 2

        # The object type, Kind, and Name should be the same for all ResourceRecordSets
        ($rrsets | Get-Member).TypeName | Should Match $rrsetType
        $rrsets.Kind | Should Match $rrsetKind
        $rrsets.Name | Should Match $dnsName


        $rrsets.Type -contains "SOA" | Should Match $true
        $rrsets.Type -contains "NS" | Should Match $true
        $rrsets.Type -contains "A" | Should Match $false
    }
}

Describe "New-GcdResourceRecordSet" {
    
    It "should fail to create a new ResourceRecordSet with an invalid record type" {
        { New-GcdResourceRecordSet -Name $dnsName1_1 -Rrdata $rrdata2 -Type "Invalid" } | Should Throw "ValidateSet"
    }

    It "should work and create a new ResourceRecordSet with the specified properties and default ttl (A type record)" {
        $rrset = New-GcdResourceRecordSet -Name $dnsName1 -Rrdata $rrdata1,$rrdata1_1 -Type "A"
        $rrset.Count | Should Be 1

        $rrset.GetType().FullName | Should Match $rrsetType
        $rrset.Kind | Should Match $rrsetKind
        $rrset.Name | Should Match $dnsName1
        $rrset.Rrdatas.Count | Should Be 2
        $rrset.Rrdatas[0] | Should Match $rrdata1
        $rrset.Rrdatas[1] | Should Match $rrdata1_1
        $rrset.Ttl | Should Match $ttlDefault
        $rrset.Type | Should Match "A"
    }

    It "should work and create a new ResourceRecordSet with the specified properties and custom ttl (AAAA type record)" {
        $rrset = New-GcdResourceRecordSet -Name $dnsName1_1 -Rrdata $rrdata2 -Type "AAAA" -Ttl $ttl1
        $rrset.Count | Should Be 1

        $rrset.GetType().FullName | Should Match $rrsetType
        $rrset.Kind | Should Match $rrsetKind
        $rrset.Name | Should Match $dnsName1_1
        $rrset.Rrdatas | Should Match $rrdata2
        $rrset.Ttl | Should Match $ttl1
        $rrset.Type | Should Match "AAAA"
    }
}
