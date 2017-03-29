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

    It "should get specific job via pipeline" {
        $jobs = Get-BqJob
        $job = $jobs[0] 
        $return = $job | Get-BqJob
        $return.JobReference.JobId | Should Be $job.JobReference.JobId
    }

    It "should get specific job via parameter" {
        $jobs = Get-BqJob
        $job = $jobs[0] 
        $return = Get-BqJob -JobId $job.JobReference.JobId
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

Reset-GCloudConfig $oldActiveConfig $configName
