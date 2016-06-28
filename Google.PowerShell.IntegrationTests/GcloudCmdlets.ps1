# TODO(chrsmith): Provide a "initialize unit tests" method, which also sets common properties like $project.

# Install the GCP cmdlets module into the current PowerShell session.
function Install-GcloudCmdlets() {
    $dll = Get-ChildItem $PSScriptRoot\..\Google.PowerShell\bin -Recurse -Include Google.PowerShell.dll |
        sort -Descending -Property LastWriteTime |
        select -First 1
    Import-Module $dll
}

# Creates a GCS bucket owned associated with the project, deleting any existing
# buckets with that name and all of their contents.
function Create-TestBucket($project, $bucket) {
    gsutil -m rm -r "gs://${bucket}/*" 2>$null
    gsutil rb "gs://${bucket}" 2>$null
    gsutil mb -p $project "gs://${bucket}" 2>$null
}

# Copies a 0-byte file from the local machine to Google Cloud Storage.
function Add-TestFile($bucket, $objName) {
    $filename = [System.IO.Path]::GetTempFileName()
    gsutil ls "gs://${bucket}" 2>$null
    gsutil cp $filename "gs://${bucket}/${objName}" 2>$null
    Remove-Item -Force $filename
}

# Creates a new gcloud configuration and sets it to active. Returns project, zone, oldActiveConfig,
# and newConfigName.
function Set-GCloudConfig(){
    $project = "gcloud-powershell-testing"
    $zone = "us-central1-f"

    # parse the configurations list, creating objects with properties named by the first line of output.
    $configList = gcloud config configurations list 2>$null
    $oldActiveConfig = $configList -split [System.Environment]::NewLine |
         % {$_ -split "\s+" -join ","} | ConvertFrom-Csv | Where {$_.IS_ACTIVE -match "True"}

    $configRandom = Get-Random
    $configName = "testing$configRandom"
    gcloud config configurations create $configName 2>$null
    gcloud config configurations activate $configName 2>$null
    gcloud config set core/account $oldActiveConfig.ACCOUNT 2>$null
    gcloud config set core/project $project 2>$null
    gcloud config set compute/zone $zone 2>$null
    gcloud config set compute/region us-central1 2>$null
    
    return $project, $zone, $oldActiveConfig, $configName
}

# Reactivates the old active config and deletes the testing config
function Reset-GCloudConfig($oldConfig, $configName) {
    gcloud config configurations activate $oldConfig.NAME 2>$null
    gcloud config configurations delete $configName -q 2>$null
}
