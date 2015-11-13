. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

# Both commands tested together.
Describe "Write-GcsBucketLogging and Remove-GcsBucketLogging" {
    It "should work" {
        $bucket = "gcps-logging-testing"
        gsutil rb gs://$bucket
        gsutil mb -p $project gs://$bucket

        # Confirm not set by default.
        (Get-GcsBucket $bucket).Logging | Should BeNullOrEmpty

        # Write, and confirm in output.
        $result = Write-GcsBucketLogging `
            $bucket -LogBucket "gcloudps-alpha" -LogObjectPrefix "gcloudps-beta"
        $result.Logging.LogBucket | Should BeExactly "gcloudps-alpha"
        $result.Logging.LogObjectPrefix | Should BeExactly "gcloudps-beta"

        # Confirm added
        $result = Get-GcsBucket $bucket
        $result.Logging.LogBucket | Should BeExactly "gcloudps-alpha"
        $result.Logging.LogObjectPrefix | Should BeExactly "gcloudps-beta"

        # Remove, and confirm not in output.
        $result = Remove-GcsBucketLogging $bucket
        $result.Logging.LogBucket | Should BeNullOrEmpty
        $result.Logging.LogObjectPrefix | Should BeNullOrEmpty

        # Confirm removed.
        $result = Get-GcsBucket $bucket
        $result.Logging.LogBucket | Should BeNullOrEmpty
        $result.Logging.LogObjectPrefix | Should BeNullOrEmpty
    }
}
