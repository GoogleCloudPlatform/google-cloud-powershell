. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
 
$project = "gcloud-powershell-testing"

Describe "Get-GcSqlOperations" {

    It "should get a reasonable response" {
        $operations = Get-GcSqlOperation -Project $project -Instance "test-db"
        ($operations.operationType -contains "Create") | Should Be true
    }

	It "should get a reasonable response from a given query" {
        $operation = Get-GcSqlOperation -Project $project -Name "515a9bb2-fcad-4c62-a8c5-3cfb7cbabfa2"
        $operation.operationType | Should Be "Create"
    }

    It "should compound with the list parameter set" {
        $operations = Get-GcSqlOperation -Project $project -Instance "test-db"
        $firstOperation = $operations | Select-Object -first 1
        $operationName = $firstOperation.name
        $operation = Get-GcSqlOperation -Project $project -Name $operationName
        $operation.name | Should Be $firstOperation.name
        $operation.operationType | Should Be $firstOperation.operationType
    }

    It "shouldn't require Project to be passed in to work" {
        $operation = Get-GcSqlOperation -Name "515a9bb2-fcad-4c62-a8c5-3cfb7cbabfa2"
        $operation.operationType | Should Be "Create"
    }
}
