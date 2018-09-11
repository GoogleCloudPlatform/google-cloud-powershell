. $PSScriptRoot\..\Dns\GcdCmdlets.ps1
Install-GcloudCmdlets
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcdChange" {
    BeforeAll {
        Remove-AllManagedZone($project)
    }
    AfterAll {
        Remove-AllManagedZone($project)
    }

    It "should fail to return changes of non-existent project" {
        { Get-GcdChange -Project $nonExistProject -Zone $testZone1 } | Should Throw "403"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdChange -Project $accessErrProject -Zone $testZone1 } | Should Throw "403"
    }

    It "should fail to return changes of non-existent ManagedZones of existing project" {
        { Get-GcdChange -Project $project -Zone $nonExistManagedZone } | Should Throw "404"
    }

    # Create zone for testing 
    gcloud dns managed-zones create --dns-name=$dnsName1 --description="testing zone, 1" $testZone1 --project=$project 2>$null
    
    # Make 2 new changes for testing by adding and immediately deleting an CNAME-type record to the test zone
    Add-GcdChange -Project $project -Zone $testZone1 -Add $testRrsetA
    Add-GcdChange -Project $project -Zone $testZone1 -Remove $testRrsetA

    It "should work and retrieve 3 changes (including A-type record addition & deletion)" {
        $changes = Get-GcdChange -Project $project $testZone1

        $changes.Count | Should Be 3

        # The object type, Kind, Status, and names of Additions/Deletions should be the same for all changes
        ($changes | Get-Member).TypeName | Should Match $changeType
        $changes.Kind | Should Match $changeKind
        $changes.Additions.Name | Should Match $dnsName1
        $changes.Deletions.Name | Should Match $dnsName1

        $changes.Additions.Type -contains "A" | Should Match $true
        $changes.Deletions.Type -contains "A" | Should Match $true

        $changes[0].Id | Should Be 2
        $changes[1].Id | Should Be 1
        $changes[2].Id | Should Be 0
    }

    It "should work and retrieve the first change from creation by Id" {
        $changes = Get-GcdChange -Project $project $testZone1 "0"
        $changes.Count | Should Be 1
        $changes.GetType().FullName | Should Match $changeType
        $changes.Id | Should Be 0
        $changes.Kind | Should Match $changeKind
        $changes.Additions.Name | Should Match $dnsName1
        $changes.Additions.Type -contains "A" | Should Match $false
        $changes.Deletions.Type -contains "A" | Should Match $false
    }
}

