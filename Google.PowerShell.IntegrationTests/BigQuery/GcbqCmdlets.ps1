. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

# Define variables that will be used in testing
$project = "gcloud-powershell-testing"

$nonExistProject = "project-no-exist"
$accessErrProject = "asdf"

# Define functions that will be used in testing

function Test-Funct($fileName) {
    if (Test-Path $fileName) {
        Remove-Item $fileName
    }
}
