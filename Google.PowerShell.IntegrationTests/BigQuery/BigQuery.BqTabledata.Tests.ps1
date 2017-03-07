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

    It "should add to Tables correctly"{
        $schema = New-BqSchema -Name "Title" -Type "STRING"
        $table = $test_set | New-BqTable "my_table"
        $table.ETag = $null;
        #TODO(ahandley): Find a more elegant way of doing this.  Don't make users have to do this.
        # Suspect the best way to do this is to make a Set-BqSchema cmdlet.  Going to have to investigate.
        $table.Schema = $schema
        $result = $table | Set-BqTable
        $result.Schema.Fields[0].Name | Should Be "Title"
    }

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
