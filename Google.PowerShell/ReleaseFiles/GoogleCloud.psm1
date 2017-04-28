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
    $noPrompt = $env:GCLOUD_SDK_INSTALLATION_NO_PROMPT -eq $true

    $query = "Do you want to install Google Cloud SDK? If you want to force the installation without prompt," +
             " set `$env:GCLOUD_SDK_INSTALLATION_NO_PROMPT to true."
    $caption = "Installing Google Cloud SDK"

    if ($PSCmdlet.ShouldProcess("Google Cloud SDK", "Install")) {
        if ($noPrompt) {
            Write-Host $GCloudSdkLicense

            # We use this method of installation instead of the installer because the installer does all the installation
            # in the background so we can't determine when it's done.
            $cloudSdkUri = "https://dl.google.com/dl/cloudsdk/channels/rapid/google-cloud-sdk.zip"
            $zipFileLocation = Join-Path $env:TMP ([System.IO.Path]::GetRandomFileName())
            $extractedFolder = Join-Path $env:TMP ([System.IO.Path]::GetRandomFileName())
            $installationPath = "$env:LOCALAPPDATA\Google\Cloud SDK"

            Invoke-WebRequest -Uri $cloudSdkUri -OutFile $zipFileLocation
            Add-Type -AssemblyName System.IO.Compression.FileSystem

            # This will extract it to a folder $env:APPDATA\google-cloud-sdk.
            Write-Host "Extracting Google Cloud SDK to '$extractedFolder' ..."
            [System.IO.Compression.ZipFile]::ExtractToDirectory($zipFileLocation, $extractedFolder)

            md $installationPath | Out-Null
            Write-Host "Moving Google Cloud SDK to '$installationPath' ..."
            Copy-Item "$extractedFolder\google-cloud-sdk" $installationPath -Recurse -Force

            # Set this to true to disable prompts.
            $env:CLOUDSDK_CORE_DISABLE_PROMPTS = $true
            Write-Host "Running installation script ..."
            & "$installationPath\google-cloud-sdk\install.bat" --quiet 2>$null

            $cloudBinPath = "$installationPath\google-cloud-sdk\bin"
            $envPath = [System.Environment]::GetEnvironmentVariable("Path")
            if (-not $envPath.Contains($cloudBinPath)) {
                [System.Environment]::SetEnvironmentVariable("Path", "$envPath;$cloudBinPath")
            }
        }
        else {
            if ($PSCmdlet.ShouldContinue($query, $caption)) {
                $cloudSdkInstaller = "https://dl.google.com/dl/cloudsdk/channels/rapid/GoogleCloudSDKInstaller.exe"
                $installerLocation = Join-Path $env:TMP "$([System.IO.Path]::GetRandomFileName()).exe"
                Invoke-WebRequest -Uri $cloudSdkInstaller -OutFile $installerLocation
                & $installerLocation
                Write-Warning "You may have to restart the shell before gcloud can be used."
            }
        }
        Write-Warning "Please also make sure to run 'gcloud init' before using the module."
    }
}

Install-GCloudSdk
Import-Module "$script:GCloudModulePath\Google.PowerShell.dll"
