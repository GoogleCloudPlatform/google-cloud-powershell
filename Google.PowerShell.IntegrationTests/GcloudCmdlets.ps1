﻿# TODO(chrsmith): Provide a "initialize unit tests" method, which also sets common properties like $project.

# Install the GCP cmdlets module into the current PowerShell session.
function Install-GcloudCmdlets() {
    $dll = Get-ChildItem $PSScriptRoot\..\Google.PowerShell\bin -Recurse -Include Google.PowerShell.dll |
        sort -Property CreationTime | select -First 1
    Import-Module $dll -Verbose
}

# Creates a GCS bucket owned associated with the project, deleting any existing
# buckets with that name and all of their contents.
function Create-TestBucket($project, $bucket) {
    gsutil -m rm -r "gs://${bucket}/*"
    gsutil rb "gs://${bucket}"
    gsutil mb -p $project "gs://${bucket}"
}

# Copies a 0-byte file from the local machine to Google Cloud Storage.
function Add-TestFile($bucket, $objName) {
    $filename = [System.IO.Path]::GetTempFileName()
    gsutil ls "gs://${bucket}"
    gsutil cp $filename "gs://${bucket}/${objName}"
    Remove-Item -Force $filename
}
<#
# Creates a new gcloud configuration and sets it to active. Returns project, zone, oldActiveConfig,
# and newConfigName.
#>
function Set-GcloudConfig(){
    $project = "gcloud-powershell-testing"
    $zone = "us-central1-f"
    
    $ErrorActionPreference = 'SilentlyContinue'

    # parse the configurations list, creating objects with properties named by the first line of output.
    $configList = gcloud config configurations list
    $oldActiveConfig = $configList -split [System.Environment]::NewLine |
         % {$_ -split "\s+" -join ","} | ConvertFrom-Csv | Where {$_.IS_ACTIVE -match "True"}

    $configRandom = Get-Random
    $configName = "testing$configRandom"
    gcloud config configurations create $configName
    gcloud config configurations activate $configName
    gcloud config set core/account $oldActiveConfig.ACCOUNT
    gcloud config set core/project $project
    gcloud config set compute/zone $zone
    
    $ErrorActionPreference = 'Continue'
    return $project, $zone, $oldActiveConfig, $configName
}

# Reactivates the old active config and deletes the testing config
function Reset-GcloudConfig($oldConfig, $configName) {
    $ErrorActionPreference = "SilentlyContinue"
    gcloud config configurations activate $oldConfig.NAME
    gcloud config configurations delete $configName -q
    $ErrorActionPreference = "Continue"
}
