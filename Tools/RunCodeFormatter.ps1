# Installs and runs the codeformatter tool on the current solution.
# The easiest way to use this is to add it as a content file to your
# Visual Studio project, and whenever you want to run codeformatter
# just right click and select "Execute as Script"
#
# The tool will be installed to %LOCALAPPDATA%\CodeFormatter-XXX,
# suffixed by the release ID. If this script finds that the tool is
# present, the existing version will be used. This means that if
# the release ID changes, the latest version of codeformatter will
# be downloaded on the next run.

# Update from time-to-time by looking at:
# https://github.com/dotnet/codeformatter/releases
# TODO(chrsmith): Use the GitHub API and automate this, see:
# https://developer.github.com/v3/repos/releases/
$release = "v1.0.0-alpha6"
$latestBinaryDrop = "https://github.com/dotnet/codeformatter/releases/download/${release}/CodeFormatter.zip"

$installPath = "${env:LOCALAPPDATA}\CodeFormatter-${release}\"

# Download and install if needed. To delete an existing installation run:
# Remove-Item -Recurse -Force $installPath
If ( !(Test-Path $installPath) ) {
    New-Item -Type directory $installPath
    Invoke-WebRequest $latestBinaryDrop -OutFile "${installPath}\codeformatter.zip"

    Add-Type -Assembly "System.IO.Compression.FileSystem"
    [IO.Compression.ZipFile]::ExtractToDirectory(
        "${installPath}\codeformatter.zip",
        "${installPath}")

    Remove-Item "${installPath}\codeformatter.zip"
}

# TODO(chrsmith): Generalize this to search for any Solution file.
$solutionPath = Join-Path $PSScriptRoot "\..\gcloud-powershell.sln"

# Generate the copyright header.
$copyrightHeader = [IO.Path]::GetTempFileName() 
"// Copyright 2015-2016 Google Inc. All Rights Reserved." | Out-File -FilePath $copyrightHeader
"// Licensed under the Apache License Version 2.0."  | Out-File -FilePath $copyrightHeader -Append

$args = """${solutionPath}"" /copyright:""${copyrightHeader}"""

# Format
Write-Host "Running CodeFormatter on '${solutionPath}'"
Write-Host "with arguments [${args}]"
Start-Process "${installPath}\CodeFormatter\codeformatter.exe" $args -Wait

Remove-Item $copyrightHeader
