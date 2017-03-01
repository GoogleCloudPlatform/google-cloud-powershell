. $PSScriptRoot\..\BigQuery\GcbqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcbqTable" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-GcbqDataset $datasetName
        $test_set | New-GcbqTable "my_table" -Name "my_table" -Description "Test data table"
        $test_set | New-GcbqTable "my_other_table" -Name "my_other_table" -Description "Another test table"
    }

    It "should list tables from a dataset object from pipeline"{
        $tables = Get-GcbqDataset $datasetName | Get-GcbqTable
        $tables.Count | Should BeGreaterThan 0
    }

    It "should list tables from a dataset object via parameter"{
        $dataset = Get-GcbqDataset $datasetName 
        $tables = Get-GcbqTable -Dataset $dataset
        $tables.Count | Should BeGreaterThan 0
    }

    It "should list tables from a dataset ID by parameter"{
        $tables = Get-GcbqTable -DatasetId $datasetName
        $tables.Count | Should BeGreaterThan 0
    }

    It "should get a singular table with a dataset object from pipeline"{
        $table = Get-GcbqDataset $datasetName | Get-GcbqTable "my_table"
        $table.TableReference.TableId | Should be "my_table"
        $table.Description | Should Be "Test data table"
    }

    It "should get a singular table with a dataset object via parameter"{
        $dataset = Get-GcbqDataset $datasetName
        $table = Get-GcbqTable -Dataset $dataset "my_other_table"
        $table.TableReference.TableId | Should be "my_other_table"
        $table.Description | Should Be "Another test table"
    }

    It "should get a singular table with a dataset ID by parameter"{
        $table = Get-GcbqTable -DatasetId $datasetName "my_table"
        $table.TableReference.TableId | Should be "my_table"
        $table.Description | Should Be "Test data table"
    }

    It "should throw when the table is not found"{
        { Get-GcbqTable -DatasetId $datasetName $nonExistTable -ErrorAction Stop} | Should Throw "404"
    }

    It "should throw when the dataset is not found"{
        { Get-GcbqTable -DatasetId $nonExistDataset } | Should Throw "404"
    }

    It "should throw when the project is not found"{
        { Get-GcbqTable -project $nonExistProject -DatasetId $datasetName} | Should Throw "404"
    }

    AfterAll {
        $test_set | Remove-GcbqDataset -Force
    }
}

Describe "New-GcbqTable" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-GcbqDataset $datasetName
    }

    It "should take strings, name, description, and time to make a table"{
        $table = New-GcbqTable -Project $test_set.DatasetReference.ProjectId `
            -DatasetId $test_set.DatasetReference.DatasetId "my_table_str" `
            -Name "CSV" -Description "Some Comma Separated Values"
        $table.TableReference.TableId | Should Be "my_table_str"
        $table.TableReference.DatasetId | Should Be $test_set.DatasetReference.DatasetId
        $table.TableReference.ProjectId | Should Be $test_set.DatasetReference.ProjectId
        $table.FriendlyName | Should Be "CSV"
        $table.Description | Should Be "Some Comma Separated Values"
    }

    It "should take a Dataset, name, description, and time to make a table"{
        $table = New-GcbqTable -Dataset $test_set "my_table_ds" -Name "CSV" `
            -Description "Some Comma Separated Values"
        $table.TableReference.TableId | Should Be "my_table_ds"
        $table.TableReference.DatasetId | Should Be $test_set.DatasetReference.DatasetId
        $table.TableReference.ProjectId | Should Be $test_set.DatasetReference.ProjectId
        $table.FriendlyName | Should Be "CSV"
        $table.Description | Should Be "Some Comma Separated Values"
    }

    It "should take a DatasetReference name, description, and time to make a table"{
        $table = New-GcbqTable -Dataset $test_set.DatasetReference "my_table_dr" `
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
        $newtab = $tab | New-GcbqTable
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
        $newtab = $tab | New-GcbqTable
        $newtab | Should Not BeNullOrEmpty
        $newtab.TableReference.TableId | Should Be "my_table_pipeComplex"
        $newtab.TableReference.DatasetId | Should Be $datasetName
        $newtab.FriendlyName | Should Be "PipeTest!@#456><"
        $newtab.Description | Should Be "Some cool stuff in a table?!@>><#'()*&^%"
    }

    It "should properly set the duration of time for which the tables last" {
        $expireInSec = 3600
        $table = New-GcbqTable -DatasetId $datasetName "my_table_duration" -Expiration $expireInSec
        $calculatedTime = [int64](([datetime]::UtcNow)-(get-date "1/1/1970")).TotalMilliseconds
        $table.ExpirationTime | Should BeLessThan ($calculatedTime + (($expireInSec + 5) * 1000))
    }

    It "should throw when there is already a table with the same ID"{
        New-GcbqTable "my_table_repeat" -DatasetId $datasetName 
        { New-GcbqTable "my_table_repeat" -DatasetId $datasetName -ErrorAction Stop } | Should Throw "409"
    }

    It "should throw when the dataset is not found"{
        { New-GcbqTable "my_table_d404" -DatasetId $nonExistDataset } | Should Throw "404"
    }

    It "should throw when the project is not found"{
        { New-GcbqTable -DatasetId $datasetName "my_table_p404" -project $nonExistProject} | Should Throw "404"
    }

    AfterAll {
        $test_set | Remove-GcbqDataset -Force
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
