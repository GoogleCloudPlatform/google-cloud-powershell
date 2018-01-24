. $PSScriptRoot\..\BigQuery\BqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$folder = Resolve-Path $PSScriptRoot

Describe "Get-BqJob" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
        $filename = "$folder\classics.csv"
        $table = New-BqTable -Dataset $test_Set "table_$r"
        New-BqSchema "Title" "STRING" | New-BqSchema "Author" "STRING" | New-BqSchema "Year" "INTEGER" | 
            Set-BqSchema $table
        $table | Add-BqTableRow CSV $filename -SkipLeadingRows 1
        $script:bucketName = "ps_test_$r"
        $bucket = New-GcsBucket $bucketName
        $gcspath = "gs://ps_test_$r"
    }

    It "should list jobs from the past 6 months" {
        $jobs = Get-BqJob
        $jobs.Count | Should BeGreaterThan 0
    }

    It "should filter on state correctly when listing" {
        $jobs = Get-BqJob -State "DONE"
        $jobs.Count | Should BeGreaterThan 1
        $jobs = Get-BqJob -State "PENDING"
        $jobs.Count | Should Be 0
    }

    It "should get specific job via pipeline" {
        $jobs = Get-BqJob
        $job = $jobs[0] 
        $return = $job | Get-BqJob
        $return.JobReference.JobId | Should Be $job.JobReference.JobId
    }

    It "should get specific job via string parameter" {
        $jobs = Get-BqJob
        $job = $jobs[0] 
        $return = Get-BqJob $job.JobReference.JobId
        $return.JobReference.JobId | Should Be $job.JobReference.JobId
    }

    It "should get specific job via object parameter" {
        $jobs = Get-BqJob
        $job = $jobs[0] 
        $return = Get-BqJob $job
        $return.JobReference.JobId | Should Be $job.JobReference.JobId
    }

    It "should throw when the job is not found"{
        { Get-BqJob $nonExistJob } | Should Throw "404"
    }

    It "should throw when the project is not found"{
        { Get-BqJob -project $nonExistProject } | Should Throw "404"
    }

    It "should handle projects that the user does not have permissions for" {
        { Get-BqJob -Project $accessErrProject } | Should Throw "400"
    }

    AfterAll {
        Remove-GcsBucket $script:bucketName -Force
        $test_set | Remove-BqDataset -Force
    }
}

