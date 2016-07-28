# Copyright 2016 Google Inc. All Rights Reserved.
# Licensed under the Apache License Version 2.0.
#
# Updates Cloud Tools for PowerShell module to the latest found in Google Cloud Storage bucket g-cloudsharp-unsignedbinaries

param($installPath) # Let a user manually select a Cloud SDK install path
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
$installPath = $installPath -replace '"' # Registry values had quotes for some reason
$googlePowerShellPath = Join-Path $installPath "google-cloud-sdk\platform\GoogleCloudPowerShell"

if (-not (Test-Path $googlePowerShellPath)) {
    Write-Error "Can not find Google PowerShell. $googlePowerShellPath does not exist."
    return
}

$pathToOldCmdlets = "$googlePowerShellPath\..\OldPowerShell"
Remove-Item $pathToOldCmdlets -Recurse
Move-Item $googlePowerShellPath $pathToOldCmdlets
Import-Module "$pathToOldCmdlets/GoogleCloudPowerShell.psd1"
$bucket = Get-GcsBucket g-cloudsharp-unsignedbinaries
$zipObject = Find-GcsObject $bucket -Prefix powershell | Sort Name -Descending | Select -First 1
$tempFile = New-TemporaryFile
Read-GcsObject $bucket $zipObject.Name -OutFile $tempFile -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($tempFile, "$googlePowerShellPath\..")
Remove-Item $tempFile