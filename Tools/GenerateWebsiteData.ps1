# Generates a JSON file containing the documentation information for all
# PowerShell cmdlets. This can be passed to the documentation website and
# -- by the magic of Jekyll -- be used to generate cmdlet-specific pages.
#
# After the script runs, copy `cmdlets.json` from the `Tools` folder into
# `\website\_data\` and then rerun Jekyll.

$binDirectory = Join-Path $PSScriptRoot "\..\Google.PowerShell\bin\"
Import-Module "$binDirectory\Debug\Google.PowerShell.dll"

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
    # TODO(chrsmith): Provide a "product" field, mapping "Gcs" to "Cloud Storage", etc.
    $cmdletDocObj = @{ `
        "cmdletName"=$cmdlet.Name; `
        "resource"=$cmdlet.Name.Split("-")[1];
        "documentation"=$docText `
    }
    
    $cmdletDocObjects += $cmdletDocObj
}

Write-Host "Saving cmdlets.json"
$cmdletDocObjects `
| ConvertTo-Json -Depth 10 `
| Out-File -FilePath "cmdlets.json" -Encoding "UTF8"
