. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"
$changeType = "Google.Apis.Dns.v1.Data.Change"

Describe "Get-GcdChange" {

    It "should fail to return changes of non-existent project" {
        { Get-GcdChange -Project "project-no-exist" -ManagedZone "zone-no-exist"} | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdManagedZone -Project "asdf" -ManagedZone "zone1"} | Should Throw "403"
    }

    # Delete all existing zones
    $preExistingZones = gcloud dns managed-zones list --project $project

    if ($preExistingZones.Count -gt 0) {
        $preExistingZones = $preExistingZones[1..($preExistingZones.length-1)]

        ForEach ($zoneDescrip in $preExistingZones) {
            $zoneName = $zoneDescrip.Split(" ")[0]
            gcloud dns managed-zones delete $zoneName
        }
    }

    It "should fail to return changes of non-existent managed zones of existing project" {
        { Get-GcdManagedZone -Project $project -ManagedZone "managedZone-no-exist" } | Should Throw "404"
    }

    # Create zone for testing 
    $testZone = "test1"
    $dnsName = "gcloudexample.com."
    gcloud dns managed-zones create --dns-name=$dnsName --description="testing zone, 1" $testZone

    It "should list exactly 1 change from creation" {
        (Get-GcdChange -Project $project -ManagedZone $testZone).Count -eq 1 | Should Be $true
    }

    It "should work and retrieve the 1 change from creation (including no non-default A-type records)" {
        $changes = Get-GcdChange -Project $project -ManagedZone $testZone
        $changes.GetType().FullName | Should Match $changeType
        $changes.Id -eq 0 | Should Be $true
        $changes.Kind | Should Match "dns#change"
        $changes.Status | Should Match "done"
        $changes.Additions.Name | Should Match $dnsName
        $changes.Additions.Type -contains "A" | Should Match $false
        $changes.Deletions.Type -contains "A" | Should Match $false
    }
    
    # Make a new change for testing by adding an A-type record to the test zone
    gcloud dns record-sets transaction start --zone=$testZone
    gcloud dns record-sets transaction add --name=$dnsName --type=A --ttl=300 “7.5.7.8” --zone=$testZone
    gcloud dns record-sets transaction execute --zone=$testZone

    It "should list exactly 2 changes" {
        (Get-GcdChange -Project $project -ManagedZone $testZone).Count -eq 2 | Should Be $true
    }

    It "should work and retrieve 2 changes (including an A-type record creation)" {
        $changes = Get-GcdChange -Project $project -ManagedZone $testZone

        # The type, Kind, Status, and names of Additions should be the same for all changes
        $changes[0].GetType().FullName | Should Match $changeType
        $changes[1].GetType().FullName | Should Match $changeType
        $changes.Kind | Should Match "dns#change"
        $changes.Status | Should Match "done"
        $changes.Additions.Name | Should Match $dnsName

        $changes.Additions.Type -contains "A" | Should Match $true
        $changes.Deletions.Type -contains "A" | Should Match $false

        $changes[0].Id -eq 1 | Should Be $true
        $changes[1].Id -eq 0 | Should Be $true
    }

    # Delete the previously added record to empty the managed zone (remove all non-NS/SOA records) and allow zone deletion
    gcloud dns record-sets transaction start --zone=$testZone
    gcloud dns record-sets transaction remove --name=$dnsName --type=A --ttl=300 “7.5.7.8” --zone=$testZone
    gcloud dns record-sets transaction execute --zone=$testZone

    It "should list exactly 3 changes" {
        (Get-GcdChange -Project $project -ManagedZone $testZone).Count -eq 3 | Should Be $true
    }

    It "should work and retrieve 3 changes (including an A-type record deletion)" {
        $changes = Get-GcdChange -Project $project -ManagedZone $testZone

        # The type, Kind, Status, and names of Additions/Deletions should be the same for all changes
        $changes[0].GetType().FullName | Should Match $changeType
        $changes[1].GetType().FullName | Should Match $changeType
        $changes[2].GetType().FullName | Should Match $changeType
        $changes.Kind | Should Match "dns#change"
        $changes.Status | Should Match "done"
        $changes.Additions.Name | Should Match $dnsName
        $changes.Deletions.Name | Should Match $dnsName

        $changes.Additions.Type -contains "A" | Should Match $true
        $changes.Deletions.Type -contains "A" | Should Match $true

        $changes[0].Id -eq 2 | Should Be $true
        $changes[1].Id -eq 1 | Should Be $true
        $changes[2].Id -eq 0 | Should Be $true
    }

    # Delete now-empty test zone
    gcloud dns managed-zones delete $testZone
}
