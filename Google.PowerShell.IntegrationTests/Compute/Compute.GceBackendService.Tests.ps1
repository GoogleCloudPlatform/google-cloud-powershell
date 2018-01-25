. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GceBackendService" {
    $r = Get-Random
    $serviceName1 = "backend-service1-$r"
    $serviceName2 = "backend-service2-$r"

    It "should fail for wrong project" {
        { Get-GceBackendService -Project "asdf" } | Should Throw 403
    }

    It "should fail to get non-existent proxy" {
        { Get-GceBackendService $serviceName1 } | Should Throw 404
    }

    gcloud compute http-health-checks create "health-check-$r" 2>$null
    gcloud compute backend-services create $serviceName1 --http-health-checks "health-check-$r" --global 2>$null
    gcloud compute backend-services create $serviceName2 --http-health-checks "health-check-$r" --global 2>$null

    It "should get all maps" {
        $maps = Get-GceBackendService
        $maps.Count -ge 2 | Should Be $true
        ($maps | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.BackendService }
    }

    It "should get url map by name" {
        $map = Get-GceBackendService $serviceName1
        $map.Count | Should Be 1
        ($map | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.BackendService }
        $map.Name | Should Be $serviceName1
    }
    
    gcloud compute backend-services delete $serviceName1 --global -q 2>$null
    gcloud compute backend-services delete $serviceName2 --global -q 2>$null
    gcloud compute http-health-checks delete "health-check-$r" -q 2>$null
}

Reset-GCloudConfig $oldActiveConfig $configName
