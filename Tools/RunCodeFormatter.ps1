# This is pretty jank, but it works.
$solutionPath = $PSScriptRoot + "\..\gcloud-powershell.sln"

# Generate the copyright header.
$copyrightHeader = [IO.Path]::GetTempFileName() 
"// Copyright 2015 Google Inc. All Rights Reserved." | Out-File -FilePath $copyrightHeader

# Format
Write-Host "Running CodeFormatter on $solutionPath..."
codeformatter $solutionPath /copyright:$copyrightHeader

Remove-Item $copyrightHeader

Write-Host "(press any key to continue)"codeformatter
$host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")