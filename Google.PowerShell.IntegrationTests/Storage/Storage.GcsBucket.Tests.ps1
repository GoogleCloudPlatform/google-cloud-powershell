. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

# TODO(chrsmith): When Posh updates, newer versions of Pester have Should BeOfType.
# TODO(chrsmith): Add a random suffix to bucket names to avoid collisions between devs.
Describe "Get-GcsBucket" {
    # This is another test project that cloudsharp-eng@google.com has OWNER access.
    $script:additionalTestingProject = "quoct-test-project"

    # This service account has EDITOR access to the $additionalTestingProject project.
    $script:serviceAccountInOtherTestProject = "appveyorci-testing@gcloud-powershell-testing.iam.gserviceaccount.com"

    # This is a bucket that exists in $additionalTestingProject. The $serviceAccountInOtherTestProject has
    # permission to access this bucket.
    $script:bucketInOtherTestProject = "gcloud-powershell-additional-bucket"

    # This contains the key to set up the service account $serviceAccountInOtherTestProject
    $script:additionalKey = Resolve-Path "$PSScriptRoot\..\AdditionalServiceAccountCredentials.json" -ErrorAction Ignore

    # Skip the service account test if we are not running in AppVeyor. This is because the key $additionalKey is only
    # decrypted in AppVeyor. If we are in AppVeyor running this test, the test_folder variable is set to Storage (in appveyor.yml).
    $script:skipServiceAccountTest = $env:test_folder -eq "Storage"

    It "should fail to return non-existing buckets" {
        { Get-GcsBucket -Name "gcps-bucket-no-exist" } | Should Throw "'gcps-bucket-no-exist' does not exist"
    }

    It "should work" {
        gsutil mb -p gcloud-powershell-testing gs://gcps-testbucket 2>$null
        $bucket = Get-GcsBucket -Name "gcps-testbucket"
        $bucket.GetType().FullName | Should Match "Google.Apis.Storage.v1.Data.Bucket"

        $bucket.StorageClass | Should Match "STANDARD"
        $bucket.Id | Should Match "gcps-testbucket"
        $bucket.SelfLink | Should Match "https://www.googleapis.com/storage/v1/b/gcps-testbucket"

        gsutil rb gs://gcps-testbucket 2>$null
    }

    It "should take into account changes in the Cloud SDK, including environment variables" -Skip:$skipServiceAccountTest {
        if ($null -eq $additionalKey -or (-not (Test-Path $additionalKey))) {
            throw "Cannot find service account credential for project '$additionalTestingProject'"
        }

        try {
            gcloud auth activate-service-account --key-file="$additionalKey" 2>$null
            # Should throw 403 because the project is still "gcloud-powershell-testing"
            { Get-GcsBucket } | Should Throw "403"
            # This will set the project to quoct-test-project
            $env:CLOUDSDK_CORE_PROJECT = $additionalTestingProject
            $bucket = Get-GcsBucket $bucketInOtherTestProject
            $bucket.Name | Should Match $bucketInOtherTestProject

            # Now we change the project back
            $env:CLOUDSDK_CORE_PROJECT = $null
            gcloud config set account $serviceAccountInOtherTestProject 2>$null
            $project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
            { Get-GcsBucket $bucketInOtherTestProject } | Should Throw "403"
        }
        finally {
            # If this is true then the test did not call Set-GCloudConfig.
            if ($project -eq $additionalTestingProject) {
                $project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
            }
        }
    }

    It "should contain ACL information" -Pending {
        (Get-GcsBucket -Project $project)[0].Acl.Count -gt 0 | Should Be $true
    }

    It "should list all buckets in a project" {
        $buckets = Get-GcsBucket -Project $project
        $buckets | Should Not BeNullOrEmpty
        ($buckets | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Storage.v1.Data.Bucket }
    }

    It "should list all buckets in the default project" {
        $buckets = Get-GcsBucket
        $buckets | Should Not BeNullOrEmpty
        ($buckets | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Storage.v1.Data.Bucket }
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project and "asdf" bucket.
        { Get-GcsBucket -Project "asdf" } | Should Throw "403"
        { Get-GcsBucket -Name "asdf" } | Should Throw "403"
    }
}

