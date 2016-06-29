. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"
$changeType = "Google.Apis.Dns.v1.Data.Change"

$testZone1 = "test1"
$testZone2 = "test2"
$dnsName1 = "gcloudexample1.com."
$dnsName1_1 = "a.gcloudexample1.com."
$dnsName1_2 = "b.gcloudexample1.com."
$dnsName1_3 = "c.gcloudexample1.com."
$rrdata1 = "7.5.7.8"
$ttl1 = 300

Describe "Get-GcdChange" {

    It "should fail to return changes of non-existent project" {
        { Get-GcdChange -Project "project-no-exist" -ManagedZone "zone-no-exist"} | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdChange -Project "asdf" -ManagedZone "zone1"} | Should Throw "403"
    }

    # Delete all existing zones (using Get-GcdManagedZone cmdlet)
    $preExistingZones = Get-GcdManagedZone -Project $project

    ForEach ($zoneObject in $preExistingZones) {
        gcloud dns managed-zones delete $zoneObject.Name --project=$project
    }

    It "should fail to return changes of non-existent managed zones of existing project" {
        { Get-GcdChange -Project $project -ManagedZone "managedZone-no-exist" } | Should Throw "404"
    }

    # Create zone for testing 
    gcloud dns managed-zones create --dns-name=$dnsName1 --description="testing zone, 1" $testZone1 --project=$project
    
    # Make a new change for testing by adding an A-type record to the test zone
    gcloud dns record-sets transaction start --zone=$testZone1 --project=$project
    gcloud dns record-sets transaction add --name=$dnsName1 --type=A --ttl=$ttl1 $rrdata1 --zone=$testZone1 --project=$project
    gcloud dns record-sets transaction execute --zone=$testZone1 --project=$project

    # Delete the previously added record to empty the managed zone (remove all non-NS/SOA records) and allow zone deletion
    gcloud dns record-sets transaction start --zone=$testZone1 --project=$project
    gcloud dns record-sets transaction remove --name=$dnsName1 --type=A --ttl=$ttl1 $rrdata1 --zone=$testZone1 --project=$project
    gcloud dns record-sets transaction execute --zone=$testZone1 --project=$project

    It "should work and retrieve 3 changes (including A-type record addition & deletion)" {
        $changes = Get-GcdChange -Project $project -ManagedZone $testZone1

        $changes.Count | Should Be 3

        # The object type, Kind, Status, and names of Additions/Deletions should be the same for all changes
        ($changes | Get-Member).TypeName | Should Match $changeType
        $changes.Kind | Should Match "dns#change"
        $changes.Status | Should Match "done"
        $changes.Additions.Name | Should Match $dnsName1
        $changes.Deletions.Name | Should Match $dnsName1

        $changes.Additions.Type -Contains "A" | Should Match $true
        $changes.Deletions.Type -Contains "A" | Should Match $true

        $changes[0].Id | Should Be 2
        $changes[1].Id | Should Be 1
        $changes[2].Id | Should Be 0
    }

    It "should work and retrieve the first change from creation by Id" {
        $changes = Get-GcdChange -Project $project -ManagedZone $testZone1 -ChangeId "0"
        $changes.Count | Should Be 1
        $changes.GetType().FullName | Should Match $changeType
        $changes.Id | Should Be 0
        $changes.Kind | Should Match "dns#change"
        $changes.Status | Should Match "done"
        $changes.Additions.Name | Should Match $dnsName1
        $changes.Additions.Type -Contains "A" | Should Match $false
        $changes.Deletions.Type -Contains "A" | Should Match $false
    }

    # Delete now-empty test zone 
    gcloud dns managed-zones delete $testZone1 --project=$project
}

