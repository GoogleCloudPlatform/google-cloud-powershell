. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

# Define variables that will be used in testing
$project = "gcloud-powershell-testing"

$nonExistProject = "project-no-exist"
$nonExistManagedZone = "zone-no-exist"
$accessErrProject = "asdf"

$changeType = "Google.Apis.Dns.v1.Data.Change"
$managedZoneType = "Google.Apis.Dns.v1.Data.ManagedZone"
$quotaType = "Google.Apis.Dns.v1.Data.Quota"
$rrsetType = "Google.Apis.Dns.v1.Data.ResourceRecordSet"

$changeKind = "dns#change"
$managedZoneKind = "dns#managedZone"
$quotaKind = "dns#quota"
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

$rrdataA1 = "7.5.7.8"
$rrdataA2 = "7.5.6.8"
$rrdataAAAA = "2001:db8:85a3::8a2e:370:7334"
$rrdataCNAME1_2 = "hostname.b.gcloudexample1.com."
$rrdataTXT1 = "test-verification=2ZzjfideIJFLFje83"
$rrdataTXT2 = "test-verification2=JFLFje832ZzjfideI"

$ttl1 = 300
$ttlDefault = 3600

$testRrsetA = New-GcdResourceRecordSet -Name $dnsName1 -Rrdata $rrdataA1 -Type "A" -Ttl $ttl1
$testRrsetAAAA = New-GcdResourceRecordSet -Name $dnsName1_1 -Rrdata $rrdataAAAA -Type "AAAA" -Ttl $ttl1
$testRrsetCNAME = New-GcdResourceRecordSet -Name $dnsName1_2 -Rrdata $rrdataCNAME1_2 -Type "CNAME" -Ttl $ttl1
$testRrsetTXT1 = New-GcdResourceRecordSet -Name $dnsName1 -Rrdata $rrdataTXT1 -Type "TXT" -Ttl $ttl1
$testRrsetTXT2 = New-GcdResourceRecordSet -Name $dnsName1 -Rrdata $rrdataTXT2 -Type "TXT" -Ttl $ttl1

$transactionFile = "transaction.yaml"

$Err_NeedChangeContent = "Must specify at least 1 non-null, non-empty value for Add or Remove."
$Err_ProjectZonesNotDeleted = "All ManagedZones in the specified project have not been deleted."

# Define functions that will be used in testing

# Force remove all existing ManagedZones, including non-empty ones
function Remove-AllManagedZone($projectName) {
    Get-GcdManagedZone -Project $project | Remove-GcdManagedZone -Force
}

function Remove-FileIfExists($fileName) {
    if (Test-Path $fileName) {
        Remove-Item $fileName
    }
}
