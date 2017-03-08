. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GkeCluster" {
    $r = Get-Random
    $script:clusterOneName = "get-gkecluster-one-$r"
    $script:clusterTwoName = "get-gkecluster-two-$r"
    $script:clusterThreeName = "get-gkecluster-three-$r"
    $additionalZone = "us-central1-a"

    gcloud container clusters create $clusterOneName --num-nodes=1 2>$null
    gcloud container clusters create $clusterTwoName --num-nodes=1 2>$null
    gcloud container clusters create $clusterThreeName --zone $zone `
            --additional-zones $additionalZone --num-nodes=1 2>$null

    It "should work" {
        $clusters = Get-GkeCluster
        $clusters.Count -ge 3 | Should Be $true

        $clusterOne = $clusters | Where Name -eq $clusterOneName
        $clusterTwo = $clusters | Where Name -eq $clusterTwoName
        $clusterThree = $clusters | Where Name -eq $clusterThreeName

        $clusterOne.Zone | Should BeExactly $zone
        $clusterTwo.Zone | Should BeExactly $zone
        $clusterThree.Zone | Should BeExactly $zone

        $clusterThree.Locations.Count | Should Be 2
        $clusterThree.Locations -contains $zone | Should Be $true
        $clusterThree.Locations -contains $additionalZone | Should Be $true
        $clusterThree.CurrentNodeCount | Should Be 2

        $clusterOne.Locations | Should Be $zone
        $clusterTwo.Locations | Should Be $zone
        $clusterOne.CurrentNodeCount | Should Be 1
        $clusterTwo.CurrentNodeCount | Should Be 1
    }

    It "should work with -ClusterName" {
        $clusters = Get-GkeCluster -ClusterName $clusterOneName, $clusterThreeName
        $clusters.Count | Should Be 2

        $clusters | Where Name -eq $clusterOneName | Should Not BeNullOrEmpty
        $clusters | Where Name -eq $clusterThreeName | Should Not BeNullOrEmpty
    }

    It "should work with -Zone" {
        $clusters = Get-GkeCluster -Zone "us-east1-b"
        $clusters | Should BeNullOrEmpty

        $clusters = Get-GkeCluster -Zone $zone
        $clusters.Count | Should Be 3

        $clusters | Where Name -eq $clusterOneName | Should Not BeNullOrEmpty
        $clusters | Where Name -eq $clusterTwoName | Should Not BeNullOrEmpty
        $clusters | Where Name -eq $clusterThreeName | Should Not BeNullOrEmpty
    }

    It "should work with -ClusterName and -Zone" {
        $clusterThree = Get-GkeCluster -ClusterName $clusterThreeName -Zone $zone
        $clusterThree.Name | Should BeExactly $clusterThreeName
        $clusterThree.Zone | Should BeExactly $zone

        { Get-GkeCluster -ClusterName $clusterThreeName -Zone "us-east1-b" -ErrorAction Stop } |
            Should Throw "cannot be found"
    }

    It "should raise an error for non-existing cluster" {
        { Get-GkeCluster -ClusterName "cluster-no-exist-$r" -ErrorActionStop } |
            Should Throw "cannot be found"
    }

    AfterAll {
        gcloud container clusters delete $clusterOneName -q 2>$null
        gcloud container clusters delete $clusterTwoName -q 2>$null
        gcloud container clusters delete $clusterThreeName -q 2>$null
    }
}