Describe "BqJob-Query" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
        $filename = "$folder\classics.csv"
        $table = New-BqTable -Dataset $test_Set "table_$r"
        New-BqSchema "Title" "STRING" | New-BqSchema "Author" "STRING" | New-BqSchema "Year" "INTEGER" | 
            Set-BqSchema $table | Add-BqTableRow CSV $filename -SkipLeadingRows 1
    }

    It "should query a pre-loaded table" {
        $job = Start-BqJob -Query "select * from $datasetName.table_$r where Year > 1900"
        $job | Should Not Be $null
        $results = $job | Receive-BqJob
        $results.Count | Should Be 2
        $results[0]["Author"] | Should Be "Gabriel Marquez"
        $results[1]["Year"] | Should Be 1985
    }

    It "should query a pre-loaded table with more options than ever before!" {
        $alt_tab = New-BqTable -Dataset $test_Set "table_res_$r"
        $job = Start-BqJob -Query "select * from $datasetName.table_$r where Year > 1900" `
                           -DefaultDataset $test_set -Destination $alt_tab -PollUntilComplete
        $job = $job | Get-BqJob
        $alt_tab = $alt_tab | Get-BqTable
        $job.Status.State | Should Be "DONE"
        $job.Configuration.Query.DefaultDataset.DatasetId | Should Be $test_set.DatasetReference.DatasetId
        $job.Configuration.Query.DestinationTable.TableId | Should Be $alt_tab.TableReference.TableId
        $alt_tab.NumRows | Should Be 2
    }

    It "should use legacy SQL when asked" {
        $job = Start-BqJob -Query "select * from $datasetName.table_$r where Year > 1900" -UseLegacySql
        $job.Configuration.Query.UseLegacySql | Should Be True
        $results = Receive-BqJob $job
        $results.Count | Should Be 2
        $results[0]["Year"] | Should Be 1967
        $results[1]["Author"] | Should Be "Orson Scott Card"
    }

    It "should handle Timeouts" {
        $job = Start-BqJob -Query "select * from $datasetName.table_$r where Year > 1900"
        $results = $job | Receive-BqJob -Timeout 1000
        $results.Count | Should Be 2
    }

    It "should go end to end with a synch command" {
        $results = Start-BqJob -Query "select * from $datasetName.table_$r where Year > 1900" -Synchronous |
            Receive-BqJob
        $results.Count | Should Be 2
    }

    It "should properly halt when -WhatIf is passed" {
        Start-BqJob -Query "select * from $datasetName.table_$r where Year > 1900" -WhatIf | Should Be $null
    }

    # Note - Priority cannot be tested, as the server does not output the Job.Configuration.Query.Priority property.

    It "should handle projects that the user does not have permissions for" {
        { Start-BqJob -Query "select * from $datasetName.table_$r" -Project $accessErrProject } | Should Throw "400"
    }

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Describe "BqJob-Copy" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
        $filename = "$folder\classics.csv"
        $filename_other = "$folder\otherschema.csv"

        $table = New-BqTable -Dataset $test_Set "table_$r"
        New-BqSchema "Title" "STRING" | New-BqSchema "Author" "STRING" | New-BqSchema "Year" "INTEGER" | 
            Set-BqSchema $table | Add-BqTableRow CSV $filename -SkipLeadingRows 1

        $table_other = New-BqTable -Dataset $test_Set "table_other_$r"
        New-BqSchema "Position" "INTEGER" | New-BqSchema "Number" "INTEGER" | New-BqSchema "Average" "FLOAT" | 
            Set-BqSchema $table_other | Add-BqTableRow CSV $filename_other -SkipLeadingRows 1
    }

    It "should copy a table with the same schema" {
        $r = Get-Random     
        $target = New-BqTable -Dataset $test_Set "table_$r"
        $table | Start-BqJob -Copy $target -PollUntilComplete
        $target = $target | Get-BqTable
        $target.NumRows | Should Be 10
    }

    It "should handle writemodes correctly" {
        $r = Get-Random     
        $target = New-BqTable -Dataset $test_Set "table_$r"
        $table | Start-BqJob -Copy $target -WriteMode WriteIfEmpty -PollUntilComplete
        $target = $target | Get-BqTable
        $target.NumRows | Should Be 10
        $table | Start-BqJob -Copy $target -WriteMode WriteAppend -PollUntilComplete
        $target = $target | Get-BqTable
        $target.NumRows | Should Be 20
        $table | Start-BqJob -Copy $target -WriteMode WriteTruncate -PollUntilComplete
        $target = $target | Get-BqTable
        $target.NumRows | Should Be 10
        { $table | Start-BqJob -Copy $target -WriteMode WriteIfEmpty -PollUntilComplete } | Should Throw "Already Exists"
    }
    
    It "should throw an error when trying to mix schemas" {
        { $table_other | Start-BqJob -Copy $table -WriteMode WriteAppend } | Should Throw "Provided Schema does not match"
    }

    It "should throw an error when the table already exists" {
        { $table | Start-BqJob -Copy $table } | Should Throw "Already Exists"
    }

    It "should throw when told to copy an empty table" {
        $r = Get-Random     
        $empty = New-BqTable -Dataset $test_Set "table_$r"
        { $empty | Start-BqJob -Copy $table } | Should Throw "Cannot read a table without a schema"
    }

    It "should make a new table if the target does not exist" {
        $ref = New-Object -TypeName Google.Apis.Bigquery.v2.Data.TableReference
        $ref.ProjectId = $project
        $ref.DatasetId = $datasetName
        $ref.TableId = "random_table_name"
        $table_other | Start-BqJob -Copy $ref -PollUntilComplete
        $target = $ref | Get-BqTable
        $target.NumRows | Should Be 15
    }

    It "should properly halt when -WhatIf is passed" {
        $table | Start-BqJob -Copy $table_other -WhatIf | Should Be $null
    }

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Describe "BqJob-Extract-Load" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
        $filename = "$folder\classics.csv"
        $table = New-BqTable -Dataset $test_Set "table_$r"
        New-BqSchema "Title" "STRING" | New-BqSchema "Author" "STRING" | New-BqSchema "Year" "INTEGER" | 
            Set-BqSchema $table | Add-BqTableRow CSV $filename -SkipLeadingRows 1
        $script:bucketName = "ps_test_$r"
        $bucket = New-GcsBucket $bucketName
        $gcspath = "gs://ps_test_$r"
    }

    It "should set Load parameters correctly" {
        $alt_tab = $test_set | New-BqTable "param_test_$r"
        $table | Start-BqJob -Extract CSV "$gcspath/param.csv" -Synchronous
        New-BqSchema "Title" "STRING" | New-BqSchema "Author" "STRING" | 
            New-BqSchema "Year" "INTEGER" | Set-BqSchema $alt_tab
        $job = $alt_tab | Start-BqJob -Load CSV "$gcspath/param.csv" -WriteMode WriteAppend `
            -Encoding "ISO-8859-1" -FieldDelimiter "|" -Quote "'" -MaxBadRecords 3 `
            -SkipLeadingRows 2 -AllowUnknownfields -AllowJaggedRows -AllowQuotedNewlines
        $job.Configuration.Load.AllowJaggedRows | Should Be $true
        $job.Configuration.Load.AllowQuotedNewlines | Should Be $true
        $job.Configuration.Load.Encoding | Should Be "ISO-8859-1"
        $job.Configuration.Load.FieldDelimiter | Should Be "|"
        $job.Configuration.Load.IgnoreUnknownValues | Should Be $true
        $job.Configuration.Load.Quote | Should Be "'"
        $job.Configuration.Load.SkipLeadingRows | Should Be 2
        $job.Configuration.Load.SourceFormat.ToString() | Should Be "CSV"
        $job.Configuration.Load.WriteDisposition.ToString() | Should Be "WRITE_APPEND"
        $job.Configuration.Load.MaxBadRecords | Should Be 3
    }

    It "should default Load parameters correctly" {
        $alt_tab = $test_set | New-BqTable "param_test_default_$r"
        $table | Start-BqJob -Extract CSV "$gcspath/param.csv" -Synchronous
        New-BqSchema "Title" "STRING" | New-BqSchema "Author" "STRING" |
            New-BqSchema "Year" "INTEGER" | Set-BqSchema $alt_tab
        $job = $alt_tab | Start-BqJob -Load CSV "$gcspath/param.csv"
        $job.Configuration.Load.AllowJaggedRows | Should Be $false
        $job.Configuration.Load.AllowQuotedNewlines | Should Be $false
        $job.Configuration.Load.Encoding | Should Be "UTF-8"
        $job.Configuration.Load.FieldDelimiter | Should BeNullOrEmpty
        $job.Configuration.Load.IgnoreUnknownValues | Should Be $false
        $job.Configuration.Load.Quote | Should BeNullOrEmpty
        $job.Configuration.Load.SkipLeadingRows | Should BeNullOrEmpty
        $job.Configuration.Load.SourceFormat.ToString() | Should Be "CSV"
        $job.Configuration.Load.WriteDisposition | Should Be $null
        $job.Configuration.Load.MaxBadRecords | Should Be 0
    }

    It "should set Extract parameters correctly" {
        $job = $table | Start-BqJob -Extract CSV "$gcspath/otherparam.csv" -Compress -FieldDelimiter "|" -NoHeader
        $job.Configuration.Extract.Compression | Should Be $true
        $job.Configuration.Extract.DestinationFormat.ToString() | Should Be "CSV"
        $job.Configuration.Extract.DestinationUri | Should Be "$gcspath/otherparam.csv"
        $job.Configuration.Extract.FieldDelimiter | Should Be "|"
        $job.Configuration.Extract.PrintHeader | Should Be $false
    }

    It "should default Extract parameters correctly" {
        $job = $table | Start-BqJob -Extract CSV "$gcspath/otherparam.csv"
        $job.Configuration.Extract.Compression | Should Be "NONE"
        $job.Configuration.Extract.DestinationFormat.ToString() | Should Be "CSV"
        $job.Configuration.Extract.DestinationUri | Should Be "$gcspath/otherparam.csv"
        $job.Configuration.Extract.FieldDelimiter | Should BeNullOrEmpty
        $job.Configuration.Extract.PrintHeader | Should Be $true
    }

    It "should Extract/Load a basic CSV" {
        $job = $table | Start-BqJob -Extract CSV "$gcspath/basic.csv" -Synchronous
        $job.Status.State | Should Be "DONE"
        $job.Status.ErrorResult | Should Be $null

        $file = Get-GcsObject $bucketName "basic.csv"
        $file | Should Not Be $null

        $alt_tab = $test_set | New-BqTable "basic_test_$r"
        New-BqSchema "Title" "STRING" | New-BqSchema "Author" "STRING" |
            New-BqSchema "Year" "INTEGER" | Set-BqSchema $alt_tab
        $boj = $alt_tab | Start-BqJob -Load CSV "$gcspath/basic.csv" -SkipLeadingRows 1 -Synchronous
        $boj.Status.State | Should Be "DONE"

        $alt_tab = $alt_tab | Get-BqTable
        $alt_tab.NumRows | Should Be 10
    }

    It "should Extract/Load a more complex CSV request" {
        $job = $table | Start-BqJob -Extract CSV "$gcspath/complex.csv" -FieldDelimiter "|" -NoHeader -Synchronous
        $job.Status.State | Should Be "DONE"
        $job.Status.ErrorResult | Should Be $null

        $file = Get-GcsObject $bucketName "basic.csv"
        $file | Should Not Be $null

        $alt_tab = $test_set | New-BqTable "complex_test_$r"
        New-BqSchema "Title" "STRING" | New-BqSchema "Author" "STRING" |
            New-BqSchema "Year" "INTEGER" | Set-BqSchema $alt_tab
        $boj = $alt_tab | Start-BqJob -Load CSV "$gcspath/complex.csv" -FieldDelimiter "|" -Synchronous
        $boj.Status.State | Should Be "DONE"

        $alt_tab = $alt_tab | Get-BqTable
        $alt_tab.NumRows | Should Be 10
    }

    It "should handle WriteMode correctly" {
        $table | Start-BqJob -Extract CSV "$gcspath/write.csv" -Synchronous
        $file = Get-GcsObject $bucketName "write.csv"
        $alt_tab = $test_set | New-BqTable "writemode_test_$r"
        New-BqSchema "Title" "STRING" | New-BqSchema "Author" "STRING" |
            New-BqSchema "Year" "INTEGER" | Set-BqSchema $alt_tab

        $alt_tab | Start-BqJob -Load CSV "$gcspath/write.csv" -SkipLeadingRows 1 `
            -WriteMode WriteIfEmpty -Synchronous
        $alt_tab = $alt_tab | Get-BqTable
        $alt_tab.NumRows | Should Be 10

        $alt_tab | Start-BqJob -Load CSV "$gcspath/write.csv" -SkipLeadingRows 1 `
            -WriteMode WriteAppend -Synchronous
        $alt_tab = $alt_tab | Get-BqTable
        $alt_tab.NumRows | Should Be 20

        $alt_tab | Start-BqJob -Load CSV "$gcspath/write.csv" -SkipLeadingRows 1 `
            -WriteMode WriteTruncate -Synchronous
        $alt_tab = $alt_tab | Get-BqTable
        $alt_tab.NumRows | Should Be 10
    }

    It "Load should properly halt when -WhatIf is passed" {
        $table | Start-BqJob -Load AVRO "$gcspath/whatif.json" -WhatIf | Should Be $null
    }

    It "Extract should properly halt when -WhatIf is passed" {
        $table | Start-BqJob -Extract JSON "$gcspath/whatif.json" -WhatIf | Should Be $null
    }

    AfterAll {
        $test_set | Remove-BqDataset -Force
        Remove-GcsBucket $script:bucketName -Force
    }
}

