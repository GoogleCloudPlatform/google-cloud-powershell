. $PSScriptRoot\..\BigQuery\GcbqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcbqDataset" {

    It "should be true that 1 == 1"{
        1 | Should Be 1
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
