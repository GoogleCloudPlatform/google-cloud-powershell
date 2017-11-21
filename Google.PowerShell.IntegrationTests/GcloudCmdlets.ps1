# TODO(chrsmith): Provide a "initialize unit tests" method, which also sets common properties like $project.

# Install the GCP cmdlets module into the current PowerShell session.
function Install-GcloudCmdlets() {
    # Find the latest Google.PowerShell.dll that shares a folder with GoogleCloud.psd1.
    $dll = Get-ChildItem $PSScriptRoot\.. -Recurse -Include Google.PowerShell.dll |
        where {Test-Path (Join-Path $_.PSParentPath GoogleCloud.psd1)} |
        sort LastWriteTime -Descending |
        select -First 1

    # Set environment variable to disable Google Analytics metric reporting.
    # Shouldn't persist beyond the current PowerShell session.
    # Important: We have to set this first before calling Import-Module
    # as Import-Module will try to initialize the GCS PowerShell Provider,
    # which will actually creates an AnalyticsReport that will report
    # the data to production instead of debugging server.
    $env:DISABLE_POWERSHELL_ANALYTICS = "TRUE"

    # Copy all the assemblies file into fullclr folder since that is what
    # the psd1 file is expecting.
    $fullClrFolder = Join-Path $dll.PSParentPath fullclr
    if (-not (Test-Path $fullClrFolder)) {
        mkdir $fullClrFolder
        Copy-Item "$($dll.PSParentPath)\*" $fullClrFolder -Include *.pdb, *.xml, *.dll
    }

    # Import the GoogleCloud.psd1 in the folder of the latest dll.
    Join-Path $dll.PSParentPath GoogleCloud.psd1 | Import-Module
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

# Installs Cloud SDK non-interactively.
function Install-CloudSdk() {
    $cloudSdkUri = "https://dl.google.com/dl/cloudsdk/channels/rapid/google-cloud-sdk.zip"
    Invoke-WebRequest -Uri $cloudSdkUri -OutFile "$env:APPDATA\gcloudsdk.zip"
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    # This will extract it to a folder $env:APPDATA\google-cloud-sdk.
    [System.IO.Compression.ZipFile]::ExtractToDirectory("$env:APPDATA\gcloudsdk.zip", "$env:APPDATA")
    
    $installationPath = "$env:LOCALAPPDATA\Google\Cloud SDK"

    md $installationPath
    Copy-Item "$env:APPDATA\google-cloud-sdk" $installationPath -Recurse -Force

    # Set this to true to disable prompts.
    $env:CLOUDSDK_CORE_DISABLE_PROMPTS = $true
    & "$installationPath\google-cloud-sdk\install.bat" --quiet 2>$null

    $cloudBinPath = "$installationPath\google-cloud-sdk\bin"
    $envPath = [System.Environment]::GetEnvironmentVariable("Path")
    if (-not $envPath.Contains($cloudBinPath)) {
        [System.Environment]::SetEnvironmentVariable("Path", "$envPath;$cloudBinPath")
    }
}

# Runs pester test in folder $env:test_folder and throws error if any test fails.
function Start-PesterTest() {
    $testResult = Invoke-Pester "$PSScriptRoot\$env:test_folder" -PassThru
    if ($testResult.FailedCount -gt 0) {
        throw "$($testResult.FailedCount) tests failed."
    }
}
