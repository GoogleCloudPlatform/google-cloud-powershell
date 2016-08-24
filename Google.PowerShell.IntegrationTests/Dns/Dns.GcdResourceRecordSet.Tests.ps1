. $PSScriptRoot\..\Dns\GcdCmdlets.ps1

Describe "Get-GcdResourceRecordSet" {
    BeforeAll {
        Remove-AllManagedZone($project)
    }
    AfterAll {
        Remove-AllManagedZone($project)
    }

    It "should fail to return ResourceRecordSets of non-existent project" {
        { Get-GcdResourceRecordSet -Project $nonExistProject $testZone1 } | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdResourceRecordSet -Project $accessErrProject $testZone1 } | Should Throw "403"
    }

    It "should fail to return ResourceRecordSets of non-existent ManagedZone of existing project" {
        { Get-GcdResourceRecordSet -Project $project $nonExistManagedZone } | Should Throw "404"
    }

    # Create zone for testing 
    gcloud dns managed-zones create --dns-name=$dnsName1 --description=$testDescrip1 $testZone1 --project=$project

    # Add a new A-type record and a new AAAA type record to the test zone
    Add-GcdChange -Project $project -Zone $testZone1 -Add $testRrsetA,$testRrsetAAAA

    It "should work and retrieve 4 ResourceRecordSets (2 from creation, 2 added)" {
        $rrsets = Get-GcdResourceRecordSet -Project $project $testZone1
        $rrsets.Count | Should Be 4

        # The object type, Kind, and Name should be the same for all ResourceRecordSets
        ($rrsets | Get-Member).TypeName | Should Match $rrsetType
        $rrsets.Kind | Should Match $rrsetKind
        $rrsets.Name | Should Match $dnsName

        $rrsets.Type -contains "SOA" | Should Match $true
        $rrsets.Type -contains "NS" | Should Match $true
        $rrsets.Type -contains "A" | Should Match $true
        $rrsets.Type -contains "AAAA" | Should Match $true

        (($rrsets.Rrdatas -contains $rrdataA1) -and ($rrsets.Rrdatas -contains $rrdataAAAA)) | Should Match $true
    }

    It "should work and retrieve only the NS and AAAA type records" {
        $rrsets = Get-GcdResourceRecordSet -Project $project $testZone1 "NS","AAAA"
        $rrsets.Count | Should Be 2

        ($rrsets | Get-Member).TypeName | Should Match $rrsetType
        $rrsets.Kind | Should Match $rrsetKind
        $rrsets.Name | Should Match $dnsName

        $rrsets.Type -contains "NS" | Should Match $true
        $rrsets.Type -contains "AAAA" | Should Match $true
        $rrsets.Type -contains "SOA" | Should Match $false
        $rrsets.Type -contains "A" | Should Match $false
    }
}

Describe "New-GcdResourceRecordSet" {
    
    It "should fail to create a new ResourceRecordSet with an invalid record type" {
        { New-GcdResourceRecordSet $dnsName1 $rrdataA1 "Invalid" } | Should Throw "ValidateSet"
    }

    It "should work and create a new ResourceRecordSet with the specified properties and default ttl (A type record)" {
        $rrset = New-GcdResourceRecordSet $dnsName1 $rrdataA1,$rrdataA2 "A"
        $rrset.Count | Should Be 1

        $rrset.GetType().FullName | Should Match $rrsetType
        $rrset.Kind | Should Match $rrsetKind
        $rrset.Name | Should Match $dnsName1
        $rrset.Rrdatas.Count | Should Be 2
        $rrset.Rrdatas[0] | Should Match $rrdataA1
        $rrset.Rrdatas[1] | Should Match $rrdataA2
        $rrset.Ttl | Should Match $ttlDefault
        $rrset.Type | Should Match "A"
    }

    It "should work and create a new ResourceRecordSet with the specified properties and custom ttl (AAAA type record)" {
        $rrset = New-GcdResourceRecordSet $dnsName1_1 $rrdataAAAA "AAAA" $ttl1
        $rrset.Count | Should Be 1

        $rrset.GetType().FullName | Should Match $rrsetType
        $rrset.Kind | Should Match $rrsetKind
        $rrset.Name | Should Match $dnsName1_1
        $rrset.Rrdatas | Should Match $rrdataAAAA
        $rrset.Ttl | Should Match $ttl1
        $rrset.Type | Should Match "AAAA"
    }

    It "should work and create ResourceRecordSets of other types (CNAME and TXT)" {
        $rrsetCNAME = New-GcdResourceRecordSet $dnsName1_2 $rrdataCNAME1_2 "CNAME" $ttl1
        $rrsetTXT = New-GcdResourceRecordSet $dnsName1 $rrdataTXT1 "TXT" $ttl1

        $rrsetCNAME.Type | Should Match "CNAME"
        $rrsetTXT.Type | Should Match "TXT"
    }
}
