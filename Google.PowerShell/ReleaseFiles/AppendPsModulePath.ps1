# Copyright 2016 Google Inc. All Rights Reserved.
# Licensed under the Apache License Version 2.0.
#
# Appends the Google PowerShell module location to the registry's PSModulePath environment variable

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

# Get the value of the two registry keys that initialize the environment variable.
$hklmLocation = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"
$hklmValue = (Get-ItemProperty $hklmLocation).PSModulePath
$hkcuLocation = "HKCU:\Environment"
$hkcuValue = (Get-ItemProperty $hkcuLocation).PSModulePath
$regValue = (($hklmValue, $hkcuValue | ?{$_}) -join ";")

if(($regValue -split ";" -contains $googlePowerShellPath))
{
    Write-Warning "Path already exists."
    return
}

$wid = [Security.Principal.WindowsIdentity]::GetCurrent()
$wip = New-Object Security.Principal.WindowsPrincipal $wid
$isElevated = $wip.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)

if ($isElevated -and (Test-Path $hklmPath))
{
    # If we are running as administrator, and the Cloud SDK is installed for all users,
    # append to the PSModluePath in the local machine registry location.
    Write-Verbose "Adding to registry key for all users."
    Push-Location $hklmLocation
    # Don't add a semicolon if $hklmValue is $null
    Set-ItemProperty . PSModulePath (($hklmValue, $googlePowerShellPath | ?{$_}) -join ";")
}
else
{
    # If we are running as a local user, or Cloud SDK is installed for just the current user,
    # append to the PSModluePath in the current user registry location.
    Write-Verbose "Adding to registry key for user $env:UserName."
    Push-Location $hkcuLocation
    # Don't add a semicolon if $hkcuValue is $null
    Set-ItemProperty . PSModulePath (($hkcuValue, $googlePowerShellPath | ?{$_}) -join ";")
}
Write-Output (Get-ItemProperty . PSModulePath)
Pop-Location
