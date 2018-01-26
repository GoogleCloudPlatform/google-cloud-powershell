. $PSScriptRoot\..\BigQuery\BqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$folder = Resolve-Path $PSScriptRoot

Describe "New-BqSchema" {

    BeforeAll {
        $filename = "$folder\schema.json"
        $filename_mini = "$folder\schema_mini.json"
    }

    It "should create new TableFieldSchema objects by values"{
        $field = New-BqSchema -Name "Title" -Type "STRING"
        $field.Name | Should Be "Title"
        $field.Type | Should Be "STRING"
    }

    It "should create new TableFieldSchema objects by strings"{
        $field = New-BqSchema -JSON '[{"Name":"Title","Type":"STRING"}]'
        $field.Name | Should Be "Title"
        $field.Type | Should Be "STRING"
    }

    It "should create new TableFieldSchema objects by files"{
        $field = New-BqSchema -Filename $filename_mini
        $field.Name | Should Be "Title"
        $field.Type | Should Be "STRING"
    }

    It "should add fields to the pipeline when passed any number of fields"{
        $field = New-BqSchema "Title" "STRING"
        $field = $field | New-BqSchema "Author" "STRING"
        $field = $field | New-BqSchema "Year" "INTEGER"
        $field.Count | Should Be 3
    }

    It "should add a bunch of fields via string"{
        $field = New-BqSchema -JSON '[{"Name":"Title","Type":"STRING"},{"Name":"Author","Type":"STRING"},{"Name":"Year","Type":"INTEGER"}]'
        $field.Count | Should Be 3
    }

    It "should add a bunch of fields via file"{
        $field = New-BqSchema -Filename $filename
        $field.Count | Should Be 3
    }

    It "should handle optional strings correctly"{
        $field = New-BqSchema "Title" "STRING" -Description "Test data table" -Mode "REQUIRED"
        $field.Description | Should Be "Test data table"
        $field.Mode | Should Be "REQUIRED"
    }

    It "should handle fields / nested structures"{
        $inner = New-BqSchema "Title" "STRING"
        $inner = $inner | New-BqSchema "Author" "STRING"
        $outer = New-BqSchema "Nest" "RECORD" -Fields $inner
        $outer.Fields.Count | Should Be 2
    }

    It "should deny invalid types"{
        { New-BqSchema "Title" "NotAType" } | Should Throw "Cannot convert value"
    }

    It "should deny invalid modes"{
        { New-BqSchema "Title" "STRING" -Mode "NotAMode" } | Should Throw "Cannot convert value"
    }

    It "should let users know that they need to have a JSON array"{
        { $field = New-BqSchema -JSON '{"Name":"Title","Type":"STRING"}' } | Should Throw "Cannot deserialize"
    }

    It "should handle when files do not exist"{
        { $field = New-BqSchema -Filename "fileDoesNotExist" } | Should Throw "Could not find file"
    }
}

