. $PSScriptRoot\..\BigQuery\BqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$folder = Resolve-Path $PSScriptRoot

Describe "Get-BqTable" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
        $test_set | New-BqTable "my_table" -Name "my_table" -Description "Test data table"
        $test_set | New-BqTable "my_other_table" -Name "my_other_table" -Description "Another test table"
    }

    It "should list tables from a dataset object from pipeline"{
        $tables = Get-BqDataset $datasetName | Get-BqTable
        $tables.Count | Should BeGreaterThan 0
    }

    It "should list tables from a dataset object via parameter"{
        $dataset = Get-BqDataset $datasetName 
        $tables = Get-BqTable -Dataset $dataset
        $tables.Count | Should BeGreaterThan 0
    }

    It "should list tables from a dataset ID by parameter"{
        $tables = Get-BqTable -DatasetId $datasetName
        $tables.Count | Should BeGreaterThan 0
    }

    It "should get a singular table with a dataset object from pipeline"{
        $table = Get-BqDataset $datasetName | Get-BqTable "my_table"
        $table.TableReference.TableId | Should be "my_table"
        $table.Description | Should Be "Test data table"
    }

    It "should get a singular table with a dataset object via parameter"{
        $dataset = Get-BqDataset $datasetName
        $table = Get-BqTable -Dataset $dataset "my_other_table"
        $table.TableReference.TableId | Should be "my_other_table"
        $table.Description | Should Be "Another test table"
    }

    It "should get a singular table with a dataset ID by parameter"{
        $table = Get-BqTable -DatasetId $datasetName "my_table"
        $table.TableReference.TableId | Should be "my_table"
        $table.Description | Should Be "Test data table"
    }

    It "should throw when the table is not found"{
        { Get-BqTable $nonExistTable -DatasetId $datasetName } | Should Throw "404"
    }

    It "should throw when the dataset is not found"{
        { Get-BqTable $nonExistTable -DatasetId $nonExistDataset } | Should Throw "404"
    }

    It "should throw when the project is not found"{
        { Get-BqTable $nonExistTable -DatasetId $datasetName -Project $nonExistProject } | Should Throw "404"
    }

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Describe "Set-BqTable" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
    }

    It "should update trivial metadata fields via pipeline" {
        try {
            New-BqTable "pipe_table" -Dataset $test_set -Name "test" -Description "my table"
            $table = Get-BqTable "pipe_table" -Dataset $test_set
            $table.FriendlyName = "Some Test Data"
            $table.Description = "A new description!"
            $table | Set-BqTable

            $table = Get-BqTable "pipe_table" -Dataset $test_set
            $table | Should Not BeNullOrEmpty
            $table.FriendlyName | Should Be "Some Test Data"
            $table.Description | Should Be "A new description!"
        } finally {
            Remove-BqTable "pipe_table" -Dataset $test_set
        }
    }

    It "should update trivial metadata fields via parameter" {
        try {
            New-BqTable "param_table" -DatasetId $datasetName -Name "test" -Description "my table"
            $table = Get-BqTable "param_table" -DatasetId $datasetName 
            $table.FriendlyName = "Some Test Data"
            $table.Description = "A new description!"
            Set-BqTable $table

            $table = Get-BqTable "param_table" -DatasetId $datasetName 
            $table | Should Not BeNullOrEmpty
            $table.FriendlyName | Should Be "Some Test Data"
            $table.Description | Should Be "A new description!"
        } finally {
            Remove-BqTable "param_table" -Dataset $test_set 
        }
    }

    It "should not overwrite resources if a set request is malformed" {
        try {
            New-BqTable "tab_overwrite" -DatasetId $datasetName -Name "Testdata" -Description "Some interesting data!"
            $table = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Table
            $table.TableReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.TableReference
            { Set-BqTable $table } | Should Throw "is missing"
        } finally {
            Remove-BqTable "tab_overwrite" -Dataset $test_set 
        }
    }

    It "should insert the table if it does not already exist" {
        $table = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Table
        $table.TableReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.TableReference
        $table.TableReference.ProjectId = $project
        $table.TableReference.DatasetId = $datasetName
        $table.TableReference.TableId = "not_gonna_happen_today"
        $new = Set-BqTable $table
        $new.TableReference.TableId | Should Be "not_gonna_happen_today"
    } 

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Describe "New-BqTable" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
    }

    It "should take strings, name, description, and time to make a table"{
        $table = New-BqTable "my_table_str" -DatasetId $test_set.DatasetReference.DatasetId `
            -Project $test_set.DatasetReference.ProjectId -Name "CSV" -Description "Some Comma Separated Values"
        $table.TableReference.TableId | Should Be "my_table_str"
        $table.TableReference.DatasetId | Should Be $test_set.DatasetReference.DatasetId
        $table.TableReference.ProjectId | Should Be $test_set.DatasetReference.ProjectId
        $table.FriendlyName | Should Be "CSV"
        $table.Description | Should Be "Some Comma Separated Values"
    }

    It "should take a Dataset, name, description, and time to make a table"{
        $table = New-BqTable "my_table_ds" -Dataset $test_set -Name "CSV" `
            -Description "Some Comma Separated Values"
        $table.TableReference.TableId | Should Be "my_table_ds"
        $table.TableReference.DatasetId | Should Be $test_set.DatasetReference.DatasetId
        $table.TableReference.ProjectId | Should Be $test_set.DatasetReference.ProjectId
        $table.FriendlyName | Should Be "CSV"
        $table.Description | Should Be "Some Comma Separated Values"
    }

    It "should take a DatasetReference name, description, and time to make a table"{
        $table = New-BqTable "my_table_dr" -Dataset $test_set.DatasetReference `
            -Name "CSV" -Description "Some Comma Separated Values"
        $table.TableReference.TableId | Should Be "my_table_dr"
        $table.TableReference.DatasetId | Should Be $test_set.DatasetReference.DatasetId
        $table.TableReference.ProjectId | Should Be $test_set.DatasetReference.ProjectId
        $table.FriendlyName | Should Be "CSV"
        $table.Description | Should Be "Some Comma Separated Values"
    }

    It "should accept a table object from pipeline"{
        $tab = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Table
        $tab.TableReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.TableReference
        $tab.TableReference.TableId = "my_table_pipe"
        $tab.TableReference.DatasetId = $datasetName
        $tab.TableReference.ProjectId = $project
        $tab.FriendlyName = "PipeTest"
        $tab.Description = "Some cool stuff in a table"
        $newtab = $tab | New-BqTable
        $newtab | Should Not BeNullOrEmpty
        $newtab.TableReference.TableId | Should Be "my_table_pipe"
        $newtab.TableReference.DatasetId | Should Be $datasetName
        $newtab.FriendlyName | Should Be "PipeTest"
        $newtab.Description | Should Be "Some cool stuff in a table"
    }

    It "should accept a more complex table object from pipeline"{
        $tab = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Table
        $tab.TableReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.TableReference
        $tab.TableReference.TableId = "my_table_pipeComplex"
        $tab.TableReference.DatasetId = $datasetName
        $tab.TableReference.ProjectId = $project
        $tab.FriendlyName = "PipeTest!@#456><"
        $tab.Description = "Some cool stuff in a table?!@>><#'()*&^%"
        $newtab = $tab | New-BqTable
        $newtab | Should Not BeNullOrEmpty
        $newtab.TableReference.TableId | Should Be "my_table_pipeComplex"
        $newtab.TableReference.DatasetId | Should Be $datasetName
        $newtab.FriendlyName | Should Be "PipeTest!@#456><"
        $newtab.Description | Should Be "Some cool stuff in a table?!@>><#'()*&^%"
    }

    It "should set schema properly"{
        $schema = New-BqSchema -JSON `
            '[{"Name":"Title","Type":"STRING"},{"Name":"Author","Type":"STRING"},{"Name":"Year","Type":"INTEGER"}]' | Set-BqSchema
        $table = New-BqTable "my_table_schema" -Dataset $test_set -Schema $schema
        $table.Schema.Fields[0].Name | Should Be "Title"
        $table.Schema.Fields[1].Name | Should Be "Author"
        $table.Schema.Fields[2].Name | Should Be "Year"
    }

    It "should properly set the duration of time for which the tables last" {
        $expireInSec = 3600
        $table = New-BqTable "my_table_duration" -DatasetId $datasetName -Expiration $expireInSec
        $calculatedTime = [int64](([datetime]::UtcNow)-(get-date "1/1/1970")).TotalMilliseconds
        $table.ExpirationTime | Should BeLessThan ($calculatedTime + (($expireInSec + 5) * 1000))
    }

    It "should throw when there is already a table with the same ID"{
        New-BqTable "my_table_repeat" -DatasetId $datasetName 
        { New-BqTable "my_table_repeat" -DatasetId $datasetName } | Should Throw "409"
    }

    It "should throw when the dataset is not found"{
        { New-BqTable "my_table_d404" -DatasetId $nonExistDataset } | Should Throw "404"
    }

    It "should throw when the project is not found"{
        { New-BqTable "my_table_p404" -DatasetId $datasetName -project $nonExistProject} | Should Throw "404"
    }

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Describe "Remove-BqTable" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
    }

    It "should not delete the table if -WhatIf is specified" {
        $table = New-BqTable "table_if" -Dataset $test_set 
        $table | Remove-BqTable -WhatIf
        $remainder = Get-BqTable "table_if" -Dataset $test_set 
        $remainder.TableReference.TableId | Should Be "table_if"
        Get-BqTable "table_if" -Dataset $test_set | Remove-BqTable
    }
    
    It "should delete an empty table from the pipeline with no -Force" {
        New-BqTable "table_empty_pipe" -Dataset $test_set
        Get-BqTable "table_empty_pipe" -Dataset $test_set | Remove-BqTable
        { Get-BqTable "table_empty_pipe" -Dataset $test_set } | Should Throw 404
    }

    It "should delete an empty table from an argument with no -Force" {
        New-BqTable "table_empty_arg" -Dataset $test_set 
        $table = Get-BqTable "table_empty_arg" -Dataset $test_set 
        Remove-BqTable $table
        { Get-BqTable "table_empty_arg" -Dataset $test_set } | Should Throw 404
    }

    It "should delete a table by value with explicit project" {
        New-BqTable "table_explicit" -Project $project -DatasetId $datasetName
        Remove-BqTable "table_explicit" -Project $project -DatasetId $datasetName
        { Get-BqTable "table_explicit" -Dataset $test_set } | Should Throw 404
    }

    It "should delete a nonempty table as long as -Force is specified" {
        $filename_csv = "$folder\classics.csv"
        $schema = New-BqSchema -Name "Title" -Type "STRING" | New-BqSchema -Name "Author" -Type "STRING" |
                  New-BqSchema -Name "Year" -Type "INTEGER" | Set-BqSchema
        $table = New-BqTable "table_force_delete" -Dataset $test_set -Schema $schema
        $table | Add-BqTableRow CSV $filename_csv -SkipLeadingRows 1
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
        Get-BqTable "table_force_delete" -Dataset $test_set | Remove-BqTable -Force
        { Get-BqTable "table_force_delete" -Dataset $test_set } | Should Throw 404
    }

    It "should handle when a table does not exist" {
        $table = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Table
        $table.TableReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.TableReference
        $table.TableReference.DatasetId = $test_set
        $table.TableReference.ProjectId = $project
        $table.TableReference.TableId = "table_not_actually_there_for_some_reason"
        { Remove-BqTable $table } | Should Throw 404
    }

    It "should handle projects that do not exist" {
        { Remove-BqTable "table" -Project $nonExistProject -DatasetId $nonExistDataset } | Should Throw 404
    }

    It "should handle project:dataset combinations that do not exist" {
        { Remove-BqTable "table" -Project $project -DatasetId $nonExistDataset } | Should Throw 404
    }

    It "should handle projects that the user does not have permissions for" {
        { Remove-BqTable "table" -Project $accessErrProject -DatasetId $nonExistDataset } | Should Throw 400
    }

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
