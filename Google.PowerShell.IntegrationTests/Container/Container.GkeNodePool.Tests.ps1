. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

$script:gcloudCmdletsPath = (Resolve-Path "$PSScriptRoot\..\GcloudCmdlets.ps1").Path

$r = Get-Random
$clusterOneName = "gkenodepool-one-$r"
$clusterTwoName = "get-gkecluster-two-$r"
$clusterTwoZone = "europe-west1-c"

# Create Cluster in Parallel to save time.
$clusterCreationScriptBlock =
{
    param($cmdletPath, $clusterName, $clusterZone)
    . $cmdletPath
    Install-GCloudCmdlets | Out-Null
    Add-GkeCluster -ClusterName $clusterName -Zone $clusterZone
}

$clusterOneCreationJob = Start-Job -ScriptBlock $clusterCreationScriptBlock `
                                   -ArgumentList @($gcloudCmdletsPath, $clusterOneName, $zone)
$clusterTwoCreationJob = Start-Job -ScriptBlock $clusterCreationScriptBlock `
                                   -ArgumentList @($gcloudCmdletsPath, $clusterTwoName, $clusterTwoZone)

Wait-Job $clusterOneCreationJob, $clusterTwoCreationJob | Remove-Job

Describe "Get-GkeNodePool" {
    $additionalNodePool = "get-gkenodepool-$r"

    # Create an additional node pool.
    gcloud container node-pools create $additionalNodePool --zone $zone --cluster $clusterOneName 2>$null

    It "should work" {
        $nodePools = Get-GkeNodePool -Cluster $clusterOneName
        $nodePools.Count | Should Be 2
        
        $nodePools | Where Name -eq $additionalNodePool | Should Not BeNullOrEmpty
    }

    It "should work with -NodePoolName" {
        $nodePool = Get-GkeNodePool -Cluster $clusterOneName -NodePoolName $additionalNodePool
        $nodePool.Name | Should BeExactly $additionalNodePool
    }

    It "should work with cluster in a different zone" {
        $nodePool = Get-GkeNodePool -Cluster $clusterTwoName -Zone $clusterTwoZone
        $nodePool.Name | Should BeExactly "default-pool"
    }

    It "should raise an error for non-existing pool" {
        { Get-GkeNodePool -NodePoolName "cluster-no-exist" -ClusterName $clusterOneName -ErrorAction Stop } |
            Should Throw "cannot be found"
    }
}

