. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

# Both commands tested together.
Describe "Write-GcsBucketWebsite and Delete-GcsBucketWebsite" {
    It "should work" {
        $bucket = "gcps-website-testing"
        gsutil rb gs://$bucket
        gsutil mb -p $project gs://$bucket

        # Confirm not set by default.
        (Get-GcsBucket $bucket).Website | Should BeNullOrEmpty

        # Write, and confirm in output.
        $result = Write-GcsBucketWebsite `
            $bucket -MainPageSuffix "www.google.com" -NotFound "www.google.com/404"
        $result.Website.MainPageSuffix | Should BeExactly "www.google.com"
        $result.Website.NotFoundPage | Should BeExactly "www.google.com/404"

        # Confirm added
        $result = Get-GcsBucket $bucket
        $result.Website.MainPageSuffix | Should BeExactly "www.google.com"
        $result.Website.NotFoundPage | Should BeExactly "www.google.com/404"

        # Remove, and confirm not in output.
        $result = Remove-GcsBucketWebsite $bucket
        $result.Website.MainPageSuffix | Should BeNullOrEmpty
        $result.Website.NotFoundPage | Should BeNullOrEmpty

        # Confirm removed.
        $result = (Get-GcsBucket $bucket).Website
        $result.Website.MainPageSuffix | Should BeNullOrEmpty
        $result.Website.NotFoundPage | Should BeNullOrEmpty
    }
}
