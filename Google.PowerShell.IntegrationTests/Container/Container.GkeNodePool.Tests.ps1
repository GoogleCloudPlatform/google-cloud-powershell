. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

$script:gcloudCmdletsPath = (Resolve-Path "$PSScriptRoot\..\GcloudCmdlets.ps1").Path

$r = Get-Random
$clusterOneName = "gkenodepool-one-$r"
$clusterTwoName = "gkenodepool-two-$r"
$clusterThreeName = "gkenodepool-three-$r"
$clusterTwoZone = "europe-west1-c"
$clusterThreeZone = "europe-west1-d"

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
$clusterThreeCreationJob = Start-Job -ScriptBlock $clusterCreationScriptBlock `
                                   -ArgumentList @($gcloudCmdletsPath, $clusterThreeName, $clusterThreeZone)

Wait-Job $clusterOneCreationJob, $clusterTwoCreationJob, $clusterThreeCreationJob | Remove-Job

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

Describe "Add-GkeNodePool" {
    It "should work with -ImageType, -MachineType and -DiskSizeGb" {
        $nodePool = Add-GkeNodePool "my-pool" -ImageType container_vm `
                                              -MachineType n1-standard-1 `
                                              -DiskSizeGb 20 `
                                              -Cluster $clusterOneName
        $cluster = Get-GkeCluster $clusterOneName
        $nodePoolOnline = $cluster.NodePools | Where Name -eq $nodePool.Name

        ForEach($pool in @($nodePool, $nodePoolOnline)) {
            $pool.Name | Should BeExactly "my-pool"
            $pool.Config.ImageType | Should Match CONTAINER_VM
            $pool.Config.MachineType | Should BeExactly n1-standard-1
            $pool.Config.DiskSizeGb | Should Be 20
        }
    }

    It "should work with -NodePool and Cluster Object" {
        $nodePoolObject = New-GkeNodePool "my-pool1" -ImageType cos `
                                               -Metadata @{"key" = "value"} `
                                               -MininumNodesToScaleTo 2 `
                                               -MaximumNodesToScaleTo 3
        $clusterObject = Get-GkeCluster $clusterTwoName -Zone $clusterTwoZone

        $nodePool = Add-GkeNodePool -NodePool $nodePoolObject `
                                    -Cluster $clusterObject `
                                    -Zone $clusterTwoZone

        $cluster = Get-GkeCluster $clusterTwoName -Zone $clusterTwoZone
        $nodePoolOnline = $cluster.NodePools | Where Name -eq $nodePool.Name

        ForEach($pool in @($nodePool, $nodePoolOnline)) {
            $pool.Name | Should BeExactly "my-pool1"
            $pool.Config.ImageType | Should Match cos
            $pool.Config.Metadata["key"] | Should BeExactly "value"
            $pool.Autoscaling.MinNodeCount | Should Be 2
            $pool.Autoscaling.MaxNodeCount | Should Be 3
        }
    }

    It "should work with -Label, -Preemptible and service account and pipeline" {
        $serviceAccount = New-GceServiceAccountConfig -BigtableAdmin Full `
                                                      -CloudLogging None `
                                                      -CloudMonitoring None `
                                                      -ServiceControl $false `
                                                      -ServiceManagement $false `
                                                      -Storage None

        $nodePoolConfig = New-GkeNodePool "my-pool2" -Metadata @{"key" = "value"} `
                                                     -Label @{"release" = "stable"} `
                                                     -PreEmptible `
                                                     -ServiceAccount $serviceAccount

        $nodePool = $nodePoolConfig | Add-GkeNodePool -Cluster $clusterThreeName -Zone $clusterThreeZone
        $cluster = Get-GkeCluster $clusterThreeName -Zone $clusterThreeZone
        $nodePoolOnline = $cluster.NodePools | Where Name -eq $nodePool.Name

        ForEach($pool in @($nodePool, $nodePoolOnline)) {
            $pool.Name | Should BeExactly "my-pool2"
            $pool.Config.Metadata["key"] | Should BeExactly "value"
            $pool.Config.Labels["release"] | Should BeExactly "stable"
            $pool.Config.Preemptible | Should Be $true
            $pool.Config.ServiceAccount | Should Match "-compute@developer.gserviceaccount.com"
            $pool.Config.OauthScopes[0] | Should Match "bigtable.admin"
        }
    }

    It "should raise an error for bad metadata key" {
        { Add-GkeNodePool "nodepool" -Metadata @{"#$" = "value"} -Cluster $clusterOneName } |
            Should Throw "can only be alphanumeric, hyphen or underscore."

        { Add-GkeNodePool "nodepool" -Metadata @{"instance-template" = "test" } -Cluster $clusterOneName } |
            Should Throw "reserved keyword"
    }

    It "should raise an error for negative SsdCount" {
        { Add-GkeNodePool "nodepool" -LocalSsdCount -3 -Cluster $clusterOneName } |
            Should Throw "less than the minimum allowed range of 0"
    }

    It "should raise an error for wrong DiskSize" {
        { Add-GkeNodePool "nodepool" -DiskSizeGb 3 -Cluster $clusterOneName } |
            Should Throw "less than the minimum allowed range of 10"
    }

    It "should raise error if we try to create an existing node pool" {
        { Add-GkeNodePool "default-pool" -Cluster $clusterOneName -ErrorAction Stop } |
            Should Throw "already exists"
    }

    It "should raise error if we try to add to non-existing cluster" {
        { Add-GkeNodePool "new-pool" -Cluster "cluster-non-exist-$r" -ErrorAction Stop } |
            Should Throw "not found"
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
$clusterThreeDeletionJob = Start-Job -ScriptBlock $clusterDeletionScriptBlock `
                                   -ArgumentList @($gcloudCmdletsPath, $clusterThreeName, $clusterThreeZone)

Wait-Job $clusterOneDeletionJob, $clusterTwoDeletionJob, $clusterThreeDeletionJob | Remove-Job
