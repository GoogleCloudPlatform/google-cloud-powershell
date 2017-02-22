. $PSScriptRoot\..\BigQuery\GcbqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcbqTable" {

    It "should list tables from a dataset object from pipeline."{
        $tables = Get-GcbqDataset “pshell_testing” | Get-GcbqTable
        $tables.Count | Should Not Be 0
    }

    It "should list tables from a dataset object via parameter."{
        $table = Get-GcbqDataset “pshell_testing” 
        $tables = Get-GcbqTable -InputObject $table
        $tables.Count | Should Not Be 0
    }

    It "should list tables from a dataset ID by parameter."{
        $tables = Get-GcbqTable -Dataset “pshell_testing”
        $tables.Count | Should Not Be 0
    }

    It "should get a singular table with a dataset object from pipeline."{
        $table = Get-GcbqDataset “pshell_testing” | Get-GcbqTable "my_table"
        $table.TableReference.TableId | Should be "my_table"
        $table.Description | Should Be "TEST"
    }

    It "should get a singular table with a dataset object via parameter."{
        $dset = Get-GcbqDataset “pshell_testing”
        $table = Get-GcbqTable -InputObject $dset "my_other_table"
        $table.TableReference.TableId | Should be "my_other_table"
        $table.Description | Should Be "TEST"
    }

    It "should get a singular table with a dataset ID by parameter."{
        $table = Get-GcbqTable -Dataset “pshell_testing” "my_table"
        $table.TableReference.TableId | Should be "my_table"
        $table.Description | Should Be "TEST"
    }

    It "should throw when no database passed."{
        { Get-GcbqTable } | Should Throw "No Dataset Specified"
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
