#TODO(quoct): Replace gcloud command with PowerShell cmdlet once they are available.
. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

$script:clusterDeletionScriptBlock =
{
    param($cmdletPath, $clusterName)
    . $cmdletPath
    Install-GCloudCmdlets | Out-Null
    Remove-GkeCluster $clusterName
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
        $jobOne = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList @($gcloudCmdletsPath, $clusterOneName)
        $jobTwo = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList @($gcloudCmdletsPath, $clusterTwoName)
        $jobThree = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList @($gcloudCmdletsPath, $clusterThreeName)
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
                             "DisableLoggingService" = $true }

    # This cluster has name clusterOneName, description clusterOneDescription and no logging service.
    $clusterOneJob = Start-Job -ScriptBlock $clusterCreationWithoutUsingNodeConfigScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterOneParameter)

    # Cluster Two creation.

    # Create a network and extract out subnet that corresponds to region "us-central1".
    $script:networkName = "test-network-$r"
    $region = "us-central1"
    $script:subnetName = New-NetworkAndSubnetwork $networkName $region

    $script:clusterTwoName = "gcp-new-gkecluster-2-$r"
    $clusterTwoAdditionalZone = "us-central1-c"
    $clusterTwoMachineType = "n1-standard-4"
    $clusterTwoParameter = @{"clusterName" = $clusterTwoName;
                             "network" = $networkName;
                             "subnetwork" = $subnetName;
                             "additionalZone" = $clusterTwoAdditionalZone;
                             "initialNodeCount" = 2;
                             "machineType" = $clusterTwoMachineType }

    $clusterTwoJob = Start-Job -ScriptBlock $clusterCreationWithoutUsingNodeConfigScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterTwoParameter)

    # Cluster Three creation.
    $script:clusterThreeName = "gcp-new-gkecluster-3-$r"
    $clusterThreeZone = "europe-west1-b"
    $clusterThreeUsername = Get-Random
    $clusterThreePassword = Get-Random
    $clusterThreeParameter = @{"clusterName" = $clusterThreeName;
                               "zone" = $clusterThreeZone;
                               "clusterUsername" = $clusterThreeUsername;
                               "clusterPassword" = $clusterThreePassword }

    $clusterThreeJob = Start-Job -ScriptBlock $clusterCreationWithoutUsingNodeConfigScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterThreeParameter)

    # For cluster with node config, the script block is having trouble converting $nodeConfig object
    # so we will just create it here (but the other clusters are being created in the background so that's ok.
    $clusterFourName = "gcp-new-gkecluster-4-$r"
    $clusterFourZone = "us-west1-a"
    $clusterFourMachineType = "n1-highcpu-4"
    $clusterFourServiceAccount = New-GceServiceAccountConfig -BigQuery
    $clusterFourConfig = New-GkeNodeConfig -DiskSizeGb 20 `
                                            -LocalSsdCount 3 `
                                            -Label @{"Release" = "stable"} `
                                            -ServiceAccount $clusterFourServiceAccount `
                                            -MachineType $clusterFourMachineType

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
        $cluster.NodeConfig.MachineType | Should BeExactly $clusterTwoMachineType
        $cluster.Zone | Should Be $zone
        $cluster.Locations.Count | Should Be 2
        $cluster.Locations -Contains $zone | Should Be $true
        $cluster.Locations -Contains $clusterTwoAdditionalZone | Should Be $true
        $cluster.Network | Should Be $networkName
        $cluster.Subnetwork | Should Be $subnetName
        $cluster.CurrentNodeCount -ge 2 | Should Be $true
    }

    It "should work with -MasterCredential" {
        Wait-Job $clusterThreeJob | Remove-Job
        $cluster = Get-GkeCluster -ClusterName $clusterThreeName -Zone $clusterThreeZone

        $cluster.Status | Should Be RUNNING
        $cluster.Zone | Should Be $clusterThreeZone
        $cluster.MasterAuth.Username | Should BeExactly $clusterThreeUsername
        $cluster.MasterAuth.Password | Should BeExactly $clusterThreePassword
    }

    It "should work with NodeConfig" {
        $cluster = Get-GkeCluster -ClusterName $clusterFourName -Zone $clusterFourZone

        $cluster.Status | Should Be RUNNING
        $cluster.MonitoringService | Should Be none
        $cluster.NodeConfig.MachineType | Should Be $clusterFourMachineType
        $cluster.NodeConfig.Labels["Release"] | Should BeExactly "stable"
        $cluster.NodeConfig.LocalSsdCount | Should Be 3
        $cluster.NodeConfig.DiskSizeGb | Should Be 20
        $cluster.Zone | Should Be $clusterFourZone
        $cluster.NodeConfig.OauthScopes -contains "https://www.googleapis.com/auth/bigquery" |
            Should Be $true
    }

    It "should throw error for trying to create existing cluster" {
        { Add-GkeCluster -ClusterName $clusterOneName -ErrorAction Stop } |
            Should Throw "already exists"
    }

    AfterAll {
        $jobOne = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList @($gcloudCmdletsPath,$clusterOneName)
        $jobTwo = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList @($gcloudCmdletsPath,$clusterTwoName)
        $jobThree = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList @($gcloudCmdletsPath,$clusterThreeName)
        $jobFour = Start-Job -ScriptBlock $clusterDeletionScriptBlock -ArgumentList @($gcloudCmdletsPath,$clusterFourName)
        # Use receive job so we can see the output if there is any error.
        Wait-Job $jobOne, $jobTwo, $jobThree, $jobFour | Receive-Job
        Remove-Job $jobOne, $jobTwo, $jobThree, $jobFour
        # The cluster has to be deleted before we can delete the network.
        gcloud compute networks delete $networkName 2>$null
    }
}

