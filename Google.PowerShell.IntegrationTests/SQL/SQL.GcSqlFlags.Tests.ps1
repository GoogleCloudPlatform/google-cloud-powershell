. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

Describe "Get-GcSqlFlags" {

    It "should have the correct number of flags" {
        $flags = Get-GcSqlFlags
        #There are 38 flags available for Google Cloud SQL Instances.
        $flags.Length | Should BeGreaterThan 37

    }

    It "should have the correct flags" {
        $flags = Get-GcSqlFlags
        ($flags.Name -contains "log_output") | Should Be true
        ($flags.Name -contains "group_concat_max_len") | Should Be true

    }
}
