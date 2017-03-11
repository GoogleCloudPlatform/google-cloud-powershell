. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

$script:clusterDeletionScriptBlock =
{
    param($clusterName)
    gcloud container clusters delete $clusterName -q 2>$null
}

Describe "Get-GkeCluster" {
    $r = Get-Random
    $script:clusterOneName = "get-gkecluster-one-$r"
    $script:clusterTwoName = "get-gkecluster-two-$r"
    $script:clusterThreeName = "get-gkecluster-three-$r"
    $additionalZone = "us-central1-a"

    # Create Cluster in Parallel to save time.
    $clusterCreationScriptBlock =
    {
        param($clusterName, $clusterZone, $clusterAdditionalZone)
        if ($null -eq $clusterZone -and $null -eq $clusterAdditionalZone) {
            gcloud container clusters create $clusterName --num-nodes=1 2>$null
        }
        else {
            gcloud container clusters create $clusterName --zone $clusterZone `
                    --additional-zones $clusterAdditionalZone --num-nodes=1 2>$null
        }
    }

    $jobOne = Start-Job -ScriptBlock $clusterCreationScriptBlock -ArgumentList $clusterOneName
    $jobTwo = Start-Job -ScriptBlock $clusterCreationScriptBlock -ArgumentList $clusterTwoName
    $jobThree = Start-Job -ScriptBlock $clusterCreationScriptBlock `
                          -ArgumentList @($clusterThreeName, $zone, $additionalZone)

    Wait-Job $jobOne, $jobTwo, $jobThree | Remove-Job

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
        $jobOne = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList $clusterOneName
        $jobTwo = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList $clusterTwoName
        $jobThree = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList $clusterThreeName
        Wait-Job $jobOne, $jobTwo, $jobThree | Remove-Job
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
}

Describe "Add-GkeCluster" {
    $r = Get-Random

    # Create the cluster in parallel to reduce wait time.
    $gcloudCmdletsPath = (Resolve-Path "$PSScriptRoot\..\GcloudCmdlets.ps1").Path

    # Cluster One Creation.
    $clusterOneScriptBlock = {
        param($cmdletPath, $clusterName, $clusterDescription)
        . $cmdletPath;
        Install-GCloudCmdlets | Out-Null;
        Add-GkeCluster -ClusterName $clusterName -ImageType gci `
                       -Description $clusterDescription -DisableLoggingService
    }

    $clusterOneName = "gcp-new-gkecluster-$r"
    $clusterOneDescription = "My cluster"
    $clusterOneJob = Start-Job -ScriptBlock $clusterOneScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterOneName, $clusterOneDescription)

    # Cluster Two Creation.
    $script:networkName = "test-network-$r"

    # Create a network and extract out subnet that corresponds to region "us-central1".
    gcloud compute networks create $networkName 2>$null
    $region = "us-central1"
    $network = Get-GceNetwork $networkName
    $subnet = $network.Subnetworks | Where-Object {$_.Contains($region)}
    $subnet -match "subnetworks/([^/]*)"
    $script:subnetName = $Matches[1]

    $clusterTwoScriptBlock = {
        param($cmdletPath, $clusterName, $clusterNetwork, $clusterSubnetwork, $clusterAdditionalZone)
        . $cmdletPath;
        Install-GCloudCmdlets | Out-Null;
        Add-GkeCluster -ClusterName $clusterName -MachineType n1-standard-1 `
                       -Network $clusterNetwork -Subnetwork $clusterSubnetwork `
                       -InitialNodeCount 4 -AdditionalZone $clusterAdditionalZone
    }

    $clusterTwoName = "gcp-new-gkecluster-2-$r"
    $clusterTwoAdditionalZone = "us-central1-c"
    $clusterTwoJob = Start-Job -ScriptBlock $clusterTwoScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterTwoName, $network.Name,
                                               $subnetName, $clusterTwoAdditionalZone)

    # Cluster Three Creation.
    $clusterThreeScriptBlock = {
        param($cmdletPath, $clusterName, $clusterZone, $clusterUsername, $clusterPassword)
        . $cmdletPath;
        Install-GCloudCmdlets | Out-Null;
        $password = ConvertTo-SecureString $clusterPassword -AsPlainText -Force
        $credential = New-Object System.Management.Automation.PSCredential ($clusterUsername, $password)
        Add-GkeCluster -ClusterName $clusterName `
                       -MasterCredential $credential `
                       -Zone $clusterZone
    }

    $clusterThreeName = "gcp-new-gkecluster-3-$r"
    $clusterThreeZone = "europe-west1-b"
    $clusterThreeUsername = Get-Random
    $clusterThreePassword = Get-Random
    $clusterThreeJob = Start-Job -ScriptBlock $clusterThreeScriptBlock `
                                 -ArgumentList @($gcloudCmdletsPath, $clusterThreeName,
                                                 $clusterThreeZone, $clusterThreeUsername,
                                                 $clusterThreePassword)

    # For cluster with node config, the script block is having trouble converting $nodeConfig object
    # so we will just create it here (but the other clusters are being created in the background so that's ok.
    $clusterFourName = "gcp-new-gkecluster-4-$r"
    $clusterFourZone = "us-west1-a"
    $clusterFourConfig = New-GkeNodeConfig -DiskSizeGb 20 `
                                            -LocalSsdCount 3 `
                                            -Label @{"Release" = "stable"} `
                                            -MachineType n1-highcpu-4

    Add-GkeCluster -ClusterName $clusterFourName `
                   -Zone $clusterFourZone `
                   -NodeConfig $clusterFourConfig `
                   -DisableMonitoringService

    It "should work" {
        Wait-Job $clusterOneJob | Remove-Job
        $cluster = Get-GkeCluster -ClusterName $clusterOneName

        $cluster.Status | Should Be RUNNING
        $cluster.LoggingService | Should Be none
        $cluster.NodeConfig.ImageType | Should Be gci
        $cluster.Description | Should Be $clusterOneDescription
        $cluster.Zone | Should Be $zone
    }

    It "should work with -Network, -Subnetwork and -AdditionalZone" {
        Wait-Job $clusterTwoJob | Remove-Job
        $cluster = Get-GkeCluster -ClusterName $clusterTwoName

        $cluster.Status | Should Be RUNNING
        $cluster.NodeConfig.MachineType | Should Be n1-standard-1
        $cluster.Zone | Should Be $zone
        $cluster.Locations.Count | Should Be 2
        $cluster.Locations -Contains $zone | Should Be $true
        $cluster.Locations -Contains $clusterTwoAdditionalZone | Should Be $true
        $cluster.Network | Should Be $networkName
        $cluster.Subnetwork | Should Be $subnetName
        $cluster.CurrentNodeCount -ge 4 | Should Be $true
    }

    It "should work with -MasterCredential" {
        Wait-Job $clusterThreeJob | Remove-Job
        $cluster = Get-GkeCluster -ClusterName $clusterThreeName

        $cluster.Status | Should Be RUNNING
        $cluster.Zone | Should Be $clusterThreeZone
        $cluster.MasterAuth.Username | Should BeExactly $clusterThreeUsername
        $cluster.MasterAuth.Password | Should BeExactly $clusterThreePassword
        $cluster.NodeConfig.OauthScopes -contains "https://www.googleapis.com/auth/bigquery" |
            Should Be $true
    }

    It "should work with NodeConfig" {
        $cluster = Get-GkeCluster -ClusterName $clusterFourName -Zone $clusterFourZone

        $cluster.Status | Should Be RUNNING
        $cluster.MonitoringService | Should Be none
        $cluster.NodeConfig.MachineType | Should Be n1-highcpu-4
        $cluster.NodeConfig.Labels["Release"] | Should BeExactly "stable"
        $cluster.NodeConfig.LocalSsdCount | Should Be 3
        $cluster.NodeConfig.DiskSizeGb | Should Be 20
        $cluster.Zone | Should Be $clusterFourZone
    }

    It "should throw error for trying to create existing cluster" {
        { Add-GkeCluster -ClusterName $clusterOneName -ErrorAction Stop } |
            Should Throw "already exists"
    }

    AfterAll {
        $jobOne = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList $clusterOneName
        $jobTwo = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList $clusterTwoName
        $jobThree = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList $clusterThreeName
        $jobFour = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList $clusterFourName
        gcloud compute networks delete $networkName 2>$null
        Wait-Job $jobOne, $jobTwo, $jobThree, $jobFour | Remove-Job
    }
}
