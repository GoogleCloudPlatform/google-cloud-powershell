. $PSScriptRoot\..\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"
$zone = "us-central1-f"
$zone2 = "us-central1-a"
$noExistInstance = "gcps-instance-no-exist-$($env:USERNAME)"
$existantInstance = "gcps-instance-exist-$($env:USERNAME)"
$existantInstance2 = "gcps-instance-exist2-$($env:USERNAME)"
$existantInstance3 = "gcps-instance-exist3-$($env:USERNAME)"

Describe "Get-GceInstance" {

    gcloud compute instances create $existantInstance
    gcloud compute instances create $existantInstance2
    gcloud compute instances create $existantInstance3 --zone $zone2

	It "should fail to return non-existing instances" {
        { Get-GceInstance -Project $project -Zone $zone -Name $noExistInstance } | Should Throw "404"
    }

    It "should get one" {
        $instance = Get-GceInstance -Project $project -Zone $zone -Name $existantInstance
		$instance.Name | Should Be $existantInstance
		$instance.Kind | Should Be "compute#instance"
    }

    It "should get only zone" {
		$zoneInstances = Get-GceInstance -Project $project- Zone $zone
        $zoneInstances.Length -gt 1 | Should Be $true
		$zoneInstances.Kind | Should Be "compute#instance"
		$zoneInstances.Zone | Should Be $zone
    }

    It "should list all buckets in a project" {
        (Get-GceInstance -Project $project).Count -gt (Get-GceInstance -Project $project -Zone $zone).Count | Should Be $true
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GceInstance -Project "asdf" } | Should Throw "403"
    }

	Context "Object Transformer" {
		$projectObj = New-Object Google.Apis.Compute.v1.Data.Project
		$projectObj.Name = $project
		$zoneObj = New-Object Google.Apis.Compute.v1.Data.Zone
		$zoneObj.Name = $zone
		$instanceObj = New-Object Google.Apis.Compute.v1.Data.Instance
		$instanceObj.Name = $existantInstance
		
		It "should get one" {
			$instance = Get-GceInstance -Project $projectObj -Zone $zoneObj -Name $instanceObj
			$instance.Name | Should Be $existantInstance
			$instance.Kind | Should Be "compute#instance"
		}
	}
	
	gcloud compute instances stop $existantInstance
	gcloud compute instances stop $existantInstance2
	gcloud compute instances stop $existantInstance3 --zone $zone2

	gcloud compute instances delete $existantInstance
	gcloud compute instances delete $existantInstance2
	gcloud compute instances delete $existantInstance3 --zone $zone2

}
<#
Describe "Create-GceInstance" {

    # Should remove the bucket before/after each test to ensure we are in a good state.
    BeforeEach {
        gsutil rb gs://gcps-bucket-creation
    }

    AfterEach {
        gsutil rb gs://gcps-bucket-creation
    }

    It "should work" {
        $bucket = New-GceInstance -Name "gcps-bucket-creation" -Project $project
        $bucket.GetType().FullName | Should Match "Google.Apis.Storage.v1.Data.Bucket"
        $bucket.Location | Should Match "US"
        $bucket.StorageClass | Should Match "STANDARD"
    }

    It "supports Location and StorageClass parameters" {
        $bucket = New-GceInstance `
            -Name "gcps-bucket-creation" -Project $project `
            -Location EU -StorageClass NEARLINE
        $bucket.GetType().FullName | Should Match "Google.Apis.Storage.v1.Data.Bucket"
        $bucket.Location | Should Match "EU"
        $bucket.StorageClass | Should Match "NEARLINE"
    }
}

Describe "Remove-GceInstance" {
    $bucket = "gcps-bucket-removal"
    # Delete the test bucket before/after each test to ensure we are in a good state.
    BeforeEach {
        Create-TestBucket $project $bucket
    }

    # TODO(chrsmith): Confirm that the user gets prompted if -Force is not present.
    # TODO(chrsmith): Confirm that the -WhatIf prameter prompts the user, even if -Force is added.

    It "will work" {
        Remove-GceInstance -Name $bucket -Force
        { Get-GceInstance -Name $bucket } | Should Throw "404"
    }

    It "will fail to remove non-empty buckets" {
        Add-TestFile $bucket "file.txt"
        { Remove-GceInstance -Name $bucket -Force } | Should Throw "409"
    }

    It "will be unstoppable with the DeleteObjects flag" {
        # Place an object in the GCS bucket.
        Add-TestFile $bucket "file.txt"

        Remove-GceInstance -Name $bucket -DeleteObjects -Force
        { Get-GceInstance -Name $bucket } | Should Throw "404"
    }
}

Describe "Test-GceInstance" {

    It "will work" {
        # Our own bucket
        $bucket = "gcps-test-GceInstance"
        Create-TestBucket $project $bucket
        Test-GceInstance -Name $bucket | Should Be $true
        gsutil rb gs://gcps-test-GceInstance
      
        # Buckets that exists but we don't have access to.
        Test-GceInstance -Name "asdf" | Should Be $true

        Test-GceInstance -Name "yt4fm3blvo9shden" | Should Be $false
    }
}
#>