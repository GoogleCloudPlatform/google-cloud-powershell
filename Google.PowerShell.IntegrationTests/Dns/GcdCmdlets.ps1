. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

# Define variables that will be used in testing
$project = "gcloud-powershell-testing"

$nonExistProject = "project-no-exist"
$nonExistManagedZone = "zone-no-exist"
$accessErrProject = "asdf"

$changeType = "Google.Apis.Dns.v1.Data.Change"
$managedZoneType = "Google.Apis.Dns.v1.Data.ManagedZone"
$projectType = "Google.Apis.Dns.v1.Data.Project"
$rrsetType = "Google.Apis.Dns.v1.Data.ResourceRecordSet"

$changeKind = "dns#change"
$managedZoneKind = "dns#managedZone"
$projectKind = "dns#project"
$rrsetKind = "dns#resourceRecordSet"

$testZone1 = "test1"
$testZone2 = "test2"

$testDescrip1 = "test1 description"
$testDescrip2 = "test2 description"

$dnsName1 = "gcloudexample1.com."
$dnsName1_1 = "a.gcloudexample1.com."
$dnsName1_2 = "b.gcloudexample1.com."
$dnsName1_3 = "c.gcloudexample1.com."
$dnsName2 = "gcloudexample2.com."

$rrdata1 = "7.5.7.8"
$rrdata1_1 = "7.5.6.8"
$rrdata2 = "2001:db8:85a3::8a2e:370:7334"

$ttl1 = 300
$ttlDefault = 3600

$testRrset1 = New-GcdResourceRecordSet -Name $dnsName1 -Rrdata $rrdata1 -Type "A" -Ttl $ttl1
$testRrset2 = New-GcdResourceRecordSet -Name $dnsName1_1 -Rrdata $rrdata2 -Type "AAAA" -Ttl $ttl1

$transactionFile = "transaction.yaml"

$Err_NeedChangeContent = "Must specify at least 1 non-null, non-empty value for Add or Remove."
$Err_ProjectZonesNotDeleted = "All ManagedZones in the specified project have not been deleted."

# Define functions that will be used in testing

# Force remove all existing ManagedZones, including non-empty ones
# TODO(edatta): Simplify once Remove-GcdManagedZone cmdlet is done.
function Remove-AllManagedZone($projectName) {
    $preExistingZones = Get-GcdManagedZone -DnsProject $project

    ForEach ($zoneObject in $preExistingZones) {
        $zoneName = $zoneObject.Name

        New-Item empty-file -Force
        gcloud dns record-sets import --zone=$zoneName --delete-all-existing empty-file
        Remove-Item empty-file -Force
        gcloud dns managed-zones delete $zoneName --project=$project
    }
}

function Remove-FileIfExists($fileName)
{
    if (Test-Path $fileName) {
        Remove-Item $fileName
    }
}
