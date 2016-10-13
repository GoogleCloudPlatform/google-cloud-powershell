# Builds the Google Cloud Tools for PowerShell project, and packages the output so it can be
# integrated into the Google Cloud SDK. This is really of only use to Googlers doing the
# official releases of the PowerShell module.

Clear-Host
Write-Host -ForegroundColor Yellow "Building and Packaging Google Cloud Tools for PowerShell"
Write-Host

# Root of the repo. Assuming ran from the Tools folder.
$projectRoot = Join-Path $PSScriptRoot ".." -Resolve

# Folders for the solution and build outputs.
$slnFile = Join-Path $projectRoot "gcloud-powershell.sln"
$binDir = Join-Path $projectRoot "Google.PowerShell\bin\"
$debugDir = Join-Path $binDir "Debug"

# Folders where the build artifacts are stored, the things to be packaged up.
$packageDir = Join-Path $binDir "Packaged"
$powerShellDir = Join-Path $packageDir "PowerShell"
$gcpsDir = Join-Path $packageDir "GoogleCloudPowerShell"
$archivePath = Join-Path $packageDir "powershell-0.1.8.zip"

# Purge the existing bin directory.
Write-Host -ForegroundColor Cyan "*** Purging the bindir***"
if (Test-Path $binDir) {
    Remove-Item $binDir -Recurse -Force
}

# Build the project.
Write-Host -ForegroundColor Cyan "*** Building the project ***"

$msbuild = "c:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe"
& $msbuild @($slnFile, "/t:Clean,Build", "/p:Configuration=Debug")

if (-Not (Test-Path $debugDir)) {
    Write-Error "Build results not found."
    Exit
}

# HACK: Move the GoogleCloudPowerShell.psd1 into a different folder to keep Cloud SDK installations
# before 9/29/2016 working. We changed the location of the path we add to PSModulePath during the
# Cloud SDK installation. The old path (<CloudSDK>\platform\GoogleCloudPowerShell) will no longer
# exist, since the module is put in (<CloudSDK>\platform\PowerShell\GoogleCloud). We drop a module
# file in that location so that existing machines, with the old PSModulePath, will get updates.
# TODO(chrsmith): Given that only ~2 months of Cloud SDK installs are effected, and those machines
# may get paved, etc., and full uninstall/reinstall will fix it, consider removing this in H2 2017.
Write-Host -ForegroundColor Cyan "*** HACK: Creating GoogleCloudPowerShell module ***"

New-Item -ItemType Directory $packageDir
New-Item -ItemType Directory $gcpsDir
Move-Item (Join-Path $debugDir "GoogleCloudPowerShell.psd1") "$gcpsDir\GoogleCloudPowerShell.psd1"

# Package the bits. Requires setting up the right directory structure.
Write-Host -ForegroundColor Cyan "*** Packaging the bits ***"

New-Item -ItemType Directory $powerShellDir
Copy-Item -Recurse $debugDir $powerShellDir

# The binaries in a folder named "Debug". Rename that to "GooglePowerShell".
$moduleDir = Join-Path $powerShellDir "Debug"
Rename-Item $moduleDir "GoogleCloud"

# Ensure key files are in the right place.
Write-Host -ForegroundColor Cyan "*** Sanity checking ***"
function ConfirmExists($relativePath) {
   $fullPath = Join-Path $packageDir $relativePath
   if (-Not (Test-Path $fullPath)) {
       Write-Error "Expected file '$fullPath' does not exist."
       Exit
   }
}

ConfirmExists "PowerShell\GoogleCloud\GoogleCloud.psd1"
ConfirmExists "PowerShell\GoogleCloud\Google.PowerShell.dll"
ConfirmExists "PowerShell\GoogleCloud\BootstrapCloudToolsForPowerShell.ps1"
ConfirmExists "GoogleCloudPowerShell\GoogleCloudPowerShell.psd1"

Write-Host -ForegroundColor Cyan "*** Compressing ***"
Compress-Archive -Path @($powerShellDir, $gcpsDir) $archivePath

Write-Host -ForegroundColor Cyan "*** Complete ***"
Write-Host "The latest build is at:"
Write-Host $archivePath
