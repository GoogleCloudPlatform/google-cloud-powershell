. $PSScriptRoot\..\BigQuery\GcbqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcbqProject" {

	It "should return the list of projects that the user has permissions to view" {
		$batches = Get-GcbqProject
		$batches.Count | Should BeGreaterThan 1
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
