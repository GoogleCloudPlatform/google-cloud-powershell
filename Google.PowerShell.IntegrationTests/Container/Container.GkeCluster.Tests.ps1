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
        { Get-GkeCluster -ClusterName "cluster-no-exist-$r" -ErrorAction Stop } |
            Should Throw "cannot be found"
    }

    AfterAll {
        gcloud container clusters delete $clusterOneName -q 2>$null
        gcloud container clusters delete $clusterTwoName -q 2>$null
        gcloud container clusters delete $clusterThreeName -q 2>$null
    }
}

Describe "New-GkeNodeConfig" {
    It "should work with -ImageType" {
        $nodeConfig = New-GkeNodeConfig -ImageType container_vm
        $nodeConfig.ImageType | Should BeExactly container_vm
    }

    It "should work with -MachineType" {
        $nodeConfig = New-GkeNodeConfig -ImageType container_vm -MachineType n1-standard-1
        $nodeConfig.ImageType | Should BeExactly container_vm
        $nodeConfig.MachineType | Should BeExactly n1-standard-1
    }

    It "should work with -DiskSizeGb" {
        $nodeConfig = New-GkeNodeConfig -MachineType n1-highcpu-2 -DiskSizeGb 20
        $nodeConfig.MachineType | Should BeExactly n1-highcpu-2
        $nodeConfig.DiskSizeGb | Should Be 20
    }

    It "should work with -LocalSsdCount" {
        $nodeConfig = New-GkeNodeConfig -ImageType gci -MachineType n1-highcpu-4 -LocalSsdCount 2
        $nodeConfig.MachineType | Should BeExactly n1-highcpu-4
        $nodeConfig.ImageType | Should BeExactly gci
        $nodeConfig.LocalSsdCount | Should Be 2
    }

    It "should work with -Metadata" {
        $nodeConfig = New-GkeNodeConfig -ImageType gci -Metadata @{"key" = "value"}
        $nodeConfig.ImageType | Should BeExactly gci
        $nodeConfig.Metadata["key"] | Should BeExactly "value"
    }

    It "should work with -Label" {
        $nodeConfig = New-GkeNodeConfig -ImageType gci -Metadata @{"key" = "value"} -Label @{"release" = "stable"}
        $nodeConfig.ImageType | Should BeExactly gci
        $nodeConfig.Metadata["key"] | Should BeExactly "value"
        $nodeConfig.Labels["release"] | Should BeExactly "stable"
    }

    It "should work with -Preemptible" {
        $nodeConfig = New-GkeNodeConfig -LocalSsdCount 3 -Preemptible
        $nodeConfig.Preemptible | Should Be $true
        $nodeConfig.LocalSsdCount | Should Be 3
    }

    It "should work with default service account" {
        $serviceAccount = New-GceServiceAccountConfig -BigtableAdmin Full `
                                                      -CloudLogging None `
                                                      -CloudMonitoring None `
                                                      -ServiceControl $false `
                                                      -ServiceManagement $false `
                                                      -Storage None
        $nodeConfig = New-GkeNodeConfig -ServiceAccount $serviceAccount
        $nodeConfig.ServiceAccount | Should Match "-compute@developer.gserviceaccount.com"
        $nodeConfig.OauthScopes | Should Match "bigtable.admin"
    }

    It "should work with a non-default service account" {
        $serviceAccount = New-GceServiceAccountConfig -Email testing@gserviceaccount.com `
                                                      -BigtableAdmin Full `
                                                      -CloudLogging None `
                                                      -CloudMonitoring None `
                                                      -ServiceControl $false `
                                                      -ServiceManagement $false `
                                                      -Storage None
        $nodeConfig = New-GkeNodeConfig -ServiceAccount $serviceAccount
        $nodeConfig.ServiceAccount | Should Match "testing@gserviceaccount.com"
        $nodeConfig.OauthScopes | Should Match "bigtable.admin"
    }

    It "should raise an error for bad metadata key" {
        { New-GkeNodeConfig -Metadata @{"#$" = "value"} } |
            Should Throw "can only be alphanumeric, hyphen or underscore."

        { New-GkeNodeConfig -Metadata @{"instance-template" = "test" } } | Should Throw "reserved keyword"
    }

    It "should raise an error for negative SsdCount" {
        { New-GkeNodeConfig -LocalSsdCount -3 } | Should Throw "less than the minimum allowed range of 0"
    }

    It "should raise an error for wrong DiskSize" {
        { New-GkeNodeConfig -DiskSizeGb 3 } | Should Throw "less than the minimum allowed range of 10"
    }

    AfterAll {
        gcloud container clusters delete $clusterOneName -q 2>$null
        gcloud container clusters delete $clusterTwoName -q 2>$null
        gcloud container clusters delete $clusterThreeName -q 2>$null
    }
}
