. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$r = Get-Random

Describe "Get-GceTargetPool"{
    $previousAllAcount = (Get-GceTargetPool).Count
    $previousRegionAcount = (Get-GceTargetPool -Region asia-east1).Count
    $poolName1 = "pool1-$r"
    $poolName2 = "pool2-$r"
    It "should fail for wrong project" {
        { Get-GceTargetPool -Project "asdf" } | Should Throw 403
    }

    It "should fail to get non-existant instance" {
        { Get-GceTargetPool $poolName1 } | Should Throw 404
    }

    Context "with data" {
        BeforeAll {
            gcloud compute target-pools create $poolName1 --region us-central1 2>$null

            gcloud compute target-pools create $poolName2 --region asia-east1 2>$null
        }

        It "should get all rules" {
            $rules = Get-GceTargetPool
            $rules.Count -$previousAllAcount | Should Be 2
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.TargetPool }
        }
        
        It "should get region rule" {
            $rules = Get-GceTargetPool -Region asia-east1
            $rules.Count - $previousRegionAcount | Should Be 1
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.TargetPool }
            $rules.Name | Should Be $poolName2
        }

        It "should get region rule by name" {
            $rules = Get-GceTargetPool $poolName1
            $rules.Count | Should Be 1
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.TargetPool }
            $rules.Name | Should Be $poolName1
        }

        AfterAll {
            gcloud compute target-pools delete $poolName1 --region us-central1 -q 2>$null
            gcloud compute target-pools delete $poolName2 --region asia-east1 -q 2>$null
        }
    }
}

Describe "Set-GceTargetPool" {
    $instance = Add-GceInstance "instance-$r" -BootDiskImage (Get-GceImage -Family "coreos-stable")
    $poolName = "pool-$r"
    gcloud compute target-pools create $poolName --region us-central1 2>$null
    $poolObj = Get-GceTargetPool $poolName
    It "should add instance with object" {
        $pool = $poolObj | Set-GceTargetPool -AddInstance $instance
        $pool.Instances.Count | Should Be 1
        $pool.Instances | Should Be $instance.SelfLink
    }

    It "should remove instance with object" {
        $pool = $poolObj | Set-GceTargetPool -RemoveInstance $instance
        $pool.Instances.Count | Should Be 0
    }
    It "should add instance by name" {
        $pool =Set-GceTargetPool $poolName -AddInstance $instance.SelfLink
        $pool.Instances.Count | Should Be 1
        $pool.Instances | Should Be $instance.SelfLink
    }

    It "should remove instance with object" {
        $pool =Set-GceTargetPool $poolName -RemoveInstance $instance.SelfLink
        $pool.Instances.Count | Should Be 0
    }

    gcloud compute target-pools delete $poolName --region us-central1 -q 2>$null
    $instance | Remove-GceInstance
}

Reset-GCloudConfig $oldActiveConfig $configName
