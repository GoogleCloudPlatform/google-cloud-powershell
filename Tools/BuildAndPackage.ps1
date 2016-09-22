# Builds the Google Cloud Tools for PowerShell project, and packages the output so it can be
# integrated into the Google Cloud SDK. This is really of only use to Googlers doing the
# official releases of the PowerShell module.

# Bin directory for the Google.PowerShell project.
$binDir = Join-Path $PSScriptRoot "\..\Google.PowerShell\bin\Debug"
$binDir = [System.IO.Path]::GetFullPath($binDir)

# PowerShell directory, which contains our module.
$powerShellDir = Join-Path $binDir "..\PowerShell"
$powerShellDir = [System.IO.Path]::GetFullPath($powerShellDir)

Write-Host -ForegroundColor Yellow "Building and Packaging Google Cloud Tools for PowerShell`n"

# Purge the existing bin directory.
Write-Host -ForegroundColor Cyan "`n*** Purging the bin directory ***"
if (Test-Path $binDir) {
    Remove-Item $binDir -Confirm -Recurse
}
if (Test-Path $powerShellDir) {
    Remove-Item $powerShellDir -Confirm -Recurse
}

if ((Test-Path $binDir) -Or (Test-Path $powerShellDir)) {
    Write-Host -ForegroundColor Red "ERROR: bin directory not clear"
    Return
}
Write-Host "done`n"

# Build the project.
Write-Host -ForegroundColor Cyan "`n*** Building the project ***"

# TODO(chrsmith): Enable the build from this script. The project's PostBuild step requires some
# environment variables not defined in PowerShell. (I assume set from Visual Studio?)
# copy /Y "$(ProjectDir)\ReleaseFiles\*" "$(TargetDir)"
#     "$(SolutionDir)\third_party\XmlDoc2CmdletDoc\XmlDoc2CmdletDoc.exe" "$(TargetPath)"
# msbuild /t:Clean;Build $solutionPath
Read-Host @"
*******************
Build Google.PowerShell. Not automated, please do this in Visual Studio.
(press enter once build is complete)
*******************
"@
if (-Not (Test-Path $binDir)) {
    Write-Host -ForegroundColor Red "ERROR: new binaries not found"
    Return
}

# Package the bits. Requires setting up the right directory structure.
Write-Host -ForegroundColor Cyan "`n*** Packaging the bits ***"

New-Item -ItemType Directory $powerShellDir
Copy-Item -Recurse $binDir $powerShellDir

# The binaries in a folder named "Debug". Rename that to "GooglePowerShell".
$moduleDir = Join-Path $powerShellDir "Debug"
Rename-Item $moduleDir "GooglePowerShell"

$archivePath = Join-Path $powerShellDir "..\powershell-x.x.x.zip"
$archivePath = [System.IO.Path]::GetFullPath($archivePath)
Compress-Archive $powerShellDir $archivePath

Write-Host "done`n"

Write-Host -ForegroundColor Cyan "`n*** Complete ***"
Write-Host "The latest build is at:`n$archivePath"