Describe "Stop-BqJob" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
        $filename = "$folder\classics_large.csv"
        $table = New-BqTable -Dataset $test_Set "table_$r"
        New-BqSchema "Title" "STRING" | New-BqSchema "Author" "STRING" | New-BqSchema "Year" "INTEGER" | 
            Set-BqSchema $table | Add-BqTableRow CSV $filename -SkipLeadingRows 1
        $script:bucketName = "ps_test_$r"
        $bucket = New-GcsBucket $bucketName
        $gcspath = "gs://ps_test_$r"
    }

    It "should stop a query job" {
        $job = Start-BqJob -Query "select * from book_data.classics where Year > 1900"
        $res = $job | Stop-Bqjob 
        while ($res.Status.State -ne "DONE") {
            $res = $res | Get-BqJob
        }
        $res.Status.State | Should Be "DONE"
    }

    It "should handle jobs that are already done" {
        $job = Start-BqJob -Query "select * from book_data.classics" -Synchronous
        while ($job.Status.State -ne "DONE") {
            $job = $job | Get-BqJob
        }
        $res = $job | Stop-Bqjob | Get-BqJob
        $res.Status.State | Should Be "DONE"
    }

    It "should stop an extraction" {
        $job = $table | Start-BqJob -Extract CSV "$gcspath/basic.csv"
        $res = $job | Stop-Bqjob 
        while ($res.Status.State -ne "DONE") {
            $res = $res | Get-BqJob
        }
        $res.Status.State | Should Be "DONE"
    }

    AfterAll {
        $test_set | Remove-BqDataset -Force
        Remove-GcsBucket $script:bucketName -Force
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
