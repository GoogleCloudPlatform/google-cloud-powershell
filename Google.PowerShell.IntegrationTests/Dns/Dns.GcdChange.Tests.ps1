﻿. $PSScriptRoot\..\Dns\GcdCmdlets.ps1

Describe "Get-GcdChange" {
    BeforeAll {
        Remove-AllManagedZone($project)
    }
    AfterAll {
        Remove-AllManagedZone($project)
    }

    It "should fail to return changes of non-existent project" {
        { Get-GcdChange -DnsProject $nonExistProject -Zone $testZone1 } | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdChange -DnsProject $accessErrProject -Zone $testZone1 } | Should Throw "403"
    }

    It "should fail to return changes of non-existent ManagedZones of existing project" {
        { Get-GcdChange -DnsProject $project -Zone $nonExistManagedZone } | Should Throw "404"
    }

    # Create zone for testing 
    gcloud dns managed-zones create --dns-name=$dnsName1 --description="testing zone, 1" $testZone1 --project=$project
    
    # Make 2 new changes for testing by adding and immediately deleting an A-type record to the test zone
    Add-GcdChange -DnsProject $project -Zone $testZone1 -Add $testRrset1
    Add-GcdChange -DnsProject $project -Zone $testZone1 -Remove $testRrset1

    It "should work and retrieve 3 changes (including A-type record addition & deletion)" {
        $changes = Get-GcdChange -DnsProject $project -Zone $testZone1

        $changes.Count | Should Be 3

        # The object type, Kind, Status, and names of Additions/Deletions should be the same for all changes
        ($changes | Get-Member).TypeName | Should Match $changeType
        $changes.Kind | Should Match "dns#change"
        $changes.Status | Should Match "done"
        $changes.Additions.Name | Should Match $dnsName1
        $changes.Deletions.Name | Should Match $dnsName1

        $changes.Additions.Type -contains "A" | Should Match $true
        $changes.Deletions.Type -contains "A" | Should Match $true

        $changes[0].Id | Should Be 2
        $changes[1].Id | Should Be 1
        $changes[2].Id | Should Be 0
    }

    It "should work and retrieve the first change from creation by Id" {
        $changes = Get-GcdChange -DnsProject $project -Zone $testZone1 -ChangeId "0"
        $changes.Count | Should Be 1
        $changes.GetType().FullName | Should Match $changeType
        $changes.Id | Should Be 0
        $changes.Kind | Should Match $changeKind
        $changes.Status | Should Match "done"
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
    gcloud dns managed-zones create --dns-name=$dnsName1 --description=$testDescrip1 $testZone1 --project=$project
    gcloud dns managed-zones create --dns-name=$dnsName1 --description=$testDescrip2 $testZone2 --project=$project
    
    # Make a new change for testing by adding an A-type record to test zone 2
    gcloud dns record-sets transaction start --zone=$testZone2 --project=$project
    gcloud dns record-sets transaction add --name=$dnsName1 --type=A --ttl=$ttl1 $rrdata1 --zone=$testZone2 --project=$project
    gcloud dns record-sets transaction execute --zone=$testZone2 --project=$project

    # Copy Change request for later use
    $copyChange = (Get-GcdChange -DnsProject $project -Zone $testZone2)[0]
    $copyChange.Additions.Remove(($copyChange.Additions | Where-Object {$_.Type -ne "A"}))
    $copyChange.Deletions = $null

    It "should fail to add a Change to a non-existent project" {
        { Add-GcdChange -DnsProject $nonExistProject -Zone $testZone1 -ChangeRequest $copyChange } | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Add-GcdChange -DnsProject $accessErrProject -Zone $testZone1 -ChangeRequest $copyChange } | Should Throw "403"
    }

    It "should fail to add a Change to a non-existent ManagedZone of an existing project" {
        { Add-GcdChange -DnsProject $project -Zone $nonExistManagedZone -ChangeRequest $copyChange } | Should Throw "404"
    }

    It "should fail to add a Change with only null/empty values for Add/Remove" {
        { Add-GcdChange -DnsProject $project -Zone $testZone1 -Add $null -Remove $null } | Should Throw $Err_NeedChangeContent
        { Add-GcdChange -DnsProject $project -Zone $testZone1 -Add $null -Remove @() } | Should Throw $Err_NeedChangeContent
        { Add-GcdChange -DnsProject $project -Zone $testZone1 -Add @() -Remove @() } | Should Throw $Err_NeedChangeContent
    }

    It "should work and add 1 Change from Change Request (another A-type record addition)" {
        $initChanges = Get-GcdChange -DnsProject $project -Zone $testZone1
        $newChange = Add-GcdChange -DnsProject $project -Zone $testZone1 -ChangeRequest $copyChange
        $allChanges = Get-GcdChange -DnsProject $project -Zone $testZone1

        Compare-Object $newChange ($allChanges[$allChanges.Length - 1]) | Should Match $null
        $allChanges.Length | Should Be ($initChanges.Length + 1)

        $newChange.GetType().FullName | Should Match $changeType
        $newChange.Additions | Should Match $copyChange.Additions
        $newChange.Deletions | Should Match $copyChange.Deletions
        $newChange.Kind | Should Match $changeKind
    }

    # Make a new change for testing by adding an A-type record to test zone 1
    gcloud dns record-sets transaction start --zone=$testZone1 --project=$project
    gcloud dns record-sets transaction add --name=$dnsName1_1 --type=A --ttl=$ttl1 $rrdata1 --zone=$testZone1 --project=$project
    gcloud dns record-sets transaction execute --zone=$testZone1 --project=$project

    # Create ResourceRecordSets that can be added and removed
    $rmRrset1 = New-GcdResourceRecordSet -Name $dnsName1 -Rrdata $rrdata1 -Type "A" -Ttl $ttl1
    $rmRrset2 = New-GcdResourceRecordSet -Name $dnsName1_1 -Rrdata $rrdata1 -Type "A" -Ttl $ttl1
    $addRrset1 = New-GcdResourceRecordSet -Name $dnsName1_2 -Rrdata $rrdata1 -Type "A" -Ttl $ttl1
    $addRrset2 = New-GcdResourceRecordSet -Name $dnsName1_3 -Rrdata $rrdata1 -Type "A" -Ttl $ttl1

    It "should work and add 1 Change with Add/Remove arguments (2 A-type record additions, 2 A-type record deletions)" {
        $initChanges = Get-GcdChange -DnsProject $project -Zone $testZone1
        
        $newChange = Add-GcdChange -DnsProject $project -Zone $testZone1 -Add $addRrset1,$addRrset2 -Remove $rmRrset1,$rmRrset2
        $allChanges = Get-GcdChange -DnsProject $project -Zone $testZone1

        Compare-Object $newChange ($allChanges[$allChanges.Length - 1]) | Should Match $null
        $allChanges.Length | Should Be ($initChanges.Length + 1)

        $newChange.GetType().FullName | Should Match $changeType
        $newChange.Additions.Count | Should Be 2
        $newChange.Deletions.Count | Should Be 2
        (($newChange.Additions.Name -contains $dnsName1_2) -and ($newChange.Additions.Name -contains $dnsName1_3)) | Should Match $true
        (($newChange.Deletions.Name -contains $dnsName1) -and ($newChange.Deletions.Name -contains $dnsName1_1)) | Should Match $true
        $newChange.Kind | Should Match $changeKind
    }

    # Delete the previously added records to empty the ManagedZones
    Add-GcdChange -DnsProject $project -Zone $testZone1 -Remove $addRrset1,$addRrset2
    Add-GcdChange -DnsProject $project -Zone $testZone2 -Remove $copyChange.Additions

    It "should fail to add Change that tries to remove a non-existent ResourceRecord" {
        { Add-GcdChange -DnsProject $project -Zone $testZone1 -Remove $rmRrset1 } | Should Throw "404"
    }
}
