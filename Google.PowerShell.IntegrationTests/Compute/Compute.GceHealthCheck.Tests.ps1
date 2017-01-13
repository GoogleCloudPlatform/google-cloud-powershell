. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$r = Get-Random

Describe "Add-GceHealthCheck" {
    $healthCheckName = "test-add-gcehealthcheck-$r"
    # We have to do this because there are 2 types of Google.Apis.Compute.v1.Data.HttpHealthCheck:
    # One is HttpHealthCheck and the other is HTTPHealthCheck (The only difference is the case)
    # and PowerShell New-Object does not like that.
    $tempHealthCheck = "test-add-gcehealthcheck-temp-$r"
    $tempHealthCheck2 = "test-add-gcehealthcheck-temp2-$r"
    try {
        $httpHealthCheckType = (Add-GceHealthCheck $tempHealthCheck).GetType()
        $httpsHealthCheckType = (Add-GceHealthCheck $tempHealthCheck2 -Https).GetType()
    }
    finally {
        Get-GceHealthCheck | Remove-GceHealthCheck -ErrorAction SilentlyContinue
    }

    It "should fail for wrong project" {
        { Add-GceHealthCheck $healthCheckName -Project "asdf" } | Should Throw 403
    }

    Context "add success" {
        AfterEach {
            Get-GceHealthCheck | Remove-GceHealthCheck -ErrorAction SilentlyContinue
        }

        It "should set defaults" {
            $healthCheck = Add-GceHealthCheck $healthCheckName
            ($healthCheck | Get-Member).TypeName | Should Be Google.Apis.Compute.v1.Data.HttpHealthCheck
            $healthCheck.Name | Should Be $healthCheckName
            $healthCheck.Port | Should Be 80
            $healthCheck.RequestPath | Should Be "/"
            $healthCheck.CheckIntervalSec | Should Be 5
            $healthCheck.TimeoutSec | Should Be 5
            $healthCheck.HealthyThreshold | Should Be 2
            $healthCheck.UnhealthyThreshold | Should Be 2
        }

        It "should set values" {
            $healthCheck = Add-GceHealthCheck $healthCheckName -Description "Test Description" `
                -HostHeader "google.com" -Port 50 -RequestPath "/some/path" -CheckInterval "0:0:2" `
                -Timeout "0:0:2" -HealthyThreshold 3 -UnhealthyThreshold 3 -Https
            ($healthCheck | Get-Member).TypeName | Should Be Google.Apis.Compute.v1.Data.HttpsHealthCheck
            $healthCheck.Name | Should Be $healthCheckName
            $healthCheck.Port | Should Be 50
            $healthCheck.Host | Should Be "google.com"
            $healthCheck.RequestPath | Should Be "/some/path"
            $healthCheck.CheckIntervalSec | Should Be 2
            $healthCheck.TimeoutSec | Should Be 2
            $healthCheck.HealthyThreshold | Should Be 3
            $healthCheck.UnhealthyThreshold | Should Be 3

        }

        It "should use an HTTP object over pipeline" {
            $initHealthCheck = $httpHealthCheckType.GetConstructor(@()).Invoke(@())
            $initHealthCheck.Name = $healthCheckName
            $healthCheck = $initHealthCheck | Add-GceHealthCheck
            ($healthCheck | Get-Member).TypeName | Should Be Google.Apis.Compute.v1.Data.HttpHealthCheck
            $healthCheck.Name | Should Be $healthCheckName
            $healthCheck.Port | Should Be 80
            $healthCheck.RequestPath | Should Be "/"
            $healthCheck.CheckIntervalSec | Should Be 5
            $healthCheck.TimeoutSec | Should Be 5
            $healthCheck.HealthyThreshold | Should Be 2
            $healthCheck.UnhealthyThreshold | Should Be 2
        }

        It "should use an HTTPS object over pipeline" {
            $initHealthCheck = $httpsHealthCheckType.GetConstructor(@()).Invoke(@())
            $initHealthCheck.Name = $healthCheckName
            $healthCheck = $initHealthCheck | Add-GceHealthCheck
            ($healthCheck | Get-Member).TypeName | Should Be Google.Apis.Compute.v1.Data.HttpsHealthCheck
            $healthCheck.Name | Should Be $healthCheckName
            $healthCheck.Port | Should Be 443
            $healthCheck.RequestPath | Should Be "/"
            $healthCheck.CheckIntervalSec | Should Be 5
            $healthCheck.TimeoutSec | Should Be 5
            $healthCheck.HealthyThreshold | Should Be 2
            $healthCheck.UnhealthyThreshold | Should Be 2
        }
    }
}