Describe "New-GkeNodePool" {
    It "should work with -ImageType" {
        $nodePool = New-GkeNodePool "my-pool" -ImageType container_vm
        $nodePool.Name | Should BeExactly "my-pool"
        $nodePool.Config.ImageType | Should BeExactly container_vm
    }

    It "should work with -MachineType" {
        $nodePool = New-GkeNodePool "my-pool3" -ImageType container_vm `
                                              -MachineType n1-standard-1
        $nodePool.Name | Should BeExactly "my-pool3"
        $nodePool.Config.ImageType | Should BeExactly container_vm
        $nodePool.Config.MachineType | Should BeExactly n1-standard-1
    }

    It "should work with -DiskSizeGb and -MaximumNodesToScaleTo" {
        $nodePool = New-GkeNodePool "my-pool4" -MachineType n1-highcpu-2 `
                                               -DiskSizeGb 20 `
                                               -MaximumNodesToScaleTo 3
        $nodePool.Name | Should BeExactly "my-pool4"
        $nodePool.Config.MachineType | Should BeExactly n1-highcpu-2
        $nodePool.Config.DiskSizeGb | Should Be 20
        $nodePool.Autoscaling.MaxNodeCount | Should Be 3
    }

    It "should work with -LocalSsdCount" {
        $nodePool = New-GkeNodePool "my-pool5" -ImageType cos `
                                               -MachineType n1-highcpu-4 `
                                               -LocalSsdCount 2
        $nodePool.Name | Should BeExactly "my-pool5"
        $nodePool.Config.MachineType | Should BeExactly n1-highcpu-4
        $nodePool.Config.ImageType | Should BeExactly cos
        $nodePool.Config.LocalSsdCount | Should Be 2
    }

    It "should work with -Metadata and -MinimumNodesToScaleTo" {
        $nodePool = New-GkeNodePool "my-pool6" -ImageType cos `
                                               -Metadata @{"key" = "value"} `
                                               -MininumNodesToScaleTo 2 `
                                               -MaximumNodesToScaleTo 3
        $nodePool.Name | Should BeExactly "my-pool6"
        $nodePool.Config.ImageType | Should BeExactly cos
        $nodePool.Config.Metadata["key"] | Should BeExactly "value"
        $nodePool.Autoscaling.MinNodeCount | Should Be 2
        $nodePool.Autoscaling.MaxNodeCount | Should Be 3
    }

    It "should work with -Label" {
        $nodePool = New-GkeNodePool "my-pool7" -ImageType cos `
                                               -Metadata @{"key" = "value"} `
                                               -Label @{"release" = "stable"}
        $nodePool.Name | Should BeExactly "my-pool7"
        $nodePool.Config.ImageType | Should BeExactly cos
        $nodePool.Config.Metadata["key"] | Should BeExactly "value"
        $nodePool.Config.Labels["release"] | Should BeExactly "stable"
    }

    It "should work with -Preemptible" {
        $nodePool = New-GkeNodePool "my-pool8" -LocalSsdCount 3 -Preemptible
        $nodePool.Name | Should BeExactly "my-pool8"
        $nodePool.Config.Preemptible | Should Be $true
        $nodePool.Config.LocalSsdCount | Should Be 3
    }

    It "should work with default service account" {
        $serviceAccount = New-GceServiceAccountConfig -BigtableAdmin Full `
                                                      -CloudLogging None `
                                                      -CloudMonitoring None `
                                                      -ServiceControl $false `
                                                      -ServiceManagement $false `
                                                      -Storage None
        $nodePool = New-GkeNodePool "my-nodepool9" -ServiceAccount $serviceAccount
        $nodePool.Name | Should BeExactly "my-nodepool9"
        $nodePool.Config.ServiceAccount | Should Match "-compute@developer.gserviceaccount.com"
        $nodePool.Config.OauthScopes | Should Match "bigtable.admin"
    }

    It "should work with a non-default service account" {
        $serviceAccount = New-GceServiceAccountConfig -Email testing@gserviceaccount.com `
                                                      -BigtableAdmin Full `
                                                      -CloudLogging None `
                                                      -CloudMonitoring None `
                                                      -ServiceControl $false `
                                                      -ServiceManagement $false `
                                                      -Storage None
        $nodePool = New-GkeNodePool "my-nodepool10" -ServiceAccount $serviceAccount
        $nodePool.Name | Should BeExactly "my-nodepool10"
        $nodePool.Config.ServiceAccount | Should Match "testing@gserviceaccount.com"
        $nodePool.Config.OauthScopes | Should Match "bigtable.admin"
    }

    It "should raise an error for bad metadata key" {
        { New-GkeNodePool "nodepool" -Metadata @{"#$" = "value"} } |
            Should Throw "can only be alphanumeric, hyphen or underscore."

        { New-GkeNodePool "nodepool" -Metadata @{"instance-template" = "test" } } |
            Should Throw "reserved keyword"
    }

    It "should raise an error for negative SsdCount" {
        { New-GkeNodePool "nodepool" -LocalSsdCount -3 } |
            Should Throw "less than the minimum allowed range of 0"
    }

    It "should raise an error for wrong DiskSize" {
        { New-GkeNodePool "nodepool" -DiskSizeGb 3 } |
            Should Throw "less than the minimum allowed range of 10"
    }
}

$clusterDeletionScriptBlock =
{
    param($cmdletPath, $clusterName, $clusterZone)
    . $cmdletPath
    Install-GCloudCmdlets | Out-Null
    Remove-GkeCluster $clusterName -Zone $clusterZone
}

$clusterOneDeletionJob = Start-Job -ScriptBlock $clusterDeletionScriptBlock `
                                   -ArgumentList @($gcloudCmdletsPath, $clusterOneName, $zone)
$clusterTwoDeletionJob = Start-Job -ScriptBlock $clusterDeletionScriptBlock `
                                   -ArgumentList @($gcloudCmdletsPath, $clusterTwoName, $clusterTwoZone)

Wait-Job $clusterOneDeletionJob, $clusterTwoDeletionJob | Remove-Job
