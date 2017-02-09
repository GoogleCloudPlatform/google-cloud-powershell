. $PSScriptRoot\..\BigQuery\GcbqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcbqProject" {

	It "should reject non-positive values of maxResults" {
		{ Get-GcbqProject -MaxResults 0 } | Should Throw "MaxResults only takes positive numbers."
    }

	It "should handle overly large values of PageToken gracefully" {
        $response = Get-GcbqProject -PageToken 5555
		$response.Projects.Count | Should Be 0
    }

	It "should return the list of projects that the user has permissions to view" {
		$batches = Get-GcbqProject
		$batches.Count | Should BeGreaterThan 1
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
