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
