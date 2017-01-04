[CmdletBinding()]
Param(
    [Parameter(Mandatory = $true, Position=0)]
    [version]$publishVersion,

    [Parameter()]
    [ValidateSet("Debug", "Release")]
    [string]$configuration = "Release"
)
# Builds the Google Cloud Tools for PowerShell project, and packages the output so it can be
# integrated into the Google Cloud SDK. This is really of only use to Googlers doing the
# official releases of the PowerShell module.

Clear-Host
Write-Host -ForegroundColor Yellow "Building and Packaging Google Cloud Tools for PowerShell"
Write-Host

# Change version like 0.1.3 to 0.1.3.0 or 0.1 to 0.1.0.0.
$normalizedVersion = [version]::new(
                        $publishVersion.Major,
                        $publishVersion.Minor,
                        [math]::Max($publishVersion.Build, 0),
                        [math]::Max($publishVersion.Revision, 0))

# Root of the repo. Assuming ran from the Tools folder.
$projectRoot = Join-Path $PSScriptRoot ".." -Resolve

# Import the modified Archive module.
Import-Module (Join-Path $projectRoot "third_party\ArchiveCmdlets\Microsoft.PowerShell.Archive.psd1") -Force

# Folders for the solution and build outputs.
$slnFile = Join-Path $projectRoot "gcloud-powershell.sln"
$binDir = Join-Path $projectRoot "Google.PowerShell\bin\"
$configDir = Join-Path $binDir $configuration

# Folders where the build artifacts are stored, the things to be packaged up.
$packageDir = Join-Path $binDir "Packaged"
$powerShellDir = Join-Path $packageDir "PowerShell"
$gcpsDir = Join-Path $packageDir "GoogleCloudPowerShell"
$archivePath = Join-Path $packageDir "powershell-$normalizedVersion.zip"

# Purge the existing bin directory.
Write-Host -ForegroundColor Cyan "*** Purging the bindir***"
if (Test-Path $binDir) {
    Remove-Item $binDir -Recurse -Force
}

# String holder for export variables in module manifest.
# Read the followed function for more details.
$toBeReplaced = "TOBEREPLACED"

# Helper function to remove PrivateData from module manifest.
# If we do not do this, when we splat $moduleManifest to New-ModuleManifest,
# PrivateData will have 2 PSDaTa keys. Also, the key value pairs from PSData will
# also be lost. In addition, this function will also set VariablesToExport,
# FunctionsToExport and AliasesToExport to 'TOBEREPLACED' so we can replace them with
# @(). We have to do this because if we simply set them to an empty array, PowerShell
# will remove these values from the manifest but what we actually want is to set them to @().
function CleanupPrivateDataAndExportVariables($moduleManifest) {
    $privateData = $moduleManifest["PrivateData"]
    if ($null -ne $privateData -and $privateData.ContainsKey("PSData")) {
        $psData = $privateData["PSData"]
        if ($null -ne $psData) {
            $fieldNeededFromPSData = @("Tags", "LicenseUri", "ProjectUri", "IconUri", "ReleaseNotes")
            foreach ($field in $fieldNeededFromPSData) {
                if ($psData.ContainsKey($field)) {
                    $moduleManifest[$field] = $psData[$field]
                }
            }
        }
    }

    $moduleManifest.Remove("PrivateData")
    $moduleManifest["VariablesToExport"] = $toBeReplaced
    $moduleManifest["FunctionsToExport"] = $toBeReplaced
    $moduleManifest["AliasesToExport"] = $toBeReplaced
}

# Helper function to change the encoding of the module manifest file
# to UTF8. This is needed because module manifest created by
# New-ModuleManifest does not have UTF8 encoding.
# We also replaced 'TOBEREPLACED' with @()
function Set-ModuleManifestEncoding($moduleManifestFile) {
    $content = Get-Content $moduleManifestFile
    $content = $content.Replace("'$toBeReplaced'", "@()")
    Set-Content -Value $content -Encoding UTF8 $moduleManifestFile
}

# Change the module manifest version.
$moduleManifestFile = Join-Path $projectRoot "Google.PowerShell\ReleaseFiles\GoogleCloud.psd1"
$moduleManifestContent = Import-PowerShellDataFile $moduleManifestFile
CleanupPrivateDataAndExportVariables $moduleManifestContent
$moduleManifestContent.ModuleVersion = $normalizedVersion
New-ModuleManifest -Path $moduleManifestFile @moduleManifestContent
Set-ModuleManifestEncoding $moduleManifestFile

# Change version in AssemblyInfo file.
$assemblyInfoFile = Join-Path $projectRoot "Google.PowerShell\Properties\AssemblyInfo.cs"
$assemblyInfoContent = Get-Content -Raw $assemblyInfoFile
# Replace Version("oldversion") to Version("$normalizedVersion")
$assemblyInfoContent = $assemblyInfoContent -replace "Version\(`"[0-9\.]*`"\)", "Version(`"$normalizedVersion`")"
$assemblyInfoContent | Out-File -Encoding utf8 -FilePath $assemblyInfoFile -NoNewline

# Build the project.
Write-Host -ForegroundColor Cyan "*** Building the project ***"

