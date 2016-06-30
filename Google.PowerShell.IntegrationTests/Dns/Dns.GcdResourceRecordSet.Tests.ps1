. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"
$rrsetType = "Google.Apis.Dns.v1.Data.ResourceRecordSet"

$testZone1 = "test1"
$dnsName1 = "gcloudexample1.com."
$rrdata1 = "7.5.7.8"
$ttl1 = 300
$ttl_default = 3600

Describe "Get-GcdResourceRecordSet" {

    It "should fail to return ResourceRecordSets of non-existent project" {
        { Get-GcdResourceRecordSet -DnsProject "project-no-exist" -Zone "zone-no-exist"} | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdResourceRecordSet -DnsProject "asdf" -Zone "zone1"} | Should Throw "403"
    }

    # Delete all existing zones (using Get-GcdManagedZone cmdlet)
    $preExistingZones = Get-GcdManagedZone -Project $project

    ForEach ($zoneObject in $preExistingZones) {
        gcloud dns managed-zones delete $zoneObject.Name --project=$project
    }

    It "should fail to return ResourceRecordSets of non-existent managed zone of existing project" {
        { Get-GcdResourceRecordSet -DnsProject $project -Zone "managedZone-no-exist" } | Should Throw "404"
    }

    # Create zone for testing 
    gcloud dns managed-zones create --dns-name=$dnsName1 --description="testing zone, 1" $testZone1 --project=$project

    # Add a new A-type record to the test zone
    gcloud dns record-sets transaction start --zone=$testZone1 --project=$project
    gcloud dns record-sets transaction add --name=$dnsName1 --type=A --ttl=300 “7.5.7.8” --zone=$testZone1 --project=$project
    gcloud dns record-sets transaction execute --zone=$testZone1 --project=$project

    It "should work and retrieve 3 ResourceRecordSets (2 from creation, 1 added)" {
        $rrsets = Get-GcdResourceRecordSet -DnsProject $project -Zone $testZone1
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
    gcloud dns record-sets transaction start --zone=$testZone1 --project=$project
    gcloud dns record-sets transaction remove --name=$dnsName1 --type=A --ttl=$ttl1 $rrdata1 --zone=$testZone1 --project=$project
    gcloud dns record-sets transaction execute --zone=$testZone1 --project=$project

    It "should work and retrieve only the original 2 ResourceRecordSets" {
        $rrsets = Get-GcdResourceRecordSet -DnsProject $project -Zone $testZone1
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
    gcloud dns managed-zones delete $testZone1 --project=$project
}

Describe "New-GcdResourceRecordSet" {
    
    It "should work and create a new ResourceRecordSet with the specified properties and default ttl" {
        $rrset = New-GcdResourceRecordSet -Name $dnsName1 -Rrdata $rrdata1 -Type "A"
        $rrset.Count | Should Be 1

        $rrset.GetType().FullName | Should Match $rrsetType
        $rrset.Kind | Should Match "dns#resourceRecordSet"
        $rrset.Name | Should Match $dnsName1
        $rrset.Rrdatas | Should Match $rrdata1
        $rrset.Ttl | Should Match $ttl_default
        $rrset.Type | Should Match "A"
    }

    It "should work and create a new ResourceRecordSet with the specified properties and custom ttl" {
        $rrset = New-GcdResourceRecordSet -Name $dnsName1 -Rrdata $rrdata1 -Type "A" -Ttl $ttl1
        $rrset.Count | Should Be 1

        $rrset.GetType().FullName | Should Match $rrsetType
        $rrset.Kind | Should Match "dns#resourceRecordSet"
        $rrset.Name | Should Match $dnsName1
        $rrset.Rrdatas | Should Match $rrdata1
        $rrset.Ttl | Should Match $ttl1
        $rrset.Type | Should Match "A"
    }
}