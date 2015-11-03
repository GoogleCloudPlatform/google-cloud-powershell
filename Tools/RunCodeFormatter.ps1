# Installs and runs the codeformatter tool on the current solution.
# The easiest way to use this is to add it as a content file to your
# Visual Studio project, and whenever you want to run codeformatter
# just right click and select "Execute as Script"
#
# The tool will be installed to %LOCALAPPDATA%\CodeFormatter, and
# reuse an existing installation if it finds the binary is already
# present. While this means subsequent runs won't download the
# codeformatter release, it means that to upgrade to a new version
# you need to delete %LOCALAPPDATA%\CodeFormatter.

# Update from time-to-time by looking at:
# https://github.com/dotnet/codeformatter/releases
$release = "v1.0.0-alpha5"
$latestBinaryDrop = "https://github.com/dotnet/codeformatter/releases/download/${release}/CodeFormatter.zip"

$installPath = "${env:LOCALAPPDATA}\CodeFormatter\"

# Download and install if needed. To delete an existing installation run:
# Remove-Item -Recurse -Force $installPath
If ( !(Test-Path $installPath) ) {
	New-Item -Type directory $installPath
	Invoke-WebRequest $latestBinaryDrop -OutFile "${installPath}\codeformatter.zip"

	Add-Type -Assembly "System.IO.Compression.FileSystem"
    [IO.Compression.ZipFile]::ExtractToDirectory(
        "${installPath}\codeformatter.zip",
        "${installPath}\")

	Remove-Item "${installPath}\codeformatter.zip"
}

# TODO(chrsmith): Generalize this to search for any Solution file.
$solutionPath = Join-Path $PSScriptRoot "\..\gcloud-powershell.sln"

# Generate the copyright header.
$copyrightHeader = [IO.Path]::GetTempFileName() 
"// Copyright 2015 Google Inc. All Rights Reserved." | Out-File -FilePath $copyrightHeader
"// Licensed under the Apache License Version 2.0."  | Out-File -FilePath $copyrightHeader -Append

# Format
Write-Host "Running CodeFormatter on '${solutionPath}'"
Write-Host "with arguments [${args}]"
Start-Process "${installPath}\CodeFormatter\codeformatter.exe" $args -Wait

Remove-Item $copyrightHeader
