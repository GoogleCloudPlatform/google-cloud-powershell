. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$r = Get-Random

Describe "Get-GceUrlMap" {
    $previousAllCount = (Get-GceUrlMap).Count
    $urlMapName1 = "url-map1-$r"
    $urlMapName2 = "url-map2-$r"

    It "should fail for wrong project" {
        { Get-GceUrlMap -Project "asdf" } | Should Throw 403
    }

    It "should fail to get non-existant proxy" {
        { Get-GceUrlMap $urlMapName1 } | Should Throw 404
    }

    gcloud compute http-health-checks create "health-check-$r" 2>$null
    gcloud compute backend-services create "backend-$r" --http-health-checks "health-check-$r" --global 2>$null
    gcloud compute url-maps create $urlMapName1 --default-service "backend-$r" 2>$null
    gcloud compute url-maps create $urlMapName2 --default-service "backend-$r" 2>$null

    It "should get all maps" {
        $maps = Get-GceUrlMap
        $maps.Count - $previousAllCount | Should Be 2
        ($maps | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.UrlMap }
    }

    It "should get url map by name" {
        $map = Get-GceUrlMap $urlMapName1
        $map.Count | Should Be 1
        ($map | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.UrlMap }
    }
    
    gcloud compute url-maps delete $urlMapName1 -q 2>$null
    gcloud compute url-maps delete $urlMapName2 -q 2>$null
    gcloud compute backend-services delete "backend-$r" --global -q 2>$null
    gcloud compute http-health-checks delete "health-check-$r" -q 2>$null
}

Reset-GCloudConfig $oldActiveConfig $configName
