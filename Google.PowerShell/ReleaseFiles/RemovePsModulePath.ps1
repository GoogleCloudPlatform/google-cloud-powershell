# Get the ID and security principal of the current user account
$windowsId = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$windowsPrincipal = New-Object System.Security.Principal.WindowsPrincipal($windowsId)
 
# Get the security principal for the Administrator role
$adminRole = [System.Security.Principal.WindowsBuiltInRole]::Administrator

$escapedPathRegex = [Regex]::Escape($PSScriptRoot)
if($env:PSModulePath -split ";" -contains $PSScriptRoot)
{
    Write-Verbose "Removing from environment variable"
    $env:PSModulePath = $env:PSModulePath -replace "$escapedPathRegex;?"
    Write-Verbose "env:PSModulePath = $env:PSModulePath"
}
$regKeyPath = 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment'
Push-Location $regKeyPath
$regValue = Get-ItemPropertyValue . PSModulePath
if($regValue -split ";" -contains $PSScriptRoot)
{
    Write-Verbose "Removing from registry key"
    $newValue = ($regValue -replace "$escapedPathRegex;?")
    if ($windowsPrincipal.IsInRole($adminRole))
    {
        Set-ItemProperty . PSModulePath $newValue
    }
    else
    {
        Start-Process Powershell -Verb RunAs -Wait "-NoLogo", "-NoProfile", "-NonInteractive",
            "-File $($MyInvocation.MyCommand.Definition)"
    }
    Write-Verbose "regKey:PSModulePath = $(Get-ItemPropertyValue . PSModulePath)"
}
Pop-Location
