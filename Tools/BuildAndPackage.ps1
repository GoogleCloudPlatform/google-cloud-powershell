# Builds the Google Cloud Tools for PowerShell project, and packages the output so it can be
# integrated into the Google Cloud SDK. This is really of only use to Googlers doing the
# official releases of the PowerShell module.

# Bin directory for the Google.PowerShell project.
$binDir = Join-Path $PSScriptRoot "\..\Google.PowerShell\bin\"
$binDir = [System.IO.Path]::GetFullPath($binDir)
$debugDir = Join-Path $binDir "Debug"

# PowerShell directory, which contains our module.
$powerShellDir = Join-Path $binDir "PowerShell"

# GoogleCloudPowerShell directory, which contains a faux module for backwards compat.
$gcpsDir = Join-Path $binDir "GoogleCloudPowerShell"

# The final archive we are producing.
$archivePath = Join-Path $binDir "powershell-x.x.x.zip"

Clear-Host
Write-Host -ForegroundColor Yellow "Building and Packaging Google Cloud Tools for PowerShell`n"

# Purge the existing bin directory.
Write-Host -ForegroundColor Cyan "`n*** Purging the bindir***"
if (Test-Path $binDir) {
    Remove-Item $binDir -Recurse -Force
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
if (-Not (Test-Path $debugDir)) {
    Write-Host -ForegroundColor Red "ERROR: new binaries not found"
    Exit
}

# HACK: Move the GoogleCloudPowerShell.psd1 into a different folder to keep Cloud SDK installations
# before 9/29/2016 working. We changed the location of the path we add to PSModulePath during the
# Cloud SDK installation. The old path (<CloudSDK>\platform\GoogleCloudPowerShell) will no longer
# exist, since the module is put in (<CloudSDK>\platform\PowerShell\GoogleCloud). We drop a module
# file in that location so that existing machines, with the old PSModulePath, will get updates.
# TODO(chrsmith): Given that only ~2 months of Cloud SDK installs are effected, and those machines
# may get paved, etc., and full uninstall/reinstall will fix it, consider removing this in H2 2017.
Write-Host -ForegroundColor Cyan "`n*** HACK: Creating GoogleCloudPowerShell module ***"
Write-Host "done`n"

New-Item -ItemType Directory $gcpsDir
Move-Item (Join-Path $debugDir "GoogleCloudPowerShell.psd1") "$gcpsDir\GoogleCloudPowerShell.psd1"

# Package the bits. Requires setting up the right directory structure.
Write-Host -ForegroundColor Cyan "`n*** Packaging the bits ***"

New-Item -ItemType Directory $powerShellDir
Copy-Item -Recurse $debugDir $powerShellDir

# The binaries in a folder named "Debug". Rename that to "GooglePowerShell".
$moduleDir = Join-Path $powerShellDir "Debug"
Rename-Item $moduleDir "GoogleCloud"

# Ensure key files are in the right place.
Write-Host -ForegroundColor Cyan "`n*** Sanity checking ***"
function ConfirmExists($relativePath) {
   $fullPath = Join-Path $binDir $relativePath
   if (-Not (Test-Path $fullPath)) {
       Write-Host -ForegroundColor Red "ERROR: Expected file $fullPath does not exist."
       Exit
   }
}
ConfirmExists "PowerShell\GoogleCloud\GoogleCloud.psd1"
ConfirmExists "PowerShell\GoogleCloud\Google.PowerShell.dll"
ConfirmExists "PowerShell\GoogleCloud\BootstrapCloudToolsForPowerShell.ps1"
ConfirmExists "GoogleCloudPowerShell\GoogleCloudPowerShell.psd1"
Write-Host "done"

Write-Host -ForegroundColor Cyan "`n*** Compressing ***"
Compress-Archive -Path @($powerShellDir, $gcpsDir) $archivePath
Write-Host "done"

# Fin.
Write-Host -ForegroundColor Cyan "`n*** Complete ***"
Write-Host "The latest build is at:`n$archivePath"
