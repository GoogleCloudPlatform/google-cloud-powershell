. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
$project, $_, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcSqlOperations" {

    gcloud sql instances create "test-ops" --quiet 2>$null
    $instance = "test-ops"

    It "should get a reasonable response" {
        $operations = Get-GcSqlOperation -Project $project -Instance $instance
        ($operations.operationType -contains "Create") | Should Be true
    }

    It "shouldn't need the project parameter if configuration is set up correctly" {
        $operations = Get-GcSqlOperation -Instance $instance
        ($operations.operationType -contains "Create") | Should Be true
    }

	It "should get a reasonable response from a given query" {
        $firstOp = Get-GcSqlOperation -Instance $instance | Select-Object -last 1
        $operation = Get-GcSqlOperation -Project $project -Name $firstOp.name
        $operation.operationType | Should Be "Create"
    }

    It "should compound with the list parameter set" {
        $operations = Get-GcSqlOperation -Project $project -Instance $instance
        $firstOperation = $operations | Select-Object -first 1
        $operationName = $firstOperation.name
        $operation = Get-GcSqlOperation -Project $project -Name $operationName
        $operation.name | Should Be $firstOperation.name
        $operation.operationType | Should Be $firstOperation.operationType
    }

    It "shouldn't require Project to be passed in to work" {
        $firstOp = Get-GcSqlOperation -Instance $instance | Select-Object -last 1
        $operation = Get-GcSqlOperation -Name $firstOp.name
        $operation.operationType | Should Be "Create"
    }

    gcloud sql instances delete "test-ops" --quiet 2>$null
}
Reset-GCloudConfig $oldActiveConfig $configName