Describe "Set-BqSchema" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
        $filename = "$folder\schema.json"
    }

    It "should add a single column schema to a Table"{
        $table = $test_set | New-BqTable "my_table"
        $result = New-BqSchema "Title" "STRING" | Set-BqSchema $table
        $result.Schema.Fields[0].Name | Should Be "Title"
    }

    It "should add a multiple column schema to a Table by values"{
        $table = $test_set | New-BqTable "double_table"
        $result = New-BqSchema "Title" "STRING" | New-BqSchema "Author" "STRING" |
                  New-BqSchema "Year" "INTEGER" | Set-BqSchema $table
        $result.Schema.Fields[0].Name | Should Be "Title"
        $result.Schema.Fields[1].Name | Should Be "Author"
        $result.Schema.Fields[2].Name | Should Be "Year"
    }

    It "should add a multiple column schema to a Table by string"{
        $table = $test_set | New-BqTable "string_table"
        $result = New-BqSchema -JSON `
            '[{"Name":"Title","Type":"STRING"},{"Name":"Author","Type":"STRING"},{"Name":"Year","Type":"INTEGER"}]' | Set-BqSchema $table
        $result.Schema.Fields[0].Name | Should Be "Title"
        $result.Schema.Fields[1].Name | Should Be "Author"
        $result.Schema.Fields[2].Name | Should Be "Year"
    }

    It "should add a multiple column schema to a Table by file"{
        $table = $test_set | New-BqTable "file_table"
        $result = New-BqSchema -Filename $filename| 
                  Set-BqSchema $table
        $result.Schema.Fields[0].Name | Should Be "Title"
        $result.Schema.Fields[1].Name | Should Be "Author"
        $result.Schema.Fields[2].Name | Should Be "Year"
    }

    It "should properly return a schema object"{
        $schema = New-BqSchema -JSON `
            '[{"Name":"Title","Type":"STRING"},{"Name":"Author","Type":"STRING"},{"Name":"Year","Type":"INTEGER"}]' | Set-BqSchema
        $schema.Fields[0].Name | Should Be "Title"
        $schema.Fields[1].Name | Should Be "Author"
        $schema.Fields[2].Name | Should Be "Year"
    }

    It "should complain about duplicated column names"{
        $table = $test_set | New-BqTable "another_table"
        $schemas = New-BqSchema -Name "Title" -Type "STRING" | New-BqSchema -Name "Title" -Type "STRING"
        { $schemas | Set-BqSchema $table } | Should Throw "This schema already contains a column with name"
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

Describe "Add-BqTableRow" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
        # Input file path setup.
        $filename_csv = "$folder\classics.csv"
        $filename_json = "$folder\classics.json"
        $filename_avro = "$folder\classics.avro"
        # These files have 3 rows with missing fields and 3 rows with extra fields.
        $filename_broken_csv = "$folder\classics_broken.csv"
        $filename_broken_json = "$folder\classics_broken.json"
        # This file has a missing field and some of the AVRO formatting has been deleted.
        $filename_broken_avro = "$folder\classics_broken.avro"
        # This file has jagged rows and quoted newlines
        $filename_extra_csv = "$folder\classics_jagged.csv"
    }

    BeforeEach {
        $r = Get-Random
        $table = New-BqTable -Dataset $test_Set "table_$r"
        $table = New-BqSchema -Name "Title" -Type "STRING" |
                 New-BqSchema -Name "Author" -Type "STRING" |
                 New-BqSchema -Name "Year" -Type "INTEGER" |
                 Set-BqSchema $table
    }

    <#
    It "should properly consume a well formed CSV file" {
        $table | Add-BqTableRow CSV $filename_csv -SkipLeadingRows 1
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
    }

    It "should properly consume a well formed JSON file" {
        $table | Add-BqTableRow JSON $filename_json 
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
    }#>

    # Skip this test for now because of an error in the BigQueryClient.
    It "should properly consume a well formed AVRO file" -Skip {
        $table | Add-BqTableRow AVRO $filename_avro 
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
    }
    <#
    It "should properly reject an invalid CSV file" {
        { $table | Add-BqTableRow CSV $filename_broken_csv -SkipLeadingRows 1 } | Should Throw "contained errors"
    }

    It "should properly reject an invalid JSON file" {
        { $table | Add-BqTableRow JSON $filename_broken_json } | Should Throw "contained errors"
    }

    It "should properly reject an invalid AVRO file" {
        { $table | Add-BqTableRow AVRO $filename_broken_avro } | Should Throw "contained errors"
    }

    It "should handle less than perfect CSV files" {
        $table | Add-BqTableRow CSV $filename_broken_csv -SkipLeadingRows 1 -AllowJaggedRows -AllowUnknownFields
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
    }

    It "should handle write disposition WriteAppend" {
        $table | Add-BqTableRow CSV $filename_csv -SkipLeadingRows 1 
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
        $table | Add-BqTableRow CSV $filename_csv -SkipLeadingRows 1 -WriteMode WriteAppend
        $table = Get-BqTable $table
        $table.NumRows | Should Be 20
    }

    It "should handle write disposition WriteIfEmpty on an empty table" {
        $table | Add-BqTableRow CSV $filename_csv -SkipLeadingRows 1 -WriteMode WriteIfEmpty
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
    }

    It "should handle write disposition WriteIfEmpty on a non-empty table" {
        $table | Add-BqTableRow CSV $filename_broken_csv -SkipLeadingRows 1 -AllowJaggedRows -AllowUnknownFields 
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
        { $table | Add-BqTableRow CSV $filename_csv -SkipLeadingRows 1 `
            -WriteMode WriteIfEmpty } | Should Throw "Already Exists"
    }

    It "should handle write disposition WriteTruncate" {
        $table | Add-BqTableRow CSV $filename_csv -SkipLeadingRows 1
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
        $table | Add-BqTableRow CSV $filename_broken_csv -SkipLeadingRows 1 -AllowJaggedRows `
                -AllowUnknownFields -WriteMode WriteTruncate
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
    }

    It "should allow jagged rows and quoted newlines" {
        $table | Add-BqTableRow CSV $filename_extra_csv -SkipLeadingRows 1 -AllowJaggedRows -AllowQuotedNewLines
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
    }

    It "should handle less than perfect JSON files" {
        $table | Add-BqTableRow JSON $filename_broken_JSON -AllowUnknownFields
        $table = Get-BqTable $table
        $table.NumRows | Should Be 10
    }#>

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Describe "Get-BqTableRow" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
        # Input file path setup.
        $filename = "$folder\classics.csv"
        $filename_big = "$folder\classics_large.csv"
        # Small Table Setup.
        $table = New-BqTable "table_$r" -Dataset $test_Set 
        $table = New-BqSchema -Name "Title" -Type "STRING" |
                 New-BqSchema -Name "Author" -Type "STRING" |
                 New-BqSchema -Name "Year" -Type "INTEGER" |
                 Set-BqSchema $table
        $table | Add-BqTableRow CSV $filename -SkipLeadingRows 1
        $table = Get-BqTable $table
    }

    It "should return an entire table of 10 rows" {
        $list = $table | Get-BqTableRow 
        $list.Count | Should Be 10
        $list[0]["Author"] | Should Be "Jane Austin"
        $list[2]["Title"] | Should Be "War and Peas"
        $list[5]["Year"] | Should Be "1851"
    }

    It "should handle paging correctly" {
        # Default page size = 100,000.
        $bigtable = New-BqTable "table_big_$r" -Dataset $test_Set 
        $bigtable = New-BqSchema -Name "Title" -Type "STRING" |
                 New-BqSchema -Name "Author" -Type "STRING" |
                 New-BqSchema -Name "Year" -Type "INTEGER" |
                 Set-BqSchema $bigtable
        $bigtable | Add-BqTableRow CSV $filename_big -SkipLeadingRows 1
        $bigtable = Get-BqTable $bigtable

        $list = $bigtable | Get-BqTableRow
        $list.Count | Should Be 100100
    }

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
