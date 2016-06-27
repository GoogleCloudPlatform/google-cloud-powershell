. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"
$rrsetType = "Google.Apis.Dns.v1.Data.ResourceRecordSet"

Describe "Get-GcdResourceRecordSet" {

    It "should fail to return ResourceRecordSets of non-existent project" {
        { Get-GcdResourceRecordSet -Project "project-no-exist" -ManagedZone "zone-no-exist"} | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdResourceRecordSet -Project "asdf" -ManagedZone "zone1"} | Should Throw "403"
    }

    # Delete all existing zones (using Get-GcdManagedZone cmdlet)
    $preExistingZones = Get-GcdManagedZone -Project $project

    ForEach ($zoneObject in $preExistingZones) {
        gcloud dns managed-zones delete $zoneObject.Name --project=$project
    }

    It "should fail to return ResourceRecordSets of non-existent managed zone of existing project" {
        { Get-GcdResourceRecordSet -Project $project -ManagedZone "managedZone-no-exist" } | Should Throw "404"
    }

    # Create zone for testing 
    $testZone = "test1"
    $dnsName = "gcloudexample.com."
    gcloud dns managed-zones create --dns-name=$dnsName --description="testing zone, 1" $testZone --project=$project

    # Add a new A-type record to the test zone
    gcloud dns record-sets transaction start --zone=$testZone --project=$project
    gcloud dns record-sets transaction add --name=$dnsName --type=A --ttl=300 “7.5.7.8” --zone=$testZone --project=$project
    gcloud dns record-sets transaction execute --zone=$testZone --project=$project

    It "should work and retrieve 3 ResourceRecordSets (2 from creation, 1 added)" {
        $rrsets = Get-GcdResourceRecordSet -Project $project -ManagedZone $testZone
        $rrsets.Count | Should Be 3

        # The object type, Kind, and Name should be the same for all ResourceRecordSets
        ($rrsets | Get-Member).TypeName | Should Match $rrsetType
        $rrsets.Kind | Should Match "dns#resourceRecordSet"
        $rrsets.Name | Should Match $dnsName

        $rrsets.Type -contains "SOA" | Should Match $true
        $rrsets.Type -contains "NS" | Should Match $true
        $rrsets.Type -contains "A" | Should Match $true

        $rrsets.Rrdatas -contains "7.5.7.8"| Should Match $true
    }

    # Delete the previously added record to empty the managed zone (remove all non-NS/SOA records) and allow zone deletion
    gcloud dns record-sets transaction start --zone=$testZone --project=$project
    gcloud dns record-sets transaction remove --name=$dnsName --type=A --ttl=300 “7.5.7.8” --zone=$testZone --project=$project
    gcloud dns record-sets transaction execute --zone=$testZone --project=$project

    It "should work and retrieve only the original 2 ResourceRecordSets" {
        $rrsets = Get-GcdResourceRecordSet -Project $project -ManagedZone $testZone
        $rrsets.Count | Should Be 2

        # The object type, Kind, and Name should be the same for all ResourceRecordSets
        ($rrsets | Get-Member).TypeName | Should Match $rrsetType
        $rrsets.Kind | Should Match "dns#resourceRecordSet"
        $rrsets.Name | Should Match $dnsName

        $rrsets.Type -contains "SOA" | Should Match $true
        $rrsets.Type -contains "NS" | Should Match $true
        $rrsets.Type -contains "A" | Should Match $false
    }

    # Delete now-empty test zone
    gcloud dns managed-zones delete $testZone --project=$project
}