Describe "Remove-GkeCluster" {
    # Check that a cluster is running.
    function Check-Cluster($clusterName, $clusterZone) {
        $cluster = Get-GkeCluster -ClusterName $clusterName -Zone $clusterZone
        return $cluster.Status -eq "Running"
    }

    $r = Get-Random

    # Create the cluster in parallel.
    # Cluster One creation.
    $clusterOneName = "gcp-new-gkecluster-$r"

    $clusterTwoName = "gcp-new-gkecluster-2-$r"
    $clusterTwoZone = "us-west1-b"

    $clusterThreeName = "gcp-new-gkecluster-3-$r"
    $clusterThreeZone = "europe-west1-c"

    $clusterFourName = "gcp-new-gkecluster-4-$r"
    $clusterFourZone = "asia-east1-a"

    $clusterOneParameter = @{"clusterName" = $clusterOneName}
    $clusterTwoParameter = @{"clusterName" = $clusterTwoName; "zone" = $clusterTwoZone}
    $clusterThreeParameter = @{"clusterName" = $clusterThreeName; "zone" = $clusterThreeZone}
    $clusterFourParameter = @{"clusterName" = $clusterFourName; "zone" = $clusterFourZone}

    # Start creating the cluster.
    $clusterOneJob = Start-Job -ScriptBlock $clusterCreationWithoutUsingNodeConfigScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterOneParameter)
    $clusterTwoJob = Start-Job -ScriptBlock $clusterCreationWithoutUsingNodeConfigScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterTwoParameter)
    $clusterThreeJob = Start-Job -ScriptBlock $clusterCreationWithoutUsingNodeConfigScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterThreeParameter)
    $clusterFourJob = Start-Job -ScriptBlock $clusterCreationWithoutUsingNodeConfigScriptBlock `
                               -ArgumentList @($gcloudCmdletsPath, $clusterFourParameter)

    Wait-Job $clusterOneJob, $clusterTwoJob, $clusterThreeJob, $clusterFourJob | Remove-Job

    # Check that all the clusters are really created.
    Check-Cluster $clusterOneName $zone
    Check-Cluster $clusterTwoName $clusterTwoZone
    Check-Cluster $clusterThreeName $clusterThreeZone
    Check-Cluster $clusterFourName $clusterFourZone

    # Running jobs to remove these 3 clusters in parallel to minimize test run time.
    $clusterTwoRemovalJob = Start-Job -ScriptBlock {
        param($cmdletPath, $clusterName, $clusterZone)
        # For this cluster, we remove by pipeline.
        . $cmdletPath
        Install-GCloudCmdlets | Out-Null
        Get-GkeCluster -ClusterName $clusterName -Zone $clusterZone | Remove-GkeCluster
    } -ArgumentList @($gcloudCmdletsPath, $clusterTwoName, $clusterTwoZone)

    $clusterThreeRemovalJob = Start-Job -ScriptBlock {
        param($cmdletPath, $clusterName, $clusterZone)
        # For this cluster, we remove by object.
        . $cmdletPath
        Install-GCloudCmdlets | Out-Null
        $cluster = Get-GkeCluster -ClusterName $clusterName -Zone $clusterZone
        Remove-GkeCluster $cluster
    } -ArgumentList @($gcloudCmdletsPath, $clusterThreeName, $clusterThreeZone)

    $clusterFourRemovalJob = Start-Job -ScriptBlock {
        param($cmdletPath, $clusterName, $clusterZone)
        # For this cluster, we used -Zone in the removal.
        . $cmdletPath
        Install-GCloudCmdlets | Out-Null
        Remove-GkeCluster $clusterName -Zone $clusterZone
    } -ArgumentList @($gcloudCmdletsPath, $clusterFourName, $clusterFourZone)

    It "should not remove cluster if -WhatIf is used" {
        Remove-GkeCluster -Name $clusterOneName -WhatIf
        Check-Cluster $clusterOneName $zone | Should Be $true
    }

    It "should remove cluster by name" {
        Remove-GkeCluster -Name $clusterOneName
        { Get-GkeCluster $clusterOneName -ErrorAction Stop } | Should Throw "cannot be found"
    }

    It "should remove cluster by pipeline" {
        Wait-Job $clusterTwoRemovalJob | Remove-Job
        { Get-GkeCluster $clusterTwoName -Zone $clusterTwoZone -ErrorAction Stop } |
            Should Throw "cannot be found"
    }

    It "should remove cluster by cluster object" {
        Wait-Job $clusterThreeRemovalJob | Remove-Job
        { Get-GkeCluster $clusterThreeName -Zone $clusterThreeZone -ErrorAction Stop } |
            Should Throw "cannot be found"
    }

    It "should remove cluster by -Zone" {
        Wait-Job $clusterFourRemovalJob | Remove-Job
        { Get-GkeCluster $clusterFourName -Zone $clusterFourZone -ErrorAction Stop } |
            Should Throw "cannot be found"
    }

    It "should throw error for non-existent cluster" {
        { Remove-GkeCluster "non-existent-cluster-in-project" -ErrorAction Stop } | Should Throw "cannot be found"
    }
}
