. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

# Both commands tested together.
Describe "Write-GcsBucketLogging and Remove-GcsBucketLogging" {
    It "should work" {
        $bucketName = "gcps-logging-testing"
        Remove-GcsBucket $bucketName -Force
        $bucket = New-GcsBucket $bucketName

        # Confirm not set by default.
        $bucket.Logging | Should BeNullOrEmpty

        # Write, and confirm in output.
        $result = Write-GcsBucketLogging `
            $bucket -LogBucket "gcloudps-alpha" -LogObjectPrefix "gcloudps-beta"
        $result.Logging.LogBucket | Should BeExactly "gcloudps-alpha"
        $result.Logging.LogObjectPrefix | Should BeExactly "gcloudps-beta"

        # Confirm added
        $result = Get-GcsBucket $bucketName
        $result.Logging.LogBucket | Should BeExactly "gcloudps-alpha"
        $result.Logging.LogObjectPrefix | Should BeExactly "gcloudps-beta"

        # Remove, and confirm not in output.
        $result = Remove-GcsBucketLogging $bucket
        $result.Logging.LogBucket | Should BeNullOrEmpty
        $result.Logging.LogObjectPrefix | Should BeNullOrEmpty

        # Confirm removed.
        $result = Get-GcsBucket $bucketName
        $result.Logging.LogBucket | Should BeNullOrEmpty
        $result.Logging.LogObjectPrefix | Should BeNullOrEmpty

        Remove-GcsBucket $bucketName -Force
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
