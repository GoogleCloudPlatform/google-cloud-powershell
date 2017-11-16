$script:GCloudModule = $ExecutionContext.SessionState.Module
$script:GCloudModulePath = $script:GCloudModule.ModuleBase
$script:GCloudSdkLicense = @"
The Google Cloud SDK and its source code are licensed under Apache
License v. 2.0 (the "License"), unless otherwise specified by an alternate
license file.

You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Note that if you use the Cloud SDK with any Google Cloud Platform products,
your use is additionally going to be governed by the license agreement or
terms of service, as applicable, of the underlying Google Cloud Platform
product with which you are using the Cloud SDK. For example, if you are
using the Cloud SDK with Google App Engine, your use would additionally be
governed by the Google App Engine Terms of Service.

This also means that if you were to create works that call Google APIs, you
would still need to agree to the terms of service (usually, Google's
Developer Terms of Service at https://developers.google.com/terms) for those
APIs separately, as this code does not grant you any special rights to use
the services.

We collect anonymized usage data and anonymized stacktraces when crashes are encountered;
additional information is available at <https://cloud.google.com/sdk/usage-statistics>.

You may opt out of this collection at any time in the future by running the following command:
 gcloud config set disable_usage_reporting true

By installing the Cloud SDK, you accept the terms of the license.

"@
$script:gCloudInitWarning = "You will have to restart the shell and/or run 'gcloud init' " +
    "(if you haven't run it after installing the SDK) before the module can be used."
$script:installingSdkActivity = "Installing Google Cloud SDK"

# This function returns true if we are running PowerShell on Windows.
function IsWindows() {
    if ($PSVersionTable.PSEdition -ne "Core") {
        return $true
    }

    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)) {
        return $true
    }

    return $false
}

# Check and install Google Cloud SDK if it is not present. To install it non-interactively,
# set GCLOUD_SDK_INSTALLATION_NO_PROMPT to $true.
function Install-GCloudSdk {
    [CmdletBinding(SupportsShouldProcess = $true)]
    Param()

    $gCloudSDK = Get-Command gcloud -ErrorAction SilentlyContinue

    if ($null -ne $gCloudSDK) {
        return
    }

    Write-Host "Google Cloud SDK is not found in PATH. The SDK is required to run the module."
    $noPrompt = $env:GCLOUD_SDK_INSTALLATION_NO_PROMPT -eq $true -or $args -match "-?quiet"

    $query = "Do you want to install Google Cloud SDK? If you want to force the installation without prompt," +
             " set `$env:GCLOUD_SDK_INSTALLATION_NO_PROMPT to true or add '-quiet' to Import-Module -ArgumentList."
    $caption = "Installing Google Cloud SDK"

    $uiQuery = "Do you want to use the interactive installer? Select no to install silently on the command line."
    $uiCaption = "Installing Google Cloud SDK interactively"

    if ($PSCmdlet.ShouldProcess("Google Cloud SDK", "Install")) {
        if ($noPrompt) {
            Install-GCloudSdkSilently
        }
        else {
            if ($PSCmdlet.ShouldContinue($query, $caption)) {
                if ($PSCmdlet.ShouldContinue($uiQuery, $uiCaption)) {
                    Install-GCloudSdkInteractively
                }
                else {
                    Install-GCloudSdkSilently
                    gcloud init
                }
            }
        }
    }
}

function Install-GCloudSdkInteractively() {
    if (IsWindows) {
        $cloudSdkInstaller = "https://dl.google.com/dl/cloudsdk/channels/rapid/GoogleCloudSDKInstaller.exe"
        $installerLocation = Join-Path $env:TMP "$([System.IO.Path]::GetRandomFileName()).exe"

        Write-Progress -Activity $installingSdkActivity `
                        -Status "Downloading interactive installer to $installerLocation."

        # Set this to hide the progress bar from Invoke-WebRequest, which is not very useful.
        $ProgressPreference = "SilentlyContinue"
        Invoke-WebRequest -Uri $cloudSdkInstaller -OutFile $installerLocation
        $ProgressPreference = "Continue"

        Write-Progress -Activity $installingSdkActivity `
                        -Status "Launching interactive installer. Blocking until installation is complete."
        Start-Process $installerLocation -Wait
        Write-Progress -Activity $installingSdkActivity -Completed
    }
    else {
        curl https://sdk.cloud.google.com | bash
    }
    Write-Warning $gCloudInitWarning
}

