# Copyright 2015-2016 Google Inc. All Rights Reserved.
# Licensed under the Apache License Version 2.0.
#
# Updates Cloud Tools for PowerShell module to the latest found in 
# Google Cloud Storage bucket g-cloudsharp-unsignedbinaries.

# Let a user manually select a Cloud SDK install path
param($installPath)
$installPath = $installPath -or $args[0]

# Find the Google Cloud SDK install path from the registry.
if (-not $installPath) {
    $hklmPath = "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Google Cloud SDK"
    $hkcuPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Google Cloud SDK"
    if (Test-Path $hklmPath) {
        $installPath = Get-ItemPropertyValue $hklmPath InstallLocation
    } elseif (Test-Path $hkcuPath) {
        $installPath = Get-ItemPropertyValue $hkcuPath InstallLocation
    } else {
        Write-Error "Can not find Cloud SDK from the registry."
        return
    }
}
$installPath = $installPath -replace '"' # Registry values had quotes. This removes them.
Push-Location (Join-Path $installPath "google-cloud-sdk\platform\PowerShell")
$googlePowerShellPath = Resolve-Path "GoogleCloud"

if (-not (Test-Path $googlePowerShellPath)) {
    Write-Error "Can not find Cloud Tools for PowerShell. '$googlePowerShellPath' does not exist."
    return
}

$pathToOldCmdlets = "GoogleCloudPowerShell-unpatched-backup"
if (Test-Path $pathToOldCmdlets) {
    Remove-Item $pathToOldCmdlets -Recurse
}
Move-Item $googlePowerShellPath $pathToOldCmdlets
Import-Module "$pathToOldCmdlets/GoogleCloud.psd1"
$bucket = Get-GcsBucket g-cloudsharp-unsignedbinaries

# Find objects in the powershell directory, and select one most recently created.
$zipObject = Find-GcsObject $bucket -Prefix powershell | Sort TimeCreated -Descending | Select -First 1
$zipFileName = Split-Path $zipObject.Name -Leaf
Write-Verbose "Saving new file to $zipFileName"
Read-GcsObject $bucket $zipObject.Name -OutFile $zipFileName -Force

$zipPath = Resolve-Path $zipFileName
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, "$googlePowerShellPath\..")
Pop-Location
