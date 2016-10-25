. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$r = Get-Random
Describe "Get-GceTargetPool"{
    $httpProxyName = "http-proxy-$r"
    $httpsProxyName= "https-proxy-$r"

    It "should fail for wrong project" {
        { Get-GceTargetProxy -Project "asdf" } | Should Throw 403
    }

    It "should fail to get non-existant proxy" {
        { Get-GceTargetProxy $httpProxyName -Http } | Should Throw 404
        { Get-GceTargetProxy $httpsProxyName -Https } | Should Throw 404
        { Get-GceTargetProxy $httpProxyName } | Should Throw "Can not find target proxy"
    }

    Context "with data" {
        BeforeAll {
            gcloud compute http-health-checks create "health-check-$r" 2>$null
            gcloud compute backend-services create "backend-$r" --http-health-check "health-check-$r" 2>$null
            gcloud compute url-maps create "url-map-$r" --default-service "backend-$r" 2>$null
            gcloud compute target-http-proxies create $httpProxyName --url-map "url-map-$r" 2>$null
            # TODO(jimwp): Make this a target-https-proxy by creating a self signed certificate.
            gcloud compute target-http-proxies create $httpsProxyName --url-map "url-map-$r" 2>$null
        }

        It "should get all Proxies" {
            $proxies = Get-GceTargetProxy
            $proxies.Count | Should Be 2
        }
        
        It "should get proxy by protocol" {
            $proxy = Get-GceTargetProxy -Http
            $proxy.Count | Should Be 2
            ($proxy | Get-Member).TypeName | Should Be Google.Apis.Compute.v1.Data.TargetHttpProxy
            $proxy = Get-GceTargetProxy -Https
            $proxy.Count | Should Be 0
            $proxies = Get-GceTargetProxy -Http -Https
            $proxies.Count | Should Be 2
        }

        It "should get proxies by name" {
            $proxy = Get-GceTargetProxy $httpProxyName
            $proxy.Count | Should Be 1
            ($proxy | Get-Member).TypeName | Should Be Google.Apis.Compute.v1.Data.TargetHttpProxy
            $proxy.Name | Should Be $httpProxyName
            $proxy = Get-GceTargetProxy $httpsProxyName
            $proxy.Count | Should Be 1
            ($proxy | Get-Member).TypeName | Should Be Google.Apis.Compute.v1.Data.TargetHttpProxy
            $proxy.Name | Should Be $httpsProxyName
        }

        AfterAll {
            gcloud compute target-http-proxies delete $httpsProxyName -q 2>$null
            gcloud compute target-http-proxies delete $httpProxyName -q 2>$null
            gcloud compute url-maps delete "url-map-$r" -q 2>$null
            gcloud compute backend-services delete "backend-$r" -q 2>$null
            gcloud compute http-health-checks delete "health-check-$r" -q 2>$null
        }
    }
}


Reset-GCloudConfig $oldActiveConfig $configName
