#TODO(quoct): Replace gcloud command with PowerShell cmdlet once they are available.
. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

$script:clusterDeletionScriptBlock =
{
    param($cmdletPath, $clusterName, $clusterZone)
    . $cmdletPath
    Install-GCloudCmdlets | Out-Null
    Remove-GkeCluster $clusterName -Zone $clusterZone
}

$script:gcloudCmdletsPath = (Resolve-Path "$PSScriptRoot\..\GcloudCmdlets.ps1").Path

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
        $jobOne = Start-Job -ScriptBlock $clusterDeletionScriptBlock `
                            -ArgumentList @($gcloudCmdletsPath, $clusterOneName, $zone)
        $jobTwo = Start-Job -ScriptBlock $clusterDeletionScriptBlock `
                            -ArgumentList @($gcloudCmdletsPath, $clusterTwoName, $zone)
        $jobThree = Start-Job -ScriptBlock $clusterDeletionScriptBlock `
                              -ArgumentList @($gcloudCmdletsPath, $clusterThreeName, $zone)
        Wait-Job $jobOne, $jobTwo, $jobThree | Remove-Job
    }
}

Describe "New-GkeNodeConfig" {
    It "should work with -ImageType" {
        $nodeConfig = New-GkeNodeConfig -ImageType cos
        $nodeConfig.ImageType | Should BeExactly cos
    }

    It "should work with -MachineType" {
        $nodeConfig = New-GkeNodeConfig -ImageType ubuntu -MachineType n1-standard-1
        $nodeConfig.ImageType | Should BeExactly ubuntu
        $nodeConfig.MachineType | Should BeExactly n1-standard-1
    }

    It "should work with -DiskSizeGb" {
        $nodeConfig = New-GkeNodeConfig -MachineType n1-highcpu-2 -DiskSizeGb 20
        $nodeConfig.MachineType | Should BeExactly n1-highcpu-2
        $nodeConfig.DiskSizeGb | Should Be 20
    }

    It "should work with -LocalSsdCount" {
        $nodeConfig = New-GkeNodeConfig -ImageType cos -MachineType n1-highcpu-4 -LocalSsdCount 2
        $nodeConfig.MachineType | Should BeExactly n1-highcpu-4
        $nodeConfig.ImageType | Should BeExactly cos
        $nodeConfig.LocalSsdCount | Should Be 2
    }

    It "should work with -Metadata" {
        $nodeConfig = New-GkeNodeConfig -ImageType cos -Metadata @{"key" = "value"}
        $nodeConfig.ImageType | Should BeExactly cos
        $nodeConfig.Metadata["key"] | Should BeExactly "value"
    }

    It "should work with -Label" {
        $nodeConfig = New-GkeNodeConfig -ImageType cos -Metadata @{"key" = "value"} -Label @{"release" = "stable"}
        $nodeConfig.ImageType | Should BeExactly cos
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

# Given a network name, create a network name and extract
# out a subnetwork that corresponds to region $region.
function New-NetworkAndSubnetwork($networkName, $region) {
    gcloud compute networks create $networkName 2>$null | Out-Null
    $network = Get-GceNetwork $networkName
    $subnet = $network.Subnetworks | Where-Object {$_.Contains($region)}
    $subnet -match "subnetworks/([^/]*)" | Out-Null
    return $Matches[1]
}

# Job for creating a cluster.
$script:clusterCreationWithoutUsingNodeConfigScriptBlock = {
    param($cmdletPath, $addGkeParameters)

    # Create a credential if that is provided.
    if ($null -ne $addGkeParameters["clusterUserName"]-and
        $null -ne $addGkeParameters["clusterPassword"]) {
        $password = ConvertTo-SecureString $addGkeParameters["clusterPassword"] -AsPlainText -Force
        $credential = New-Object System.Management.Automation.PSCredential `
                                ($addGkeParameters["clusterUserName"], $password)
        $addGkeParameters["MasterCredential"] = $credential
        $addGkeParameters.Remove("clusterUserName")
        $addGkeParameters.Remove("clusterPassword")
    }

    . $cmdletPath
    Install-GCloudCmdlets | Out-Null
    $PSBoundParameters.Remove("cmdletPath")
    Add-GkeCluster @addGkeParameters
}

