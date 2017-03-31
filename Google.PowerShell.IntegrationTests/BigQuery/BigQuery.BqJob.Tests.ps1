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

    It "should query out of a pre-loaded table" {
        $job = Start-BqJob -Query "select * from $datasetName.table_$r where Year > 1900"
        $job | Should Not Be $null
        #TODO(ahandley): Add in checking the data once receive is done.
    }

    It "should query out of a pre-loaded table" {
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
        #TODO(ahandley): Add in checking the data once receive is done.
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

Reset-GCloudConfig $oldActiveConfig $configName