Describe "Add-GcdChange" {

    # Delete all existing zones (using Get-GcdManagedZone cmdlet)
    $preExistingZones = Get-GcdManagedZone -Project $project

    ForEach ($zoneObject in $preExistingZones) {
        gcloud dns managed-zones delete $zoneObject.Name --project=$project
    }

    # Create 2 zones for testing
    gcloud dns managed-zones create --dns-name=$dnsName1 --description="testing zone, 1" $testZone1 --project=$project
    gcloud dns managed-zones create --dns-name=$dnsName1 --description="testing zone, 1" $testZone2 --project=$project
    
    # Make a new change for testing by adding an A-type record to test zone 2
    gcloud dns record-sets transaction start --zone=$testZone2 --project=$project
    gcloud dns record-sets transaction add --name=$dnsName1 --type=A --ttl=$ttl1 $rrdata1 --zone=$testZone2 --project=$project
    gcloud dns record-sets transaction execute --zone=$testZone2 --project=$project

    # Copy Change request for later use
    $copyChange = (Get-GcdChange -Project $project -ManagedZone $testZone2)[0]
    $copyChange.Additions.Remove(($copyChange.Additions | Where-Object {$_.Type -ne "A"}))
    $copyChange.Deletions = $null

    It "should fail to add a Change to a non-existent project" {
        { Add-GcdChange -Project "project-no-exist" -ManagedZone "zone-no-exist" -ChangeRequest $copyChange} | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Add-GcdChange -Project "asdf" -ManagedZone "zone1" -ChangeRequest $copyChange} | Should Throw "403"
    }

    It "should fail to add a Change to a non-existent ManagedZone of an existing project" {
        { Add-GcdChange -Project $project -ManagedZone "managedZone-no-exist" -ChangeRequest $copyChange} | Should Throw "404"
    }

    It "should fail to add a null Change with no Change request or Add/Remove arguments" {
        { Add-GcdChange -Project $project -ManagedZone $testZone1 } | Should Throw "Must specify at least 1 Add or Remove, or provide a Change request, to execute."
    }

    It "should work and add 1 Change from Change Request (another A-type record addition)" {
        $initChanges = Get-GcdChange -Project $project -ManagedZone $testZone1
        $newChange = Add-GcdChange -Project $project -ManagedZone $testZone1 -ChangeRequest $copyChange
        $allChanges = Get-GcdChange -Project $project -ManagedZone $testZone1

        Compare-Object $newChange ($allChanges[$allChanges.Length - 1]) | Should Match $null
        $allChanges.Length | Should Be ($initChanges.Length + 1)

        $newChange.GetType().FullName | Should Match $changeType
        $newChange.Additions | Should Match $copyChange.Additions
        $newChange.Deletions | Should Match $copyChange.Deletions
        $newChange.Kind | Should Match "dns#change"
    }

    # Make a new change for testing by adding an A-type record to test zone 1
    gcloud dns record-sets transaction start --zone=$testZone1 --project=$project
    gcloud dns record-sets transaction add --name=$dnsName1_1 --type=A --ttl=$ttl1 $rrdata1 --zone=$testZone1 --project=$project
    gcloud dns record-sets transaction execute --zone=$testZone1 --project=$project

    # Create ResourceRecordSets that can be added and removed.
    $rmRrset1 = New-GcdResourceRecordSet -Name $dnsName1 -Rrdata $rrdata1 -Type "A" -Ttl $ttl1
    $rmRrset2 = New-GcdResourceRecordSet -Name $dnsName1_1 -Rrdata $rrdata1 -Type "A" -Ttl $ttl1
    $addRrset1 = New-GcdResourceRecordSet -Name $dnsName1_2 -Rrdata $rrdata1 -Type "A" -Ttl $ttl1
    $addRrset2 = New-GcdResourceRecordSet -Name $dnsName1_3 -Rrdata $rrdata1 -Type "A" -Ttl $ttl1

    It "should work and add 1 Change with Add/Remove arguments (2 A-type record additions, 2 A-type record deletions)" {
        $initChanges = Get-GcdChange -Project $project -ManagedZone $testZone1
        
        $newChange = Add-GcdChange -Project $project -ManagedZone $testZone1 -Add $addRrset1,$addRrset2 -Remove $rmRrset1,$rmRrset2
        $allChanges = Get-GcdChange -Project $project -ManagedZone $testZone1

        Compare-Object $newChange ($allChanges[$allChanges.Length - 1]) | Should Match $null
        $allChanges.Length | Should Be ($initChanges.Length + 1)

        $newChange.GetType().FullName | Should Match $changeType
        $newChange.Additions.Count | Should Be 2
        $newChange.Deletions.Count | Should Be 2
        (($newChange.Additions.Name -Contains $dnsName1_2) -and ($newChange.Additions.Name -Contains $dnsName1_3)) | Should Match $true
        (($newChange.Deletions.Name -Contains $dnsName1) -and ($newChange.Deletions.Name -Contains $dnsName1_1)) | Should Match $true
        $newChange.Kind | Should Match "dns#change"
    }

    # Delete the previously added records to empty the managed zones and allow zone deletion
    Add-GcdChange -Project $project -Zone $testZone1 -Remove $addRrset1,$addRrset2
    Add-GcdChange -Project $project -Zone $testZone2 -Remove $copyChange.Additions

    # Delete now-empty test zones 
    gcloud dns managed-zones delete $testZone1 --project=$project
    gcloud dns managed-zones delete $testZone2 --project=$project
}

