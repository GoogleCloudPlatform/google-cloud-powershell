$hklmPath = "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Google Cloud SDK"
if(Test-Path $HklmPath) {
    $installPath = Get-ItemPropertyValue $HklmPath InstallLocation
} else {
    $hkcuPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Google Cloud SDK"
    $installPath = Get-ItemPropertyValue $HkcuPath InstallLocation
}
$installPath = $installPath -replace '"'
$googlePowerShellPath = "$installPath\platform\GoogleCloudPowerShell"

$hklmLocation = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"
$hklmValue = Get-ItemPropertyValue $hklmLocation PSModulePath
$hkcuLocation = "HKCU:\Environment"
$hkcuValue = Get-ItemPropertyValue $hkcuLocation PSModulePath -ErrorAction SilentlyContinue
$regValue = "$hklmValue$hkcuValue"
if(-not ($regValue -split ";" -contains $googlePowerShellPath))
{
    if (Test-IsElevated -and (Test-Path $HklmPath))
    {
        Write-Verbose "Adding to registry key for all users."
        Push-Location $hklmLocation
        Set-ItemProperty . PSModulePath "$hklmValue;$googlePowerShellPath"
    }
    else
    {
        Write-Verbose "Adding to registry key for this user."
        Push-Location $hkcuLocation
        Set-ItemProperty . PSModulePath "$hkcuValue;$googlePowerShellPath"
    }
    Write-Verbose "PSModulePath = $(Get-ItemPropertyValue . PSModulePath)"
    Pop-Location
}
