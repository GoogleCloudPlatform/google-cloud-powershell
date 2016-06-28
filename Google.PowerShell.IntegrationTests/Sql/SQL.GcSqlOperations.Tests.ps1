. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

Describe "Get-GcSqlOperations" {

    It "should get a reasonable response" {
        $operations = Get-GcSqlOperations -Project $project -Instance "test-db"
		($operations.operationType -contains "Create") | Should Be true
    }
}

Describe "Get-GcSqlOperation" {
    It "should get a reasonable response from a given query" {
        $operation = Get-GcSqlOperation -Project $project -OperationName "515a9bb2-fcad-4c62-a8c5-3cfb7cbabfa2"
		$operation.operationType | Should Be "Create"
    }

    It "should compound with Get-GcSqlOperations" {
        $operations = Get-GcSqlOperations -Project $project -Instance "test-db"
		$firstOperation = $operations | Select-Object -first 1
		$operationName = $firstOperation.name
		$operation = Get-GcSqlOperation -Project $project -OperationName $operationName
		$operation.name | Should Be $firstOperation.name
		$operation.operationType | Should Be $firstOperation.operationType
    }
}