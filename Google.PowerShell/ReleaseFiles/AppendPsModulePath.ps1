# Get the ID and security principal of the current user account
$windowsId = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$windowsPrincipal = New-Object System.Security.Principal.WindowsPrincipal($windowsId)
 $VerbosePreference = "Continue"
# Get the security principal for the Administrator role
$adminRole = [System.Security.Principal.WindowsBuiltInRole]::Administrator

if(-not ($env:PSModulePath -split ";" -contains $PSScriptRoot))
{
    Write-Verbose "Adding to environment variable"
    $env:PSModulePath = "$PSScriptRoot;$env:PSModulePath"
    Write-Verbose "PSModulePath = $env:PSModulePath"
}
Push-Location 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment'
$regValue = Get-ItemPropertyValue . PSModulePath
if(-not ($regValue -split ";" -contains $PSScriptRoot))
{
    Write-Verbose "Adding to registry key"
    if ($windowsPrincipal.IsInRole($adminRole))
    {
        Set-ItemProperty . PSModulePath "$PSScriptRoot;$regValue"
    }
    else
    {
        Start-Process Powershell -Verb RunAs -Wait "-NoLogo", "-NoProfile", "-NonInteractive",
                "-File $($MyInvocation.MyCommand.Definition)"
    }
    Write-Verbose "PSModulePath = $(Get-ItemPropertyValue . PSModulePath)"
}
Pop-Location
