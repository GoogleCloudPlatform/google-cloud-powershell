. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
$project, $_, $oldActiveConfig, $configName = Set-GCloudConfig

$r = Get-Random
# A random number is used to avoid collisions with the speed of creating
# and deleting instances.
$instance = "test-inst$r"

Add-GcSqlInstance $instance
Start-Sleep -Seconds 10

Describe "Get-GcSqlSslCert" {
    It "should get a reasonable list response when no sslcerts exist" {
        $certs = Get-GcSqlSslCert $instance
        $certs.Count | Should Be 0
    }

    Add-GcSqlSslCert $instance "test-ssl"
    Start-Sleep -Seconds 10

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

    It "should be able to take in an instance and get the information" -Skip {
        $testInstance = Get-GcSqlInstance $instance
        $certs = Get-GcSqlSslCert -InstanceObject $testInstance
        $certs.Count | Should Be 1
    }
}

Describe "Add-GcSqlSslCert" {
    It "should create an SSL cert for a given instance." {
       $cert = Add-GcSqlSslCert $instance  "test-ssl-2"
       Start-Sleep -Seconds 10
       $cert.CertInfo.Kind | Should Be "sql#sslCert"
       $cert.CertInfo.Instance | Should Be $instance
    }

    It "should compound with Get-GcSqlSslCert" -Skip {
       $instanceCerts = Get-GcSqlSslCert $instance
       $priorCount = $instanceCerts.Count
       $cert = Add-GcSqlSslCert $instance  "test-ssl-3"
       Start-Sleep -Seconds 10
       $instanceCerts = Get-GcSqlSslCert $instance
       ($instanceCerts.CommonName -contains "test-ssl-3") | Should Be true
       $instanceCerts.Count | Should Be ($priorCount + 1)
    }

    It "should be able to take in an instance object" -Skip {
        $cert = Get-GcSqlInstance $instance | Add-GcSqlSslCert -CommonName "test-ssl-4"
        $instanceCerts = Get-GcSqlSslCert $instance
       ($instanceCerts.CommonName -contains "test-ssl-4") | Should Be true
    }
}

Describe "Remove-GcSqlSslCert" {
    It "should work" {
        $sslName = "remove-test-1"
        $cert = Add-GcSqlSslCert $instance $sslName
        Start-Sleep -Seconds 10
        $fingerprint = $cert.CertInfo.sha1Fingerprint
        Remove-GcSqlSslCert $instance $fingerprint
        { Get-GcSqlSslCert $instance $fingerprint } | Should Throw "404"
    }

    It "should work with a pipelined Certificate" -Skip {
        $sslName = "remove-test-2"
        $cert = Add-GcSqlSslCert $instance $sslName
        Start-Sleep -Seconds 10
        $cert.CertInfo | Remove-GcSqlSslCert
        $fingerprint = $cert.CertInfo.sha1Fingerprint
        { Get-GcSqlSslCert $instance $fingerprint } | Should Throw "404"
    }

    It "shouldn't delete anything that doesn't exist" {
        { Remove-GcSqlSslCert $instance "f02" } | Should Throw "The SSL certificate does not exist. [404]"
    }
}

Describe "Reset-GcSqlSslConfig" {
    It "should work" -Skip {
        Add-GcSqlSslCert $instance  "test-ssl-res1"
        Start-Sleep -Seconds 10
        $instanceCerts = Get-GcSqlSslCert $instance
        ($instanceCerts.CommonName -contains "test-ssl-res1") | Should Be true
        Reset-GcSqlSslConfig $instance
        $instanceCerts = Get-GcSqlSslCert $instance
        ($instanceCerts.CommonName -contains "test-ssl-res1") | Should Be false
        $instanceCerts.Count | Should Be 0
    }

    It "should work with a pipelined instance" -Skip {
        Add-GcSqlSslCert $instance  "test-ssl-res2"
        Start-Sleep -Seconds 10
        $instanceCerts = Get-GcSqlSslCert $instance
        ($instanceCerts.CommonName -contains "test-ssl-res2") | Should Be true
        Reset-GcSqlSslConfig $instance
        $instanceCerts = Get-GcSqlSslCert $instance
        ($instanceCerts.CommonName -contains "test-ssl-res2") | Should Be false
        $instanceCerts.Count | Should Be 0
    }
}

Remove-GcSqlInstance $instance
Start-Sleep -Seconds 10

Describe "Add-GcSqlSslEphemeral" {
    $instance = "ephem-test$r"
    # We need to set up a second-generation instance.
    $setting = New-GcSqlSettingConfig "db-n1-standard-1"
    $config = New-GcSqlInstanceConfig $instance -SettingConfig $setting
    Add-GcSqlInstance $config
    Start-Sleep -Seconds 10

    It "should work" {
        $publicKey = Get-Content -Path "$PSScriptRoot\public.pem" -Raw
        $ssl = Add-GcSqlSslEphemeral $instance $publicKey
        $ssl.Kind | Should Be "sql#sslCert"
    }

    It "should error when given a bad public key" {
        { Add-GcSqlSslEphemeral $instance "no" } | Should Throw "Provided public key was in an invalid or unsupported format. [400]"
    }

    Start-Sleep -Seconds 10
    Remove-GcSqlInstance $instance
}

Reset-GCloudConfig $oldActiveConfig $configName
