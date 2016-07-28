. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

# TODO(chrsmith): When Posh updates, newer versions of Pester have Should BeOfType.
# TODO(chrsmith): Add a random suffix to bucket names to avoid collisions between devs.
Describe "Get-GcsBucket" {

    It "should fail to return non-existing buckets" {
        { Get-GcsBucket -Name "gcps-bucket-no-exist" } | Should Throw "404"
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

    It "should contain ACL information" {
        (Get-GcsBucket -Project $project)[0].ACL.Length -gt 0 | Should Be $true
    }

    It "should list all buckets in a project" {
        $buckets = Get-GcsBucket -Project $projec
        $buckets | Should Not BeNullOrEmpty
        ($buckets | Get-Member).TypeName | Should Be Google.Apis.Storage.v1.Data.Bucket
    }

    It "should list all buckets in the default project" {
        $buckets = Get-GcsBucket
        $buckets | Should Not BeNullOrEmpty
        ($buckets | Get-Member).TypeName | Should Be Google.Apis.Storage.v1.Data.Bucket
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
    # Delete the test bucket before/after each test to ensure we are in a good state.
    BeforeEach {
        Create-TestBucket $project $bucket
    }

    # TODO(chrsmith): Confirm that the user gets prompted if -Force is not present.
    # TODO(chrsmith): Confirm that the -WhatIf prameter prompts the user, even if -Force is added.

    It "will work" {
        Remove-GcsBucket -Name $bucket -Force
        { Get-GcsBucket -Name $bucket } | Should Throw "404"
    }

    It "will work with pipeline" {
        $bucket | Remove-GcsBucket -Force
        { Get-GcsBucket -Name $bucket } | Should Throw "404"

        # Also passing a Bucket object.
        $bucketObj = New-GcsBucket "gcps-bucket-removal2"
        $bucketObj | Remove-GcsBucket -Force
        { Get-GcsBucket -Name $bucketObj.Name } | Should Throw "404"
    }

    It "will be unstoppable with the Force flag" {
        # Place an object in the GCS bucket.
        Add-TestFile $bucket "file.txt"

        Remove-GcsBucket -Name $bucket -Force
        { Get-GcsBucket -Name $bucket } | Should Throw "404"
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
