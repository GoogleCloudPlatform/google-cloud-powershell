. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

# Both commands tested together.
Describe "{Delete, Write}-GcsBucketWebsite" {
    It "should work" {
        $bucketName = "gcps-logging-testing"
        Remove-GcsBucket $bucketName -Force
        $bucket = New-GcsBucket $bucketName

        # Confirm not set by default.
        $bucket.Website | Should BeNullOrEmpty

        # Write, and confirm in output.
        $result = Write-GcsBucketWebsite `
            $bucket -MainPageSuffix "www.google.com" -NotFound "www.google.com/404"
        $result.Website.MainPageSuffix | Should BeExactly "www.google.com"
        $result.Website.NotFoundPage | Should BeExactly "www.google.com/404"

        # Confirm added.
        $result = Get-GcsBucket $bucketName
        $result.Website.MainPageSuffix | Should BeExactly "www.google.com"
        $result.Website.NotFoundPage | Should BeExactly "www.google.com/404"

        # Remove, and confirm not in output.
        $result = Remove-GcsBucketWebsite $bucket
        $result.Website.MainPageSuffix | Should BeNullOrEmpty
        $result.Website.NotFoundPage | Should BeNullOrEmpty

        # Confirm removed.
        $result = (Get-GcsBucket $bucketName).Website
        $result.Website.MainPageSuffix | Should BeNullOrEmpty
        $result.Website.NotFoundPage | Should BeNullOrEmpty

        Remove-GcsBucket $bucketName -Force
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