function Install-GCloudSdkSilently() {
    Write-Host $GCloudSdkLicense

    if (-not (IsWindows)) {
        curl https://sdk.cloud.google.com | bash -s -- --disable-prompts
        $cloudBinPath = "$HOME\google-cloud-sdk\bin"
        $envPath = [System.Environment]::GetEnvironmentVariable("PATH")
        if (-not $envPath.Contains($cloudBinPath)) {
            [System.Environment]::SetEnvironmentVariable("PATH", "$($envPath):$cloudBinPath")
        }
        return
    }

    # We use this method of installation instead of the installer because the installer does all the installation
    # in the background so we can't determine when it's done.
    $cloudSdkUri = "https://dl.google.com/dl/cloudsdk/channels/rapid/google-cloud-sdk.zip"
    $zipFileLocation = Join-Path $env:TMP ([System.IO.Path]::GetRandomFileName())
    $extractedFolder = Join-Path $env:TMP ([System.IO.Path]::GetRandomFileName())
    $installationPath = "$env:LOCALAPPDATA\Google\Cloud SDK"

    Write-Progress -Activity $installingSdkActivity `
                   -Status "Downloading latest version of Cloud SDK to $zipFileLocation."

    # Set this to hide the progress bar from Invoke-WebRequest, which is not very useful.
    $ProgressPreference = "SilentlyContinue"
    Invoke-WebRequest -Uri $cloudSdkUri -OutFile $zipFileLocation
    $ProgressPreference = "Continue"

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    # This will extract it to a folder $env:APPDATA\google-cloud-sdk.
    Write-Progress -Activity $installingSdkActivity `
                   -Status "Extracting Google Cloud SDK to '$extractedFolder' ..."
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipFileLocation, $extractedFolder)

    if (-not (Test-Path $installationPath)) {
        md $installationPath | Out-Null
    }
    Write-Progress -Activity $installingSdkActivity `
                   -Status "Moving Google Cloud SDK to '$installationPath' ..."
    Copy-Item "$extractedFolder\google-cloud-sdk" $installationPath -Recurse -Force

    # Set this to true to disable prompts.
    $env:CLOUDSDK_CORE_DISABLE_PROMPTS = $true
    Write-Progress -Activity $installingSdkActivity `
                   -Status "Running installation script ..."
    & "$installationPath\google-cloud-sdk\install.bat" --quiet 2>$null

    $cloudBinPath = "$installationPath\google-cloud-sdk\bin"
    $envPath = [System.Environment]::GetEnvironmentVariable("Path")
    if (-not $envPath.Contains($cloudBinPath)) {
        [System.Environment]::SetEnvironmentVariable("Path", "$envPath;$cloudBinPath")
    }

    # We need to set this to false so user can run gcloud init after if they want.
    $env:CLOUDSDK_CORE_DISABLE_PROMPTS = $false

    Write-Progress -Activity $installingSdkActivity -Completed
}

Install-GCloudSdk

# Import either .NET Core or .NET Full version of the module based on
# the edition of PowerShell.
if ($PSVersionTable.PSEdition -eq "Core") {
    Import-Module "$script:GCloudModulePath\coreclr\Google.PowerShell.dll"
}
else {
    Import-Module "$script:GCloudModulePath\fullclr\Google.PowerShell.dll"
}

function gs:() {
    <#
    .SYNOPSIS
    Changes the directory to the Google Cloud Storage drive.
    .DESCRIPTION
    This function changes the directory to the Google Cloud Storage drive.
    It can be called before the Google Cloud PowerShell module is imported.
    #>
    cd gs:
}
