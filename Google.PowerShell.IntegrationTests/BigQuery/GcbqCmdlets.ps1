. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

# Define variables that will be used in testing
$nonExistProject = "project-no-exist"
$accessErrProject = "asdf"
$oneDaySec = 86400
$threeDaySec = 259200
$oneDayMs = 86400000
$threeDayMs = 259200000

# Define functions that will be used in testing
