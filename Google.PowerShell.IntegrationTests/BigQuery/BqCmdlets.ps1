. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

# Define variables that will be used in testing
$nonExistProject = "project-no-exist"
$nonExistDataset = "dataset_no_exist"
$nonExistTable = "table_no_exist"
$nonExistJob = "job_no_exist"
$accessErrProject = "asdf"
$oneDaySec = 60 * 60 * 24
$oneDayMs = 1000 * $oneDaySec
$threeDaySec = 3 * $oneDaySec
$threeDayMs = 3 * $oneDayMs