Describe "Get-GceHealthCheck" {
    $healthCheckName = "test-get-gcehealthcheck-$r"
    $healthCheckName2 = "test-get-gcehealthcheck2-$r"

    It "should fail for wrong project" {
        { Get-GceHealthCheck -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant health check" {
        { Get-GceHealthCheck $healthCheckName -Http } | Should Throw 404
    }

    It "should not fail when listing from project with zero health checks" {
        $noChecks = Get-GceHealthCheck
        $noChecks.Count | Should Be 0
    }

    Context "with checks" {
        BeforeAll {
            Add-GceHealthCheck $healthCheckName
            Add-GceHealthCheck $healthCheckName2
            # HTTP health checks and HTTPS health checks have separate namespaces.
            Add-GceHealthCheck $healthCheckName -Https
        }

        AfterAll {
            Get-GceHealthCheck | Remove-GceHealthCheck
        }

        It "should get all" {
            $allChecks = Get-GceHealthCheck
            $allChecks.Count | Should Be 3
        }

        It "should get all HTTP" {
            $httpChecks = Get-GceHealthCheck -Http
            $httpChecks.Count | Should Be 2
            ($httpChecks | Get-Member).TypeName | Should Be Google.Apis.Compute.v1.Data.HttpHealthCheck
        }

        It "should get all HTTPS" {
            $httpChecks = Get-GceHealthCheck -Https
            $httpChecks.Count | Should Be 1
            ($httpChecks | Get-Member).TypeName | Should Be Google.Apis.Compute.v1.Data.HttpsHealthCheck
        }

        It "should get both HTTP and HTTPS of name" {
            $healthChecks = Get-GceHealthCheck $healthCheckName
            $healthChecks.Count | Should Be 2
            $healthChecks.Name | Should Be $healthCheckName
        }

        It "should get HTTP by name" {
            $healthCheck = Get-GceHealthCheck $healthCheckName -Http
            $healthCheck.Count | Should Be 1
            $healthCheck.Name | Should Be $healthCheckName
            ($healthCheck | Get-Member).TypeName | Should Be Google.Apis.Compute.v1.Data.HttpHealthCheck
        }
    }
}

Describe "Remove-GceHealthCheck" {
    $healthCheckName = "test-remove-gcehealthcheck-$r"

    It "should fail for wrong project" {
        { Remove-GceHealthCheck $healthCheckName -Http -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant health check" {
        { Remove-GceHealthCheck $healthCheckName -Http } | Should Throw 404
    }

    Context "Remove HTTP" {
        BeforeEach {
            Add-GceHealthCheck $healthCheckName
        }

        It "should work" {
            Remove-GceHealthCheck $healthCheckName -Http
            { Get-GceHealthCheck $healthCheckName -Http } | Should Throw 404
        }

        It "should use object pipeline" {
            Get-GceHealthCheck $healthCheckName -Http |
                Remove-GceHealthCheck
            { Get-GceHealthCheck $healthCheckName -Http } | Should Throw 404
        }

        It "should fail removing HTTPS health check" {
            { Remove-GceHealthCheck $healthCheckName -Https } | Should Throw 404
            Remove-GceHealthCheck $healthCheckName -Http
        }
    }

    Context "Remove HTTPS" {
        BeforeEach {
            Add-GceHealthCheck $healthCheckName -Https
        }

        It "should work" {
            Remove-GceHealthCheck $healthCheckName -Https
            { Get-GceHealthCheck $healthCheckName -Https } | Should Throw 404
        }

        It "should use object pipeline" {
            Get-GceHealthCheck $healthCheckName -Https |
                Remove-GceHealthCheck
            { Get-GceHealthCheck $healthCheckName -Https } | Should Throw 404
        }

        It "should fail removing HTTP health check" {
            { Remove-GceHealthCheck $healthCheckName -Http } | Should Throw 404
            Remove-GceHealthCheck $healthCheckName -Https
        }
    }
}

Describe "Set-GceHealthCheck" {
    $healthCheckName = "test-set-gcehealthcheck-$r"

    Add-GceHealthCheck $healthCheckName
    Add-GceHealthCheck $healthCheckName -Https

    AfterAll {
        Get-GceHealthCheck | Remove-GceHealthCheck
    }

    It "should work" {
        $healthChecks = Get-GceHealthCheck $healthCheckName | %{
            $_.Port = 50
            $_.Host = "google.com"
            $_.RequestPath = "/some/path"
            $_.CheckIntervalSec = 2
            $_.TimeoutSec = 2
            $_.HealthyThreshold = 3
            $_.UnhealthyThreshold = 3
            $_
        } | Set-GceHealthCheck

        $healthChecks.Count | Should Be 2
        $healthChecks.Name | Should Be $healthCheckName
        $healthChecks.Port | Should Be 50
        $healthChecks.Host | Should Be "google.com"
        $healthChecks.RequestPath | Should Be "/some/path"
        $healthChecks.CheckIntervalSec | Should Be 2
        $healthChecks.TimeoutSec | Should Be 2
        $healthChecks.HealthyThreshold | Should Be 3
        $healthChecks.UnhealthyThreshold | Should Be 3

        $healthChecks = Get-GceHealthCheck $healthCheckName

        $healthChecks.Count | Should Be 2
        $healthChecks.Name | Should Be $healthCheckName
        $healthChecks.Port | Should Be 50
        $healthChecks.Host | Should Be "google.com"
        $healthChecks.RequestPath | Should Be "/some/path"
        $healthChecks.CheckIntervalSec | Should Be 2
        $healthChecks.TimeoutSec | Should Be 2
        $healthChecks.HealthyThreshold | Should Be 3
        $healthChecks.UnhealthyThreshold | Should Be 3
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
