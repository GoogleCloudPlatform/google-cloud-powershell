. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$r = Get-Random
Describe "Get-GceForwardingRule"{
    $regionRuleName1 = "region-rule1-$r"
    $regionRuleName2 = "region-rule2-$r"
    $globalRuleName = "global-rule-$r"
    $previousAllCount = (Get-GceForwardingRule).Count
    $previousGlobalCount = (Get-GceForwardingRule).Count
    $previousAsiaEastCount = (Get-GceForwardingRule).Count

    It "should fail for wrong project" {
        { Get-GceForwardingRule -Project "asdf" } | Should Throw 403
    }

    It "should fail to get non-existant instance" {
        { Get-GceForwardingRule $regionRuleName1 } | Should Throw 404
        { Get-GceForwardingRule $globalRuleName -Global } | Should Throw 404
    }

    Context "with data" {
        BeforeAll {
            gcloud compute http-health-checks create "health-check-$r" 2>$null
            gcloud compute backend-services create "backend-$r" --http-health-checks "health-check-$r" --global 2>$null
            gcloud compute url-maps create "url-map-$r" --default-service "backend-$r" 2>$null
            gcloud compute target-http-proxies create "proxy-$r" --url-map "url-map-$r" 2>$null
            gcloud compute forwarding-rules create $globalRuleName --target-http-proxy "proxy-$r" --global --ports 8080 2>$null

            gcloud compute target-pools create "pool-$r" --region us-central1 2>$null
            gcloud compute forwarding-rules create $regionRuleName1 --target-pool "pool-$r" --region us-central1 2>$null

            gcloud compute target-pools create "pool-$r" --region asia-east1 2>$null
            gcloud compute forwarding-rules create $regionRuleName2 --target-pool "pool-$r" --region asia-east1 2>$null
        }

        It "should get all rules" {
            $rules = Get-GceForwardingRule
            $rules.Count - $previousAllCount | Should Be 3
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.ForwardingRule }
        }

        It "should get global rule" {
            $rules = Get-GceForwardingRule -Global
            $rules.Count - $previousGlobalCount | Should Be 1
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.ForwardingRule }
            $rules.Name | Should Be $globalRuleName
        }

        It "should get region rule" {
            $rules = Get-GceForwardingRule -Region asia-east1
            $rules.Count -$previousAsiaEastCount | Should Be 1
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.ForwardingRule }
            $rules.Name | Should Be $regionRuleName2
        }

        It "should get region rule by name" {
            $rules = Get-GceForwardingRule $regionRuleName1
            $rules.Count | Should Be 1
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.ForwardingRule }
            $rules.Name | Should Be $regionRuleName1
        }

        It "should get global rule by name" {
            $rules = Get-GceForwardingRule $globalRuleName -Global
            $rules.Count | Should Be 1
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.ForwardingRule }
            $rules.Name | Should Be $globalRuleName
        }

        AfterAll {
            gcloud compute forwarding-rules delete $regionRuleName1 --region us-central1 -q 2>$null
            gcloud compute target-pools delete "pool-$r" -q 2>$null

            gcloud compute forwarding-rules delete $regionRuleName2 --region asia-east1 -q 2>$null
            gcloud compute target-pools delete "pool-$r" --region asia-east1 -q 2>$null

            gcloud compute forwarding-rules delete $globalRuleName --global -q 2>$null
            gcloud compute target-http-proxies delete "proxy-$r" -q 2>$null
            gcloud compute url-maps delete "url-map-$r" -q 2>$null
            gcloud compute backend-services delete "backend-$r" --global -q 2>$null
            gcloud compute http-health-checks delete "health-check-$r" -q 2>$null
        }
    }
}

Reset-GCloudConfig $oldActiveConfig $configName