. $PSScriptRoot\..\BigQuery\BqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "New-BqSchema" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
    }

    It "should create new TableFieldSchema objects"{
        $field = New-BqSchema -Name "Title" -Type "STRING"
        $field.Name | Should Be "Title"
        $field.Type | Should Be "STRING"
    }

    It "should add fields to the pipeline when passed any number of fields"{
        $field = New-BqSchema -Name "Title" -Type "STRING"
        $field = $field | New-BqSchema -Name "Author" -Type "STRING"
        $field = $field | New-BqSchema -Name "Copyright" -Type "STRING"
        $field.Count | Should Be 3
    }

    It "should handle optional strings correctly"{
        $field = New-BqSchema -Name "Title" -Type "STRING" -Description "Test data table" -Mode "REQUIRED"
        $field.Description | Should Be "Test data table"
        $field.Mode | Should Be "REQUIRED"
    }

    It "should handle fields / nested structures"{
        $inner = New-BqSchema -Name "Title" -Type "STRING"
        $inner = $inner | New-BqSchema -Name "Author" -Type "STRING"
        $outer = New-BqSchema -Name "Nest" -Type "RECORD" -Fields $inner
        $outer.Fields.Count | Should Be 2
    }

    It "should deny invalid types"{
        { New-BqSchema -Name "Title" -Type "NotAType" -ErrorAction Stop } | Should Throw "Cannot convert value"
    }

    It "should deny invalid modes"{
        { New-BqSchema -Name "Title" -Type "STRING" -Mode "NotAMode" -ErrorAction Stop } | Should Throw "Cannot convert value"
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

    It "should add a single column schema to a Table"{
        $table = $test_set | New-BqTable "my_table"
        $result = New-BqSchema -Name "Title" -Type "STRING" | Set-BqSchema $table
        $result.Schema.Fields[0].Name | Should Be "Title"
    }

    It "should add amultiple column schema to a Table"{
        $table = $test_set | New-BqTable "double_table"
        $result = New-BqSchema -Name "Author" -Type "STRING" |
                  New-BqSchema -Name "Copyright" -Type "STRING" |
                  New-BqSchema -Name "Title" -Type "STRING" |
                  Set-BqSchema $table
        $result.Schema.Fields[0].Name | Should Be "Author"
        $result.Schema.Fields[1].Name | Should Be "Copyright"
        $result.Schema.Fields[2].Name | Should Be "Title"
    }

    It "should complain about duplicated column names"{
        $table = $test_set | New-BqTable "another_table"
        $schemas = New-BqSchema -Name "Title" -Type "STRING" | New-BqSchema -Name "Title" -Type "STRING"
        { $schemas | Set-BqSchema $table -ErrorAction Stop } | Should Throw "This schema already contains a column with name"
    }

    It "should not add a schema to a table that does not exist" {
        $table = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Table
        $table.TableReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.TableReference
        $table.TableReference.ProjectId = $project
        $table.TableReference.DatasetId = $datasetName
        $table.TableReference.TableId = "not_gonna_happen"
        { New-BqSchema -Name "Title" -Type "STRING" | Set-BqSchema $table } | Should Throw 404
    } 

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Describe "Add-BqTabledata" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
        # Input file path setup.
        $folder = Get-Location
        $folder = $folder.ToString()
        $filename_csv = "$folder\classics.csv"
        $filename_json = "$folder\classics.json"
        $filename_avro = "$folder\classics.avro"
        # These files have 3 rows with missing fields and 3 rows with extra fields.
        $filename_broken_csv = "$folder\classics_broken.csv"
        $filename_broken_json = "$folder\classics_broken.json"
        # This file has a missing field and some of the AVRO formatting has been deleted.
        $filename_broken_avro = "$folder\classics_broken.avro"
    }

    BeforeEach {
        $r = Get-Random
        $table = New-BqTable -Dataset $test_Set "table_$r"
        $table = New-BqSchema -Name "Title" -Type "STRING" |
                 New-BqSchema -Name "Author" -Type "STRING" |
                 New-BqSchema -Name "Year" -Type "INTEGER" |
                 Set-BqSchema $table
    }

    It "should properly consume a well formed CSV file" {
        $table | Add-BqTabledata $filename_csv CSV -SkipLeadingRows 1
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
    }

    It "should properly consume a well formed JSON file" {
        $table | Add-BqTabledata $filename_json JSON
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
    }

    It "should properly consume a well formed AVRO file" {
        $table | Add-BqTabledata $filename_avro AVRO
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
    }

    It "should properly reject an invalid CSV file" {
        { $table | Add-BqTabledata $filename_broken_csv CSV -SkipLeadingRows 1 -ErrorAction Stop } | Should Throw "contained errors"
    }

    It "should properly reject an invalid JSON file" {
        { $table | Add-BqTabledata $filename_broken_json JSON -ErrorAction Stop } | Should Throw "contained errors"
    }

    It "should properly reject an invalid AVRO file" {
        { $table | Add-BqTabledata $filename_broken_avro AVRO -ErrorAction Stop } | Should Throw "contained errors"
    }

    #TODO(ahandley): Test all CSV options

    It "should handle less than perfect CSV files" {
        $table | Add-BqTabledata $filename_broken_csv CSV -SkipLeadingRows 1 -MaxBadRecords 4 `
                -AllowUnknownFields -ErrorAction SilentlyContinue
        $table = Get-BqTable $table
        $table.NumRows | Should Be 7
    }

    It "should handle write disposition WriteAppend" {
        $table | Add-BqTabledata $filename_csv CSV -SkipLeadingRows 1 
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
        $table | Add-BqTabledata $filename_csv CSV -SkipLeadingRows 1 -WriteMode WriteAppend
        $table = Get-BqTable $table
        $table.NumRows | Should Be 20
    }

    It "should handle write disposition WriteIfEmpty" {
        $table | Add-BqTabledata $filename_broken_csv CSV -SkipLeadingRows 1 -MaxBadRecords 4 `
                -AllowUnknownFields -ErrorAction SilentlyContinue
        $table = Get-BqTable $table
        $table.NumRows | Should Be 7
        { $table | Add-BqTabledata $filename_csv CSV -SkipLeadingRows 1 `
            -WriteMode WriteIfEmpty -ErrorAction Stop } | Should Throw "404"
    }

    It "should handle write disposition WriteTruncate" {
        $table | Add-BqTabledata $filename_csv CSV -SkipLeadingRows 1
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
        $table | Add-BqTabledata $filename_broken_csv CSV -SkipLeadingRows 1 -MaxBadRecords 4 `
                -AllowUnknownFields -WriteMode WriteTruncate -ErrorAction SilentlyContinue
        $table = Get-BqTable $table
        $table.NumRows | Should Be 7
    }

    #TODO(ahandley): Test all JSON options

    It "should handle less than perfect JSON files" {
        $table | Add-BqTabledata $filename_broken_JSON JSON -MaxBadRecords 4 `
                -AllowUnknownFields -ErrorAction SilentlyContinue
        $table = Get-BqTable $table
        $table.NumRows | Should Be 9
    }

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
