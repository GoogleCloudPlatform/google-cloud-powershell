. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
$project, $_, $oldActiveConfig, $configName = Set-GCloudConfig

$r = Get-Random
# A random number is used to avoid collisions with the speed of creating
# and deleting instances.
$instance = "test-inst$r"
Describe "Get-GcSqlSslCert" {
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
        $certs = Get-GcSqlSslCert $instance
        $certToFind = $certs | Select-Object -first 1
        #This test will change in the future, see the note.
        $cert = Get-GcSqlSslCert $instance $certToFind.Sha1Fingerprint
        $cert.commonName | Should Be $certToFind.CommonName
        $cert.certSerialNumber | Should be $certToFind.CertSerialNumber
    }

    It "should be able to take in an instance and get the information" {
        $testInstance = Get-GcSqlInstance $instance
        $certs = Get-GcSqlSslCert -InstanceObject $testInstance
        $certs.Count | Should Be 1
    }

    Remove-Item "test$r.txt"
}

Describe "Add-GcSqlSslCert" {
    It "should create an SSL cert for a given instance." {
       $cert = Add-GcSqlSslCert $instance  "test-ssl-2"
       $cert.CertInfo.Kind | Should Be "sql#sslCert"
       $cert.CertInfo.Instance | Should Be $instance
    }

    It "should compound with Get-GcSqlSslCert" {
       $instanceCerts = Get-GcSqlSslCert $instance
       $priorCount = $instanceCerts.Count
       $cert = Add-GcSqlSslCert $instance  "test-ssl-3"
       $instanceCerts = Get-GcSqlSslCert $instance
       ($instanceCerts.CommonName -contains "test-ssl-3") | Should Be true
       $instanceCerts.Count | Should Be ($priorCount + 1)
    }

    It "should be able to take in an instance object" {
        $cert = Get-GcSqlInstance $instance | Add-GcSqlSslCert -CommonName "test-ssl-4"
        $instanceCerts = Get-GcSqlSslCert $instance
       ($instanceCerts.CommonName -contains "test-ssl-4") | Should Be true
    }
}

Describe "Remove-GcSqlSslCert" {
    It "should work" {
        $sslName = "remove-test-1"
        $cert = Add-GcSqlSslCert $instance $sslName
        $fingerprint = $cert.CertInfo.sha1Fingerprint
        Remove-GcSqlSslCert $instance $fingerprint
        { Get-GcSqlSslCert $instance $fingerprint } | Should Throw "404"
    }

    It "should work with a pipelined Certificate" {
        $sslName = "remove-test-2"
        $cert = Add-GcSqlSslCert $instance $sslName
        $cert.CertInfo | Remove-GcSqlSslCert
        $fingerprint = $cert.CertInfo.sha1Fingerprint
        { Get-GcSqlSslCert $instance $fingerprint } | Should Throw "404"
    }

    It "shouldn't delete anything that doesn't exist" {
        { Remove-GcSqlSslCert $instance "f02" } | Should Throw "The SSL certificate does not exist. [404]"
    }
}

gcloud sql instances delete $instance --quiet 2>$null
Reset-GCloudConfig $oldActiveConfig $configName
