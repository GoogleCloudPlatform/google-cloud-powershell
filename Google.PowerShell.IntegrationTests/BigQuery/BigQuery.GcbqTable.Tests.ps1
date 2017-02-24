. $PSScriptRoot\..\BigQuery\GcbqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcbqTable" {

    #TODO(ahandley): Include setup and teardown code in these tests as New- and Remove- are developed.

    It "should list tables from a dataset object from pipeline."{
        $tables = Get-GcbqDataset “pshell_testing” | Get-GcbqTable
        $tables.Count | Should BeGreaterThan 0
    }

    It "should list tables from a dataset object via parameter."{
        $dataset = Get-GcbqDataset “pshell_testing” 
        $tables = Get-GcbqTable -Dataset $dataset
        $tables.Count | Should BeGreaterThan 0
    }

    It "should list tables from a dataset ID by parameter."{
        $tables = Get-GcbqTable -Dataset “pshell_testing”
        $tables.Count | Should BeGreaterThan 0
    }

    It "should get a singular table with a dataset object from pipeline."{
        $table = Get-GcbqDataset “pshell_testing” | Get-GcbqTable "my_table"
        $table.TableReference.TableId | Should be "my_table"
        $table.Description | Should Be "Test data table"
    }

    It "should get a singular table with a dataset object via parameter."{
        $dataset = Get-GcbqDataset “pshell_testing”
        $table = Get-GcbqTable -Dataset $dataset "my_other_table"
        $table.TableReference.TableId | Should be "my_other_table"
        $table.Description | Should Be "Another test table"
    }

    It "should get a singular table with a dataset ID by parameter."{
        $table = Get-GcbqTable -Dataset “pshell_testing” "my_table"
        $table.TableReference.TableId | Should be "my_table"
        $table.Description | Should Be "Test data table"
    }

    It "should throw when the table is not found."{
        { Get-GcbqTable -Dataset "pshell_testing" $nonExistTable -ErrorAction Stop} | Should Throw "404"
    }

    It "should throw when the dataset is not found."{
        { Get-GcbqTable -Dataset $nonExistDataset } | Should Throw "404"
    }

    It "should throw when the project is not found."{
        { Get-GcbqTable -project $nonExistProject -Dataset "pshell_testing"} | Should Throw "404"
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
