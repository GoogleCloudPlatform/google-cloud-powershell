. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

Describe "Get-GcSqlFlags" {

    It "should have the correct number of tiers" {
        $tiers = Get-GcSqlTiers -Project $project
        # As of June, 2016 there are 18 tiers available for Google Cloud SQL.
        $tiers.TierValue.Length | Should BeGreaterThan 17
    }

    It "should have the correct tiers" {
        $tiers = Get-GcSqlTiers -Project $project
        ($tiers.TierValue -contains "D0") | Should Be true
        ($tiers.TierValue -contains "D4") | Should Be true
    }
}
