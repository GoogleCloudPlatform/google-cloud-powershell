. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$r = Get-Random

Describe "Add-GceHealthCheck" {
    $healthCheckName = "test-add-gcehealthcheck-$r"

    It "should fail for wrong project" {
        { Add-GceHealthCheck $healthCheckName -Project "asdf" } | Should Throw 403
    }

    Context "add success" {
        AfterEach {
            Get-GceHealthCheck $healthCheckName | Remove-GceHealthCheck
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
            $healthCheck.RequestPath | Should Be "/some/path"
            $healthCheck.CheckIntervalSec | Should Be 2
            $healthCheck.TimeoutSec | Should Be 2
            $healthCheck.HealthyThreshold | Should Be 3
            $healthCheck.UnhealthyThreshold | Should Be 3

        }

        It "should use an http object over pipeline" {
            $initHealthCheck = New-Object Google.Apis.Compute.v1.Data.HttpHealthCheck
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

        It "should use an https object over pipeline" {
            $initHealthCheck = New-Object Google.Apis.Compute.v1.Data.HttpsHealthCheck
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