$msbuild = "c:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe"
& $msbuild @($slnFile, "/t:Clean,Build", "/p:Configuration=$configuration")

if (-Not (Test-Path $configDir)) {
    Write-Error "Build results not found."
    Exit
}

# Get all cmdlets available in GoogleCloud module.
$googlePowerShellAssemblyPath = Resolve-Path (Join-Path $configDir GoogleCloud.psd1)
$getCmdletsScriptBlock = {
    param($modulePath)
    Import-Module $modulePath | Out-Null
    (Get-Module GoogleCloud).ExportedCmdlets.Keys
}
$job = Start-Job -ScriptBlock $getCmdletsScriptBlock -ArgumentList $googlePowerShellAssemblyPath
$cmdletsList = $job | Wait-Job | Receive-Job
Remove-Job $job

# Remove the cmdlets that we do not want to be public yet.
$betaCmdlets = & (Join-Path $PSScriptRoot "BetaCmdlets.ps1")
if ($null -ne $betaCmdlets) {
    $cmdletsList = $cmdletsList | Where-Object { $betaCmdlets -notcontains $_ }
}

# Modify the list of exported cmdlets.
$gCloudManifestPath = Join-Path $configDir "GoogleCloud.psd1"
$gCloudManifest = Import-PowerShellDataFile $gCloudManifestPath
CleanupPrivateDataAndExportVariables $gCloudManifest
$gCloudManifest.CmdletsToExport = $cmdletsList
New-ModuleManifest -Path $gCloudManifestPath @gCloudManifest
Set-ModuleManifestEncoding $gCloudManifestPath

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

# Add the version folder in front of Google.PowerShell.dll and GoogleCloudPlatform.Format.ps1xml.
$gCloudPowerShellManifest = Import-PowerShellDataFile (Join-Path $configDir "GoogleCloudPowerShell.psd1")
CleanupPrivateDataAndExportVariables $gCloudPowerShellManifest
$gCloudPowerShellManifest.RootModule = $gCloudPowerShellManifest.RootModule -replace "Google.PowerShell.dll", `
                                                                                   "$normalizedVersion\Google.PowerShell.dll"
$gCloudPowerShellManifest.FormatsToProcess = $gCloudPowerShellManifest.FormatsToProcess -replace "GoogleCloudPlatform.Format.ps1xml", `
                                                                                               "$normalizedVersion\GoogleCloudPlatform.Format.ps1xml"
$gCloudPowerShellManifest.CmdletsToExport = $cmdletsList
$gCloudPowerShellManifest.ModuleVersion = $normalizedVersion
New-ModuleManifest -Path "$gcpsDir\GoogleCloudPowerShell.psd1" @gCloudPowerShellManifest
Set-ModuleManifestEncoding "$gcpsDir\GoogleCloudPowerShell.psd1"

# Package the bits. Requires setting up the right directory structure.
Write-Host -ForegroundColor Cyan "*** Packaging the bits ***"

New-Item -ItemType Directory $powerShellDir
Copy-Item -Recurse $configDir $powerShellDir

# The binaries are in a folder named "$configuration". Move them to GoogleCloud\$normalizedVersion folder.
$moduleDir = Join-Path $powerShellDir $configuration
$googleCloudDir = Join-Path $powerShellDir "GoogleCloud\$normalizedVersion"
New-Item -ItemType Directory $googleCloudDir
if ($configuration -eq "Debug")
{
    Copy-Item -Recurse "$moduleDir\*" $googleCloudDir
}
else
{
    Copy-Item -Recurse "$moduleDir\*" $googleCloudDir -Exclude '*.pdb'
}
Remove-Item -Recurse $moduleDir -Force

# Ensure key files are in the right place.
Write-Host -ForegroundColor Cyan "*** Sanity checking ***"
function ConfirmExists($relativePath) {
   $fullPath = Join-Path $packageDir $relativePath
   if (-Not (Test-Path $fullPath)) {
       Write-Error "Expected file '$fullPath' does not exist."
       Exit
   }
}

ConfirmExists "PowerShell\GoogleCloud\$normalizedVersion\GoogleCloud.psd1"
ConfirmExists "PowerShell\GoogleCloud\$normalizedVersion\Google.PowerShell.dll"
ConfirmExists "PowerShell\GoogleCloud\$normalizedVersion\BootstrapCloudToolsForPowerShell.ps1"
ConfirmExists "GoogleCloudPowerShell\GoogleCloudPowerShell.psd1"

Write-Host -ForegroundColor Cyan "*** Compressing ***"
Compress-Archive -Path @($powerShellDir, $gcpsDir) $archivePath

Write-Host -ForegroundColor Cyan "*** Complete ***"
Write-Host "The latest build is at:"
Write-Host $archivePath

Write-Host "Generating Website"
$generateWebsiteFile = (Get-ChildItem "$PSScriptRoot\GenerateWebsiteData.ps1").FullName
# We do this on another process because the GenerateWebSiteData script will load the module, so in case we want to
# run this script again, we won't have a problem with removing the assemblies.
$process = Start-Process powershell.exe -NoNewWindow -ArgumentList "-File `"$generateWebsiteFile`" -Configuration $configuration" -PassThru
$process.WaitForExit()
$process.Dispose()