Describe "Add-GcdChange" {
    BeforeAll {
        Remove-FileIfExists($transactionFile)
        Remove-AllManagedZone($project)
    }
    AfterAll {
       Remove-FileIfExists($transactionFile)
        Remove-AllManagedZone($project)
    }

    # Create 2 zones for testing
    gcloud dns managed-zones create --dns-name=$dnsName1 --description=$testDescrip1 $testZone1 --project=$project 2>$null
    gcloud dns managed-zones create --dns-name=$dnsName1 --description=$testDescrip2 $testZone2 --project=$project 2>$null
    
    # Make a new change for testing by adding a CNAME-type record to test zone 2 (testRrsetCNAME equivalent)
    gcloud dns record-sets transaction start --zone=$testZone2 --project=$project 2>$null
    gcloud dns record-sets transaction add --name=$dnsName1_2 --type=CNAME --ttl=$ttl1 $rrdataCNAME1_2 --zone=$testZone2 --project=$project 2>$null
    gcloud dns record-sets transaction execute --zone=$testZone2 --project=$project 2>$null

    # Copy Change request for later use
    $copyChange = (Get-GcdChange -Project $project -Zone $testZone2)[0]
    $copyChange.Additions.Remove(($copyChange.Additions | Where {$_.Type -ne "CNAME"}))
    $copyChange.Deletions = $null

    It "should fail to add a Change to a non-existent project" {
        { Add-GcdChange -Project $nonExistProject $testZone1 $copyChange } | Should Throw "403"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Add-GcdChange -Project $accessErrProject $testZone1 $copyChange } | Should Throw "403"
    }

    It "should fail to add a Change to a non-existent ManagedZone of an existing project" {
        { Add-GcdChange -Project $project $nonExistManagedZone $copyChange } | Should Throw "404"
    }

    It "should fail to add a Change with only null/empty values for Add/Remove" {
        { Add-GcdChange -Project $project $testZone1 -Add $null -Remove $null } | Should Throw $Err_NeedChangeContent
        { Add-GcdChange -Project $project $testZone1 -Add $null -Remove @() } | Should Throw $Err_NeedChangeContent
        { Add-GcdChange -Project $project $testZone1 -Add @() -Remove @() } | Should Throw $Err_NeedChangeContent
    }

    It "should work and add 1 Change from Change Request (CNAME-type record addition)" {
        $initChanges = Get-GcdChange -Project $project -Zone $testZone1
        $newChange = Add-GcdChange -Project $project $testZone1 $copyChange
        $allChanges = Get-GcdChange -Project $project -Zone $testZone1

        Compare-Object $newChange $allChanges[0] -Property Additions,Deletions,Id,Kind,StartTime | Should Match $null
        $allChanges.Length | Should Be ($initChanges.Length + 1)

        $newChange.GetType().FullName | Should Match $changeType
        $newChange.Additions | Should Match $copyChange.Additions
        $newChange.Deletions | Should Match $copyChange.Deletions
        $newChange.Kind | Should Match $changeKind
    }

    # Make a new change for testing by adding a TXT-type record to test zone 1
    gcloud dns record-sets transaction start --zone=$testZone1 --project=$project 2>$null
    gcloud dns record-sets transaction add --name=$dnsName1 --type=TXT --ttl=$ttl1 $rrdataTXT1 --zone=$testZone1 --project=$project 2>$null
    gcloud dns record-sets transaction execute --zone=$testZone1 --project=$project 2>$null

    # Create ResourceRecordSets that can be added and removed
    $rmRrset1 = $testRrsetCNAME
    $rmRrset2 = $testRrsetTXT1
    $addRrset1 = $testRrsetTXT2
    $addRrset2 = $testRrsetAAAA

    It "should support Add/Remove arguments in same call" {
        $initChanges = Get-GcdChange -Project $project -Zone $testZone1
        
        $newChange = Add-GcdChange -Project $project $testZone1 -Add $addRrset1,$addRrset2 -Remove $rmRrset1,$rmRrset2
        $allChanges = Get-GcdChange -Project $project -Zone $testZone1

        Compare-Object $newChange $allChanges[0] -Property Additions,Deletions,Id,Kind,StartTime | Should Match $null
        $allChanges.Length | Should Be ($initChanges.Length + 1)

        $newChange.GetType().FullName | Should Match $changeType
        $newChange.Additions.Count | Should Be 2
        $newChange.Deletions.Count | Should Be 2
        $newChange.Kind | Should Match $changeKind

        Compare-Object ($newChange.Deletions | Where {$_.Type -eq "CNAME"}) $rmRrset1 -Property Kind,Name,Rrdatas,Ttl,Type | Should Match $null
        Compare-Object ($newChange.Deletions | Where {$_.Type -eq "TXT"}) $rmRrset2 -Property Kind,Name,Rrdatas,Ttl,Type | Should Match $null
        Compare-Object ($newChange.Additions | Where {$_.Type -eq "TXT"}) $addRrset1 -Property Kind,Name,Rrdatas,Ttl,Type | Should Match $null
        Compare-Object ($newChange.Additions | Where {$_.Type -eq "AAAA"}) $addRrset2 -Property Kind,Name,Rrdatas,Ttl,Type | Should Match $null
    }

    # Delete the previously added records to empty the ManagedZones
    Add-GcdChange -Project $project -Zone $testZone1 -Remove $addRrset1,$addRrset2
    Add-GcdChange -Project $project -Zone $testZone2 -Remove $copyChange.Additions

    It "should fail to add Change that tries to remove a non-existent ResourceRecord" {
        { Add-GcdChange -Project $project $testZone1 -Remove $rmRrset1 } | Should Throw "404"
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
