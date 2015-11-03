. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

# TODO(chrsmith): When Posh updates, newer versions of Pester have Should BeOfType.
# TODO(chrsmith): Add a random suffix to bucket names to avoid collisions between devs.
Describe "Get-GcsBucket" {
	It "should fail to return non-existing buckets" {
		{ Get-GcsBucket -Name "gcps-bucket-no-exist" } | Should Throw "404"
	}
	It "should work" {
		gsutil mb -p gcloud-powershell-testing gs://gcps-testbucket
		(Get-GcsBucket -Name "gcps-testbucket").GetType().FullName | Should Match "Google.Apis.Storage.v1.Data.Bucket"
		# TODO(chrsmith): Verify the object's fields contain reasonable information.
		gsutil rb gs://gcps-testbucket
	}
	It "should contain ACL information" {
		(Get-GcsBucket -Project $project)[0].ACL.Length -gt 0 | Should Be $true
	}
	It "should list all buckets in a project" {
		(Get-GcsBucket -Project $project).Count -gt 0 | Should Be $true
	}
	It "should give access errors as appropriate" {
		# Don't know who created the "asdf" project and "asdf" bucket.
		{ Get-GcsBucket -Project "asdf" } | Should Throw "403"
		{ Get-GcsBucket -Name "asdf" } | Should Throw "403"
	}
}

Describe "Create-GcsBucket" {
	# Should remove the bucket before/after each test to ensure we are in a good state.
	BeforeEach {
		gsutil rb gs://gcps-bucket-creation
	}
	AfterEach {
		gsutil rb gs://gcps-bucket-creation
	}

	It "should work" {
		$bucket = New-GcsBucket -Name "gcps-bucket-creation" -Project $project
		$bucket.GetType().FullName | Should Match "Google.Apis.Storage.v1.Data.Bucket"
		$bucket.Location | Should Match "US"
		$bucket.StorageClass | Should Match "STANDARD"
	}

	It "supports Location and StorageClass parameters" {
		$bucket = New-GcsBucket `
		    -Name "gcps-bucket-creation" -Project $project `
		    -Location EU -StorageClass NEARLINE
		$bucket.GetType().FullName | Should Match "Google.Apis.Storage.v1.Data.Bucket"
		$bucket.Location | Should Match "EU"
		$bucket.StorageClass | Should Match "NEARLINE"
	}
}

Describe "Remove-GcsBucket" {
	# Delete the test bucket before/after each test to ensure we are in a good state.
	BeforeEach {
		gsutil rm gs://gcps-bucket-removal/*
		gsutil rb gs://gcps-bucket-removal
		gsutil mb -p $project gs://gcps-bucket-removal
	}

	# TODO(chrsmith): Confirm that the user gets prompted if -Force is not present.
	# TODO(chrsmith): Confirm that the -WhatIf prameter prompts the user, even if -Force is added.

	It "will work" {
		Remove-GcsBucket -Name "gcps-bucket-removal" -Force
		{ Get-GcsBucket -Name "gcps-bucket-removal" } | Should Throw "404"
	}

	It "will fail to remove non-empty buckets" {
		# Place an object in the GCS bucket.
		$filename = [System.IO.Path]::GetTempFileName()
		gsutil cp $filename gs://gcps-bucket-removal/file.txt
		Remove-Item -Force $filename

		{ Remove-GcsBucket -Name "gcps-bucket-removal" -Force } | Should Throw "409"
	}

	It "will be unstoppable with the DeleteObjects flag" {
		# Place an object in the GCS bucket.
		$filename = [System.IO.Path]::GetTempFileName()
		gsutil cp $filename gs://gcps-bucket-removal/file.txt
		Remove-Item $filename

		Remove-GcsBucket -Name "gcps-bucket-removal" -DeleteObjects -Force
		{ Get-GcsBucket -Name "gcps-bucket-removal" } | Should Throw "404"
	}
}

Describe "Test-GcsBucket" {
	It "will work" {
		# Our own bucket
		gsutil mb -p gcloud-powershell-testing gs://gcps-test-gcsbucket
		Test-GcsBucket -Name "gcps-test-gcsbucket" | Should Be $true
		gsutil rb gs://gcps-test-gcsbucket | Should Be $false
		# Buckets that exists but we don't have access to.
		Test-GcsBucket -Name "asdf" | Should Be $true

		Test-GcsBucket -Name "yt4fm3blvo9shden" | Should Be $false
	}
}
