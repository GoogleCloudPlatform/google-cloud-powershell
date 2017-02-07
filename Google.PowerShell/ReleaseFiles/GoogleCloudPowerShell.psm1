$script:GCloudModule = $ExecutionContext.SessionState.Module
$script:GCloudModulePath = $script:GCloudModule.ModuleBase

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
    }
}

Install-GCloudSdk
Import-Module "$script:GCloudModulePath\Google.PowerShell.dll"
