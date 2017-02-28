. $PSScriptRoot\..\BigQuery\GcbqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

$testDataset = "pshell_testing"

Describe "Get-GcbqTable" {

    $d = New-GcbqDataset $testDataset
    $d | New-GcbqTable "my_table" -Name "my_table" -Description "Test data table"
    $d | New-GcbqTable "my_other_table" -Name "my_other_table" -Description "Another test table"

    It "should list tables from a dataset object from pipeline."{
        $tables = Get-GcbqDataset $testDataset | Get-GcbqTable
        $tables.Count | Should BeGreaterThan 0
    }

    It "should list tables from a dataset object via parameter."{
        $dataset = Get-GcbqDataset $testDataset 
        $tables = Get-GcbqTable -Dataset $dataset
        $tables.Count | Should BeGreaterThan 0
    }

    It "should list tables from a dataset ID by parameter."{
        $tables = Get-GcbqTable -Dataset $testDataset
        $tables.Count | Should BeGreaterThan 0
    }

    It "should get a singular table with a dataset object from pipeline."{
        $table = Get-GcbqDataset $testDataset | Get-GcbqTable "my_table"
        $table.TableReference.TableId | Should be "my_table"
        $table.Description | Should Be "Test data table"
    }

    It "should get a singular table with a dataset object via parameter."{
        $dataset = Get-GcbqDataset $testDataset
        $table = Get-GcbqTable -Dataset $dataset "my_other_table"
        $table.TableReference.TableId | Should be "my_other_table"
        $table.Description | Should Be "Another test table"
    }

    It "should get a singular table with a dataset ID by parameter."{
        $table = Get-GcbqTable -Dataset $testDataset "my_table"
        $table.TableReference.TableId | Should be "my_table"
        $table.Description | Should Be "Test data table"
    }

    It "should throw when the table is not found."{
        { Get-GcbqTable -Dataset $testDataset $nonExistTable -ErrorAction Stop} | Should Throw "404"
    }

    It "should throw when the dataset is not found."{
        { Get-GcbqTable -Dataset $nonExistDataset } | Should Throw "404"
    }

    It "should throw when the project is not found."{
        { Get-GcbqTable -project $nonExistProject -Dataset $testDataset} | Should Throw "404"
    }

    $d | Remove-GcbqDataset -Force
}

Describe "New-GcbqTable" {

    $d = New-GcbqDataset $testDataset

    It "should take a name, description, and time to make a table."{
        $a = New-GcbqTable -Dataset $testDataset "my_table_1" -Name "CSV" -Description `
            "Some Comma Separated Values"
        $a.TableReference.TableId | Should Be "my_table_1"
        $a.FriendlyName | Should Be "CSV"
        $a.Description | Should Be "Some Comma Separated Values"
    }

    It "should accept a table object from pipeline."{
        $tab = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Table
        $tab.TableReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.TableReference
        $tab.TableReference.TableId = "my_table_2"
        $tab.TableReference.DatasetId = $testDataset
        $tab.TableReference.ProjectId = $project
        $tab.FriendlyName = "PipeTest"
        $tab.Description = "Some cool stuff in a table"
        $newtab = $tab | New-GcbqTable
        $newtab | Should Not BeNullOrEmpty
        $newtab.TableReference.TableId | Should Be "my_table_2"
        $newtab.TableReference.DatasetId | Should Be $testDataset
        $newtab.FriendlyName | Should Be "PipeTest"
        $newtab.Description | Should Be "Some cool stuff in a table"
    }

    It "should accept a more complex table object from pipeline."{
        $tab = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Table
        $tab.TableReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.TableReference
        $tab.TableReference.TableId = "my_table_3"
        $tab.TableReference.DatasetId = $testDataset
        $tab.TableReference.ProjectId = $project
        $tab.FriendlyName = "PipeTest!@#456><"
        $tab.Description = "Some cool stuff in a table?!@>><#'()*&^%"
        $newtab = $tab | New-GcbqTable
        $newtab | Should Not BeNullOrEmpty
        $newtab.TableReference.TableId | Should Be "my_table_3"
        $newtab.TableReference.DatasetId | Should Be $testDataset
        $newtab.FriendlyName | Should Be "PipeTest!@#456><"
        $newtab.Description | Should Be "Some cool stuff in a table?!@>><#'()*&^%"
    }

    It "should throw when there is already a table with the same ID."{
        New-GcbqTable "my_table_4" -Dataset $testDataset 
        { New-GcbqTable "my_table_4" -Dataset $testDataset -ErrorAction Stop } | Should Throw "409"
    }

    It "should throw when the dataset is not found."{
        { New-GcbqTable "my_table_5" -Dataset $nonExistDataset } | Should Throw "404"
    }

    It "should throw when the project is not found."{
        { New-GcbqTable -Dataset $testDataset "my_table_6" -project $nonExistProject} | Should Throw "404"
    }

    $d | Remove-GcbqDataset -Force
}

Reset-GCloudConfig $oldActiveConfig $configName
