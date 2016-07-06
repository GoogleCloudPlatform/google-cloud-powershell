. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
$project, $_, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcSqlSslCert" {
    # A note for this series of tests: 
    # Due to the fact the list functionality does not tell SSL certificate sha1fingerprints, all tests for 
    # if singular get is working have to use a premade SSL certificate. 
    # This can change once the Add-GcSqlSslCert cmdlet is done,
    # as we can then get a test-specific SSLCertificate and its sha1fingerprint.

    $r = Get-Random
    # A random number is used to avoid collisions with the speed of creating
    # and deleting instances.
    $instance = "test-inst$r"
    gcloud sql instances create $instance --quiet 2>$null

    It "should get a reasonable list response when no sslcerts exist" {
        $certs = Get-GcSqlSslCert $instance
        $certs.Count | Should Be 0
    }

    gcloud sql ssl-certs create "test-ssl" "test$r.txt" --instance $instance --quiet 2>$null

    It "should get a reasonable list response when an sslcert exists" {
        $certs = Get-GcSqlSslCert $instance
        $certs.Count | Should Be 1
    }

    It "should get the correct response for a specific sslcert" {
        #This test will change in the future, see the note.
        $cert = Get-GcSqlSslCert "test-db" "6c80d45f3bb0e22528fb5c9a0e3a83baee4331ab"
        $cert.commonName | Should Be "test-ssl"
        $cert.certSerialNumber | Should be "171810368"
    }

    Remove-Item "test$r.txt"
    gcloud sql instances delete $instance --quiet 2>$null
}
Reset-GCloudConfig $oldActiveConfig $configName
