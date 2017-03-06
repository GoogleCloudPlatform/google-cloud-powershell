. $PSScriptRoot\..\BigQuery\BqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

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
        { Get-BqTable -DatasetId $datasetName $nonExistTable -ErrorAction Stop} | Should Throw "404"
    }

    It "should throw when the dataset is not found"{
        { Get-BqTable -DatasetId $nonExistDataset } | Should Throw "404"
    }

    It "should throw when the project is not found"{
        { Get-BqTable -project $nonExistProject -DatasetId $datasetName} | Should Throw "404"
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
            New-BqTable -Dataset $test_set "pipe_table" -Name "test" -Description "my table"
            $table = Get-BqTable -Dataset $test_set "pipe_table"
            $table.FriendlyName = "Some Test Data"
            $table.Description = "A new description!"
            $table | Set-BqTable

            $table = Get-BqTable -Dataset $test_set "pipe_table"
            $table | Should Not BeNullOrEmpty
            $table.FriendlyName | Should Be "Some Test Data"
            $table.Description | Should Be "A new description!"
        } finally {
            Remove-BqTable -Dataset $test_set "pipe_table"
        }
    }

    It "should update trivial metadata fields via parameter" {
        try {
            New-BqTable -DatasetId $datasetName "param_table" -Name "test" -Description "my table"
            $table = Get-BqTable -DatasetId $datasetName "param_table"
            $table.FriendlyName = "Some Test Data"
            $table.Description = "A new description!"
            Set-BqTable -InputObject $table

            $table = Get-BqTable -DatasetId $datasetName "param_table"
            $table | Should Not BeNullOrEmpty
            $table.FriendlyName | Should Be "Some Test Data"
            $table.Description | Should Be "A new description!"
        } finally {
            Remove-BqTable -Dataset $test_set "param_table"
        }
    }

    It "should not overwrite resources if a set request is malformed" {
        try {
            New-BqTable -DatasetId $datasetName "tab_overwrite" -Name "Testdata" -Description "Some interesting data!"
            $table = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Table
            $table.TableReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.TableReference
            { Set-BqTable -InputObject $table } | Should Throw "is missing"
        } finally {
            Remove-BqTable -Dataset $test_set "tab_overwrite"
        }
    }

    It "should not update a table that does not exist" {
        $table = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Table
        $table.TableReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.TableReference
        $table.TableReference.ProjectId = $project
        $table.TableReference.DatasetId = $datasetName
        $table.TableReference.TableId = "not_gonna_happen_today"
        { Set-BqTable -InputObject $table } | Should Throw 404
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
        $table = New-BqTable -Project $test_set.DatasetReference.ProjectId `
            -DatasetId $test_set.DatasetReference.DatasetId "my_table_str" `
            -Name "CSV" -Description "Some Comma Separated Values"
        $table.TableReference.TableId | Should Be "my_table_str"
        $table.TableReference.DatasetId | Should Be $test_set.DatasetReference.DatasetId
        $table.TableReference.ProjectId | Should Be $test_set.DatasetReference.ProjectId
        $table.FriendlyName | Should Be "CSV"
        $table.Description | Should Be "Some Comma Separated Values"
    }

    It "should take a Dataset, name, description, and time to make a table"{
        $table = New-BqTable -Dataset $test_set "my_table_ds" -Name "CSV" `
            -Description "Some Comma Separated Values"
        $table.TableReference.TableId | Should Be "my_table_ds"
        $table.TableReference.DatasetId | Should Be $test_set.DatasetReference.DatasetId
        $table.TableReference.ProjectId | Should Be $test_set.DatasetReference.ProjectId
        $table.FriendlyName | Should Be "CSV"
        $table.Description | Should Be "Some Comma Separated Values"
    }

    It "should take a DatasetReference name, description, and time to make a table"{
        $table = New-BqTable -Dataset $test_set.DatasetReference "my_table_dr" `
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

    It "should properly set the duration of time for which the tables last" {
        $expireInSec = 3600
        $table = New-BqTable -DatasetId $datasetName "my_table_duration" -Expiration $expireInSec
        $calculatedTime = [int64](([datetime]::UtcNow)-(get-date "1/1/1970")).TotalMilliseconds
        $table.ExpirationTime | Should BeLessThan ($calculatedTime + (($expireInSec + 5) * 1000))
    }

    It "should throw when there is already a table with the same ID"{
        New-BqTable "my_table_repeat" -DatasetId $datasetName 
        { New-BqTable "my_table_repeat" -DatasetId $datasetName -ErrorAction Stop } | Should Throw "409"
    }

    It "should throw when the dataset is not found"{
        { New-BqTable "my_table_d404" -DatasetId $nonExistDataset } | Should Throw "404"
    }

    It "should throw when the project is not found"{
        { New-BqTable -DatasetId $datasetName "my_table_p404" -project $nonExistProject} | Should Throw "404"
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
        $table = New-BqTable -Dataset $test_set -Table "table_if"
        $table | Remove-BqTable -WhatIf
        $remainder = Get-BqTable -Dataset $test_set "table_if"
        $remainder.TableReference.TableId | Should Be "table_if"
        Get-BqTable -Dataset $test_set "table_if" | Remove-BqTable
    }
    
    It "should delete an empty table from the pipeline with no -Force" {
        New-BqTable -Dataset $test_set -Table "table_empty_pipe"
        Get-BqTable -Dataset $test_set "table_empty_pipe" | Remove-BqTable
        { Get-BqTable -Dataset $test_set "table_empty_pipe" -ErrorAction Stop } | Should Throw 404
    }

    It "should delete an empty table from an argument with no -Force" {
        New-BqTable -Dataset $test_set "table_empty_arg"
        $table = Get-BqTable -Dataset $test_set "table_empty_arg" 
        Remove-BqTable -InputObject $table
        { Get-BqTable -Dataset $test_set "table_empty_arg" -ErrorAction Stop } | Should Throw 404
    }

    It "should delete a table by value with explicit project" {
        New-BqTable -Project $project -DatasetId $datasetName -Table "table_explicit"
        Remove-BqTable -Project $project -DatasetId $datasetName -Table "table_explicit"
        { Get-BqTable -Dataset $test_set "table_explicit" -ErrorAction Stop } | Should Throw 404
    }

    #TODO(ahandley): It "should delete a nonempty table as long as -Force is specified" #needs set-tabledata

    It "should handle when a table does not exist" {
        $table = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Table
        $table.TableReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.TableReference
        $table.TableReference.DatasetId = $test_set
        $table.TableReference.ProjectId = $project
        $table.TableReference.TableId = "table_not_actually_there_for_some_reason"
        { Remove-BqTable -InputObject $table -ErrorAction Stop } | Should Throw 404
    }

    It "should handle projects that do not exist" {
        { Remove-BqTable -Project $nonExistProject -DatasetId $nonExistDataset `
            -Table "table" -ErrorAction Stop } | Should Throw 404
    }

    It "should handle project:dataset combinations that do not exist" {
        { Remove-BqTable -Project $project -DatasetId $nonExistDataset `
            -Table "table" -ErrorAction Stop } | Should Throw 404
    }

    It "should handle projects that the user does not have permissions for" {
        { Remove-BqTable -Project $accessErrProject -DatasetId $nonExistDataset `
            -Table "table" -ErrorAction Stop } | Should Throw 400
    }

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