Describe "New-GcsBucket" {

    # Should remove the bucket before/after each test to ensure we are in a good state.
    BeforeEach {
        gsutil -m rm -r "gs://gcps-bucket-creation/*" 2>$null
        gsutil rb gs://gcps-bucket-creation 2>$null
    }

    AfterEach {
        gsutil rb gs://gcps-bucket-creation 2>$null
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

    It "supports Coldline storage class" {
        $bucket = New-GcsBucket -Name "gcps-bucket-creation" `
                                -Project $project `
                                -StorageClass COLDLINE
        $bucket.GetType().FullName | Should Match "Google.Apis.Storage.v1.Data.Bucket"
        $bucket.StorageClass | Should Match "COLDLINE"
    }

    It "supports Multi Regional storage class" {
        $bucket = New-GcsBucket -Name "gcps-bucket-creation" `
                                -Project $project `
                                -StorageClass MULTI_REGIONAL
        $bucket.GetType().FullName | Should Match "Google.Apis.Storage.v1.Data.Bucket"
        $bucket.StorageClass | Should Match "MULTI_REGIONAL"
    }

    It "supports Durable Reduced Availability storage class" {
        $bucket = New-GcsBucket -Name "gcps-bucket-creation" `
                                -Project $project `
                                -StorageClass DURABLE_REDUCED_AVAILABILITY
        $bucket.GetType().FullName | Should Match "Google.Apis.Storage.v1.Data.Bucket"
        $bucket.StorageClass | Should Match "DURABLE_REDUCED_AVAILABILITY"
    }

    It "throws error for Regional storage class" {
        # This test will throw error because gcloud-powershell-testing has Multi-Regional Storage
        # and so it does not support creating Regional buckets.
        { New-GcsBucket -Name "gcps-bucket-creation" `
                                -Project $project `
                                -StorageClass REGIONAL } | Should Throw "not supported"
    }

    It "supports setting default ACLs" {
        # "authenticatedRead" means it is only accessible to users authenticated with a
        # Google address. So a blind HTTP GET won't work, but if you have the right cookies
        # it will.
        $bucket = New-GcsBucket `
            -Name "gcps-bucket-creation" `
            -DefaultObjectAcl "authenticatedRead"

        $bucket.DefaultObjectAcl.Entity | Should Be "allAuthenticatedUsers"
        $bucket.DefaultObjectAcl.Role | Should Be "READER"

        $gcsObj = "testing 1, 2, 3..." | New-GcsObject $bucket "test-obj"

        # General requests to the object should fail.
        { Invoke-WebRequest https://www.googleapis.com/storage/v1/b/gcps-bucket-creation/o/test-obj } |
            Should Throw "(401) Unauthorized"
        # But going through gsutil (which passes along your Google credentials) will work.
        gsutil cat gs://gcps-bucket-creation/test-obj | Should Be "testing 1, 2, 3..."
    }
}

Describe "Remove-GcsBucket" {
    $bucket = "gcps-bucket-removal"
    $bucketNotExistMsg = "'$bucket' does not exist"
    # Delete the test bucket before/after each test to ensure we are in a good state.
    BeforeEach {
        Create-TestBucket $project $bucket
    }

    # TODO(chrsmith): Confirm that the user gets prompted if -Force is not present.
    # TODO(chrsmith): Confirm that the -WhatIf prameter prompts the user, even if -Force is added.

    It "will work" {
        Remove-GcsBucket -Name $bucket -Force
        { Get-GcsBucket -Name $bucket } | Should Throw $bucketNotExistMsg
    }

    It "will work with pipeline" {
        $bucket | Remove-GcsBucket -Force
        { Get-GcsBucket -Name $bucket } | Should Throw $bucketNotExistMsg

        # Also passing a Bucket object.
        $bucketObj = New-GcsBucket "gcps-bucket-removal2"
        $bucketObj | Remove-GcsBucket -Force
        { Get-GcsBucket -Name $bucketObj.Name } | Should Throw "'gcps-bucket-removal2' does not exist."
    }

    It "will be unstoppable with the Force flag" {
        # Place an object in the GCS bucket.
        Add-TestFile $bucket "file.txt"

        Remove-GcsBucket -Name $bucket -Force
        { Get-GcsBucket -Name $bucket } | Should Throw $bucketNotExistMsg
    }
}

Describe "Test-GcsBucket" {

    It "will work" {
        # Our own bucket
        $bucket = "gcps-test-gcsbucket"
        Create-TestBucket $project $bucket
        Test-GcsBucket -Name $bucket | Should Be $true
        Remove-GcsBucket $bucket

        # Test using a Bucket object.
        $bucketObj = New-GcsBucket "gcps-test-gcsbucket"
        Test-GcsBucket $bucketObj | Should Be $true
        Remove-GcsBucket $bucketObj
      
        # Buckets that exists but we don't have access to.
        Test-GcsBucket -Name "asdf" | Should Be $true
        Test-GcsBucket -Name "yt4fm3blvo9shden" | Should Be $false
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
