Import-Module "C:\src\github.com\chrsmith\gcloud-powershell\Google.PowerShell\bin\Debug\Google.PowerShell.dll"

$cmdlets = Get-Command -Module "Google.PowerShell"

# Generate a single JSON file containing all the documentation for all the
# cmdlets. Unfortunately we can't split these into multiple files because of
# the way we generating web pages in Jekyll.
$cmdletDocObjects = @()
foreach ($cmdlet in $cmdlets) {
    Write-Host "Building $($cmdlet.Name)..."
    # Without Out-String the output of Get-Help will be a structured object,
    # which would be useful for a custom renderer of help output.
    $docText = Get-Help -Full $cmdlet.Name | Out-String
    $cmdletDocObj = @{ "cmdletName"=$cmdlet.Name; "documentation"=$docText }
    
    $cmdletDocObjects += $cmdletDocObj
}

$cmdletDocObjects `
| ConvertTo-Json -Depth 10 `
| Out-File -FilePath "cmdlets.json" -Encoding "UTF8"