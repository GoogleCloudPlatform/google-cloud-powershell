. $PSScriptRoot\..\BigQuery\BqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "New-BqSchema" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
    }

    It "should create new TableSchema objects"{
        $schema = New-BqSchema -Name "Title" -Type "STRING"
        $schema.Fields[0].Name | Should Be "Title"
        $schema.Fields[0].Type | Should Be "STRING"
    }

    It "should add fields to existing TableSchema objects"{
        $schema = New-BqSchema -Name "Title" -Type "STRING"
        $schema = $schema | New-BqSchema -Name "Author" -Type "STRING"
        $schema.Fields.Count | Should Be 2
    }

    It "should handle optional strings correctly"{
        $schema = New-BqSchema -Name "Title" -Type "STRING" -Description "Test data table" -Mode "REQUIRED"
        $schema.Fields[0].Description | Should Be "Test data table"
        $schema.Fields[0].Mode | Should Be "REQUIRED"
    }

    It "should handle fields / nested structures"{
        $inner = New-BqSchema -Name "Title" -Type "STRING"
        $inner = $inner | New-BqSchema -Name "Author" -Type "STRING"
        $outer = New-BqSchema -Name "Nest" -Type "RECORD" -Fields $inner
        $outer.Fields[0].Fields.Count | Should Be 2
    }

    It "should deny invalid types"{
        { New-BqSchema -Name "Title" -Type "NotAType" -ErrorAction Stop } | Should Throw "Cannot convert value"
    }

    It "should deny invalid modes"{
        { New-BqSchema -Name "Title" -Type "STRING" -Mode "NotAMode" -ErrorAction Stop } | Should Throw "Cannot convert value"
    }

    It "should complain about duplicated column names"{
        $schema = New-BqSchema -Name "Title" -Type "STRING"
        { $schema | New-BqSchema -Name "Title" -Type "STRING" -ErrorAction Stop } | Should Throw "This schema already contains a column with name"
    }

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Describe "Set-BqSchema" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
    }

    It "should add to Tables correctly"{
        $table = $test_set | New-BqTable "my_table"
        $schema = New-BqSchema -Name "Title" -Type "STRING"
        $schema | Set-BqSchema $table
        $result = $table | Get-BqTable
        $result.Schema.Fields[0].Name | Should Be "Title"
    }

    It "should not add a schema to a table that does not exist" {
        $table = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Table
        $table.TableReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.TableReference
        $table.TableReference.ProjectId = $project
        $table.TableReference.DatasetId = $datasetName
        $table.TableReference.TableId = "not_gonna_happen_today"
        $schema = New-BqSchema -Name "Title" -Type "STRING"
        { $schema | Set-BqSchema $table } | Should Throw 404
    } 

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}


Reset-GCloudConfig $oldActiveConfig $configName