Describe "Add-GkeCluster" {
    $r = Get-Random

    # Create the cluster in parallel to reduce wait time.
    # Cluster 1, 2 and 3 creation will be started as separate jobs.
    # This means that even if the time for creation is 6 minutes for each cluster,
    # altogether, we just have to wait around 6 minutes for 3 of them to be created
    # since they all start at the same time.

    # Cluster One creation.
    $script:clusterOneName = "gcp-new-gkecluster-$r"
    $clusterOneDescription = "My cluster"
    $clusterOneParameter = @{"clusterName" = $clusterOneName;
                             "description" = $clusterOneDescription;
                             "DisableLoggingService" = $true;
                             "DisableHttpLoadBalancing" = $true }

    # This cluster has name clusterOneName, description clusterOneDescription and no logging service.
    $clusterOneJob = Start-Job -ScriptBlock $clusterCreationWithoutUsingNodeConfigScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterOneParameter)


    # Cluster Three creation.
    $script:clusterTwoName = "gcp-new-gkecluster-3-$r"
    $script:clusterTwoZone = "europe-west1-b"
    $clusterTwoUsername = Get-Random
    # Password have to be 12 characters.
    $clusterTwoPassword = "this is 12 characters long"
    $clusterTwoParameter = @{"clusterName" = $clusterTwoName;
                               "zone" = $clusterTwoZone;
                               "clusterUsername" = $clusterTwoUsername;
                               "clusterPassword" = $clusterTwoPassword;
                               "maximumNodesToScaleTo" = 2;
                               "NumberOfNodePools" = 2 }

    $clusterTwoJob = Start-Job -ScriptBlock $clusterCreationWithoutUsingNodeConfigScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterTwoParameter)

    Wait-Job $clusterOneJob, $clusterTwoJob | Remove-Job
    Start-Sleep -Seconds 60

    # For cluster with node config, the script block is having trouble converting $nodeConfig object
    # so we will just create it here (but the other clusters are being created in the background so that's ok.
    $script:clusterThreeName = "gcp-new-gkecluster-4-$r"
    $script:clusterThreeZone = "us-west1-a"
    $clusterThreeMachineType = "n1-highcpu-4"
    $clusterThreeServiceAccount = New-GceServiceAccountConfig -BigQuery
    $clusterThreeConfig = New-GkeNodeConfig -DiskSizeGb 20 `
                                            -LocalSsdCount 3 `
                                            -Label @{"Release" = "stable"} `
                                            -ServiceAccount $clusterThreeServiceAccount `
                                            -MachineType $clusterThreeMachineType `

    Add-GkeCluster -ClusterName $clusterThreeName `
                   -Zone $clusterThreeZone `
                   -NodeConfig $clusterThreeConfig `
                   -DisableMonitoringService `
                   -EnableAutoUpgrade

    $script:clusterFourName = "gcp-new-gkecluster-5-$r"
    $script:clusterFourZone = "us-east1-d"

    It "should work" {
        $cluster = Get-GkeCluster -ClusterName $clusterOneName

        $cluster.Status -eq "RUNNING" -or $cluster.Status -eq "PROVISIONING" |
            Should Be $true
        $cluster.LoggingService | Should Be none
        $cluster.NodeConfig.ImageType | Should Be cos
        $cluster.Description | Should Be $clusterOneDescription
        $cluster.Zone | Should Be $zone
        $cluster.AddonsConfig.HttpLoadBalancing.Disabled | Should Be $true
    }

    It "should work with -MasterCredential and -MaximumNodesToScale" {
        $cluster = Get-GkeCluster -ClusterName $clusterTwoName -Zone $clusterTwoZone

        $cluster.Status -eq "RUNNING" -or $cluster.Status -eq "PROVISIONING" |
            Should Be $true
        $cluster.Zone | Should Be $clusterTwoZone
        $cluster.MasterAuth.Username | Should BeExactly $clusterTwoUsername
        $cluster.MasterAuth.Password | Should BeExactly $clusterTwoPassword
        $cluster.NodePools.Count | Should Be 2
        foreach ($nodePool in $cluster.NodePools) {
            $nodePool.Autoscaling.Enabled | Should Be $true
            $nodePool.Autoscaling.MaxNodeCount | Should Be 2
            $nodePool.Autoscaling.MinNodeCount | Should Be 1
        }
        # Cluster should have 2 nodes since it has 2 node pools and each has 1.
        $cluster.CurrentNodeCount -ge 2 | Should Be $true
    }

    It "should work with -NodeConfig and -EnableAutoUpgrade" {
        $cluster = Get-GkeCluster -ClusterName $clusterThreeName -Zone $clusterThreeZone

        $cluster.Status -eq "RUNNING" -or $cluster.Status -eq "PROVISIONING" |
            Should Be $true
        $cluster.MonitoringService | Should Be none
        $cluster.NodeConfig.MachineType | Should Be $clusterThreeMachineType
        $cluster.NodeConfig.Labels["Release"] | Should BeExactly "stable"
        $cluster.NodeConfig.LocalSsdCount | Should Be 3
        $cluster.NodeConfig.DiskSizeGb | Should Be 20
        $cluster.Zone | Should Be $clusterThreeZone
        $cluster.NodePools[0].Management.AutoUpgrade | Should Be $true
        $cluster.NodeConfig.OauthScopes -contains "https://www.googleapis.com/auth/bigquery" |
            Should Be $true
    }

    It "should work with -NodePool" {
        # Pipe the node pool from cluster 4 in to create this cluster.
        $nodePool = Get-GkeNodePool -ClusterName $clusterThreeName -Zone $clusterThreeZone
        $nodePool.InitialNodeCount = 2
        $nodePool | Add-GkeCluster -ClusterName $clusterFourName -Zone $clusterFourZone

        $cluster = Get-GkeCluster -ClusterName $clusterFourName -Zone $clusterFourZone
        $cluster.Status -eq "RUNNING" -or $cluster.Status -eq "PROVISIONING" |
            Should Be $true
        $cluster.MonitoringService | Should Not Be none
        $cluster.NodeConfig.MachineType | Should Be $clusterThreeMachineType
        $cluster.NodeConfig.Labels["Release"] | Should BeExactly "stable"
        $cluster.NodeConfig.LocalSsdCount | Should Be 3
        $cluster.NodeConfig.DiskSizeGb | Should Be 20
        # Since we modified the initial node count to 2.
        $cluster.CurrentNodeCount -ge 2 | Should Be $true
        $cluster.Zone | Should Be $clusterFourZone
    }

    It "should throw error for trying to create existing cluster" {
        { Add-GkeCluster -ClusterName $clusterOneName -ErrorAction Stop } |
            Should Throw "already exists"
    }

    AfterAll {
        Start-Sleep -Seconds 60
        $jobOne = Start-Job -ScriptBlock $clusterDeletionScriptBlock `
                            -ArgumentList @($gcloudCmdletsPath, $clusterOneName, $zone)
        $jobTwo = Start-Job -ScriptBlock $clusterDeletionScriptBlock `
                              -ArgumentList @($gcloudCmdletsPath, $clusterTwoName, $clusterTwoZone)
        $jobThree = Start-Job -ScriptBlock $clusterDeletionScriptBlock `
                             -ArgumentList @($gcloudCmdletsPath, $clusterThreeName, $clusterThreeZone)
        $jobFour = Start-Job -ScriptBlock $clusterDeletionScriptBlock `
                             -ArgumentList @($gcloudCmdletsPath, $clusterFourName, $clusterFourZone)

        Wait-Job $jobOne, $jobThree, $jobFour | Remove-Job
    }
}

# Check that a cluster is running.
function Check-Cluster($clusterName, $clusterZone) {
    $cluster = Get-GkeCluster -ClusterName $clusterName -Zone $clusterZone
    return $cluster.Status -eq "Running"
}

Describe "Set-GkeCluster" {
    $r = Get-Random
    $script:clusterTwoName = "gcp-set-gkecluster-2-$r"
    $script:clusterTwoZone = "asia-east1-a"

    $script:clusterThreeName = "gcp-set-gkecluster-3-$r"
    $script:clusterThreeZone = "europe-west1-b"

    $script:clusterNames = @($clusterTwoName, $clusterThreeName)
    $script:clusterZones = @($clusterTwoZone, $clusterThreeZone)

    $creationJobs = @()

    # Create the clusters in parallel to reduce wait time.
    for ($i = 0; $i -lt $clusterNames.Count; $i += 1) {
        $clusterParameters = @{"clusterName" = $clusterNames[$i];
                               "zone" = $clusterZones[$i]}
        $clusterCreationJob = Start-Job -ScriptBlock $clusterCreationWithoutUsingNodeConfigScriptBlock `
                                        -ArgumentList @($gcloudCmdletsPath, $clusterParameters)
        $creationJobs += $clusterCreationJob
    }

    Wait-Job $creationJobs
    Start-Sleep -Seconds 60

    # Wait until every cluster is fully started. Wait for at most 10 minutes.
    $counter = 0
    for ($i = 0; $i -lt $clusterNames.Count; $i += 1) {
        while ($true) {
            if (-not (Check-Cluster $clusterNames[$i] $clusterZones[$i])) {
                Start-Sleep -Seconds 30
                $counter += 1
                if ($counter -eq 20) {
                    break
                }
            }
            else {
                break
            }
        }
    }

    It "should work with cluster object to update node pool autoscaling" {
        $cluster = Get-GkeCluster -ClusterName $clusterTwoName -Zone $clusterTwoZone
        Set-GkeCluster -ClusterObject $cluster `
                       -NodePoolName "default-pool" `
                       -MaximumNodesToScaleTo 3 `
                       -MininumNodesToScaleTo 2

        $cluster = Get-GkeCluster -ClusterName $clusterTwoName -Zone $clusterTwoZone
        $cluster.NodePools[0].Autoscaling.MaxNodeCount | Should Be 3
        $cluster.NodePools[0].Autoscaling.MinNodeCount | Should Be 2
    }

    It "should work with pipeline and monitoring service" {
        $cluster = Get-GkeCluster -ClusterName $clusterThreeName -Zone $clusterThreeZone
        $cluster | Set-GkeCluster -MonitoringService "none"

        $cluster = Get-GkeCluster -ClusterName $clusterThreeName -Zone $clusterThreeZone
        $cluster.MonitoringService | Should Be "none"
    }

    AfterAll {
        $jobTwo = Start-Job -ScriptBlock $clusterDeletionScriptBlock `
                            -ArgumentList @($gcloudCmdletsPath, $clusterTwoName, $clusterTwoZone)
        $jobThree = Start-Job -ScriptBlock $clusterDeletionScriptBlock `
                              -ArgumentList @($gcloudCmdletsPath, $clusterThreeName, $clusterThreeZone)
        Wait-Job $jobTwo, $jobThree | Remove-Job
    }
}

Describe "Remove-GkeCluster" {
    $r = Get-Random

    # Create the cluster in parallel.
    # Cluster One creation.
    $clusterOneName = "gcp-remove-gkecluster-$r"
    $clusterOneZone = "asia-east1-a"

    $clusterTwoName = "gcp-remove-gkecluster-2-$r"
    $clusterTwoZone = "us-west1-b"

    $clusterThreeName = "gcp-remove-gkecluster-3-$r"
    $clusterThreeZone = "europe-west1-c"

    $clusterOneParameter = @{"clusterName" = $clusterOneName; "zone" = $clusterOneZone}
    $clusterTwoParameter = @{"clusterName" = $clusterTwoName; "zone" = $clusterTwoZone}
    $clusterThreeParameter = @{"clusterName" = $clusterThreeName; "zone" = $clusterThreeZone}

    # Start creating the cluster.
    $clusterOneJob = Start-Job -ScriptBlock $clusterCreationWithoutUsingNodeConfigScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterOneParameter)
    $clusterTwoJob = Start-Job -ScriptBlock $clusterCreationWithoutUsingNodeConfigScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterTwoParameter)
    $clusterThreeJob = Start-Job -ScriptBlock $clusterCreationWithoutUsingNodeConfigScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterThreeParameter)

    Wait-Job $clusterOneJob, $clusterTwoJob, $clusterThreeJob | Remove-Job
    Start-Sleep -Seconds 60

    $script:clusterNames = @($clusterOneName, $clusterTwoName, $clusterThreeName)
    $script:clusterZones = @($clusterOneZone, $clusterTwoZone, $clusterThreeZone)

    # Wait until every cluster is fully started. Wait for at most 10 minutes.
    $counter = 0
    for ($i = 0; $i -lt $clusterNames.Count; $i += 1) {
        while ($true) {
            if (-not (Check-Cluster $clusterNames[$i] $clusterZones[$i])) {
                Start-Sleep -Seconds 30
                $counter += 1
                if ($counter -eq 20) {
                    break
                }
            }
            else {
                break
            }
        }
    }

    # Running jobs to remove these 3 clusters in parallel to minimize test run time.
    $removeClusterByPipelineJob = Start-Job -ScriptBlock {
        param($cmdletPath, $clusterName, $clusterZone)
        # For this cluster, we remove by pipeline.
        . $cmdletPath
        Install-GCloudCmdlets | Out-Null
        Get-GkeCluster -ClusterName $clusterName -Zone $clusterZone | Remove-GkeCluster
    } -ArgumentList @($gcloudCmdletsPath, $clusterTwoName, $clusterTwoZone)

    $removeClusterByObjectJob = Start-Job -ScriptBlock {
        param($cmdletPath, $clusterName, $clusterZone)
        # For this cluster, we remove by object.
        . $cmdletPath
        Install-GCloudCmdlets | Out-Null
        $cluster = Get-GkeCluster -ClusterName $clusterName -Zone $clusterZone
        Remove-GkeCluster $cluster
    } -ArgumentList @($gcloudCmdletsPath, $clusterThreeName, $clusterThreeZone)

    $removeClusterWithZoneJob = Start-Job -ScriptBlock {
        param($cmdletPath, $clusterName, $clusterZone)
        # For this cluster, we used -Zone in the removal.
        . $cmdletPath
        Install-GCloudCmdlets | Out-Null
        Remove-GkeCluster $clusterName -Zone $clusterZone
    } -ArgumentList @($gcloudCmdletsPath, $clusterOneName, $clusterOneZone)

    It "should not remove cluster if -WhatIf is used" {
        Remove-GkeCluster -Name $clusterOneName -Zone $clusterOneZone -WhatIf
        Check-Cluster $clusterOneName $clusterOneZone | Should Be $true
    }

    It "should remove cluster by pipeline" {
        Wait-Job $removeClusterByPipelineJob | Remove-Job
        { Get-GkeCluster $clusterTwoName -Zone $clusterTwoZone -ErrorAction Stop } |
            Should Throw "cannot be found"
    }

    It "should remove cluster by cluster object" {
        Wait-Job $removeClusterByObjectJob | Remove-Job
        { Get-GkeCluster $clusterThreeName -Zone $clusterThreeZone -ErrorAction Stop } |
            Should Throw "cannot be found"
    }

    It "should remove cluster by name and -Zone" {
        Wait-Job $removeClusterWithZoneJob | Remove-Job
        { Get-GkeCluster $clusterOneName -Zone $clusterOneZone -ErrorAction Stop } |
            Should Throw "cannot be found"
    }

    It "should throw error for non-existent cluster" {
        { Remove-GkeCluster "non-existent-cluster-in-project" -ErrorAction Stop } | Should Throw "cannot be found"
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
