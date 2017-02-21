. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

# Define variables that will be used in testing
$nonExistProject = "project-no-exist"
$accessErrProject = "asdf"
$oneDaySec = 60 * 60 * 24
$oneDayMs = 1000 * $oneDaySec
$threeDaySec = 3 * $oneDaySec
$threeDayMs = 3 * $oneDayMs

# Define functions that will be used in testing
