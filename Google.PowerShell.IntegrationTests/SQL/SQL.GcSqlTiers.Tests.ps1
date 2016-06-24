. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

Describe "Get-GcSqlFlags" {

    It "should have the correct number of tiers" {
        $tiers = Get-GcSqlTiers -Project $project
        #There are 18 tiers available for our Google Cloud SQL Project.
        $tiers."TierValue".Length | Should be 18
    }

    It "should have the correct flags" {
        $tiers = Get-GcSqlTiers -Project $project
        $tiers."TierValue" -contains "D0"
        $tiers."TierValue" -contains "D4"
    }
}