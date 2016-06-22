. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

Describe "Get-GcSqlFlags" {

	It "should work" {
        $flags = Get-GcSqlFlags
		#There are 38 flags available for Google Cloud SQL Instances.
        $flags.Length | Should be 38

    }

}
