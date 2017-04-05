. $PSScriptRoot\..\BigQuery\BqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-BqJob" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
        $folder = Get-Location
        $folder = $folder.ToString()
        $filename = "$folder\classics.csv"
        $table = New-BqTable -Dataset $test_Set "table_$r"
        New-BqSchema -Name "Title" -Type "STRING" | New-BqSchema -Name "Author" -Type "STRING" |
            New-BqSchema -Name "Year" -Type "INTEGER" | Set-BqSchema $table | 
            Add-BqTabledata $filename CSV -SkipLeadingRows 1
        $table | Add-BqTabledata $filename CSV -SkipLeadingRows 1
    }

    It "should list jobs from the past 6 months" {
        $jobs = Get-BqJob
        $jobs.Count | Should BeGreaterThan 1
    }

    #TODO(ahandley): When Start- and Stop-BqJob are written, add in tests for AllUsers and State
    #TODO(ahandley): When Start- is ready, add test with alternate project via jobReference

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
        { Get-BqJob $nonExistJob -ErrorAction Stop } | Should Throw "404"
    }

    It "should throw when the project is not found"{
        { Get-BqJob -project $nonExistProject -ErrorAction Stop } | Should Throw "404"
    }

    It "should handle projects that the user does not have permissions for" {
        { Get-BqJob -Project $accessErrProject -ErrorAction Stop } | Should Throw "400"
    }

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Describe "BqJob-Query" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
        $folder = Get-Location
        $folder = $folder.ToString()
        $filename = "$folder\classics.csv"
        $table = New-BqTable -Dataset $test_Set "table_$r"
        New-BqSchema -Name "Title" -Type "STRING" | New-BqSchema -Name "Author" -Type "STRING" |
            New-BqSchema -Name "Year" -Type "INTEGER" | Set-BqSchema $table | 
            Add-BqTabledata $filename CSV -SkipLeadingRows 1
    }

    It "should query a pre-loaded table" {
        $job = Start-BqJob -Query "select * from $datasetName.table_$r where Year > 1900"
        $job | Should Not Be $null
        $results = $job | Receive-BqJob
        $results.Count | Should Be 2
        $results[0]["Author"] | Should Be "Orson Scott Card"
        $results[1]["Year"] | Should Be 1967
    }

    It "should query a pre-loaded table with more options than ever before!" {
        $alt_tab = New-BqTable -Dataset $test_Set "table_res_$r"
        $job = Start-BqJob -Query "select * from $datasetName.table_$r where Year > 1900" `
                           -DefaultDataset $test_set -DestinationTable $alt_tab -PollUntilComplete
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
        $folder = Get-Location
        $folder = $folder.ToString()
        $filename = "$folder\classics.csv"
        $filename_other = "$folder\otherschema.csv"

        $table = New-BqTable -Dataset $test_Set "table_$r"
        New-BqSchema -Name "Title" -Type "STRING" | New-BqSchema -Name "Author" -Type "STRING" |
            New-BqSchema -Name "Year" -Type "INTEGER" | Set-BqSchema $table | 
            Add-BqTabledata $filename CSV -SkipLeadingRows 1

        $table_other = New-BqTable -Dataset $test_Set "table_other_$r"
        New-BqSchema -Name "Position" -Type "INTEGER" | New-BqSchema -Name "Number" -Type "INTEGER" |
            New-BqSchema -Name "Average" -Type "FLOAT" | Set-BqSchema $table_other | 
            Add-BqTabledata $filename_other CSV -SkipLeadingRows 1
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

Describe "Stop-BqJob" {

    BeforeAll {
        $r = Get-Random
        $datasetName = "pshell_testing_$r"
        $test_set = New-BqDataset $datasetName
        $folder = Get-Location
        $folder = $folder.ToString()
        $filename = "$folder\classics_large.csv"
        $table = New-BqTable -Dataset $test_Set "table_$r"
        New-BqSchema -Name "Title" -Type "STRING" | New-BqSchema -Name "Author" -Type "STRING" |
            New-BqSchema -Name "Year" -Type "INTEGER" | Set-BqSchema $table | 
            Add-BqTabledata $filename CSV -SkipLeadingRows 1
    }

    It "should stop a query job" {
        $job = Start-BqJob -Query "select * from book_data.classics where Year > 1900"
        $res = $job | Stop-Bqjob | Get-BqJob
        $res.Status.State | Should Be "DONE"
    }

    It "should handle jobs that are already done" {
        $job = Start-BqJob -Query "select * from book_data.classics" -Synchronous
        $job = $job | Get-BqJob
        $job.Status.State | Should Be "DONE"
        $res = $job | Stop-Bqjob | Get-BqJob
        $res.Status.State | Should Be "DONE"
    }

    #TODO(ahandley): Add a few more test cases with different types of jobs that may run longer than a single query

    AfterAll {
        $test_set | Remove-BqDataset -Force
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
