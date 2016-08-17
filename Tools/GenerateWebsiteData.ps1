# Generates a JSON file containing the documentation information for all
# PowerShell cmdlets. This can be passed to the documentation website and
# -- by the magic of Jekyll -- be used to generate cmdlet-specific pages.

$binDirectory = Join-Path $PSScriptRoot "\..\Google.PowerShell\bin\"
Import-Module "$binDirectory\Debug\Google.PowerShell.dll"

$cmdlets = Get-Command -Module "Google.PowerShell"

# Mapping between cmdlet name and the Cloud resource. This is used to organize
# the cmdlets on the generated website.
$apiMappings = @{
    "Gcs" = "Google Cloud Storage"
    "Gce" = "Google Compute Engine"
}

# Generate a single JSON file containing all the documentation for all the
# cmdlets. Unfortunately we can't split these into multiple files because of
# the way we generating web pages in Jekyll/angular..
$cmdletDocObjects = @()

#Allows us to separate out cmdlets by product.
$cmdletDocObjectsGcs = @{}
$cmdletDocObjectsGce = @{}
foreach ($cmdlet in $cmdlets) {
    Write-Host "Building $($cmdlet.Name)..."

    $cmdletVerb = $cmdlet.Name.Split("-")[0]
    $cmdletResource = $cmdlet.Name.Split("-")[1]

    # Get documentation on the cmdlet.
    $docText = Get-Help -Full $cmdlet.Name | Out-String
    $docObj = Get-Help -Full $cmdlet.Name
    $summary = ($docObj.Synopsis | Out-String).Trim()
    $name = ($cmdlet.Name | Out-String).Trim()

    # Determine which product the cmdlet belongs to, "Google Compute Engine".
    $cloudProduct = "";
    foreach ($apiMapping in $apiMappings.GetEnumerator()) {
        if ($cmdletResource.StartsWith($apiMapping.Key)) {
            $cloudProduct = $apiMapping.Value
            break
        }
    }

    # Sanity check the data.
    $warning = "";
    if ($cloudProduct -eq "") {
        $warning = "Cmdlet $($cmdlet.Name) is not associated with a product."
    }
    if ($summary -eq "") {
        $warning = "Cmdlet $($cmdlet.Name) does not have a summary."
    }
    if ($warning -ne "") {
        Write-Warning $warning
    }

    # Generate JSON.
    $cmdletDocObj = @{
        "cmdletName" = $cmdlet.Name
        "verb" = $cmdletVerb
        "resource" = $cmdletResource

        "product" = $cloudProduct

        "summary" = $summary
        "documentation" = $docText
    }

    # Contains more information than above.
    $cmdletDocObjFull = @{
        "name" = $name
        "synopsis" = ($docObj.Synopsis | Out-String).Trim()
        "description" = ($docObj.Description | Out-String).Trim()
        "parameters" = ($docObj.Parameters | Out-String).replace("`r`n","`n")
        "inputs" = ($docObj.Inputs | Out-String).Trim()
        "outputs" = ($docObj.Outputs | Out-String).Trim()
        "examples" = ($docObj.Examples | Out-String).replace("`r`n","`n")
        }

    $cmdletDocObjects += $cmdletDocObj

    # Making the JSON a hashtable from cmdlet name to cmdlet information allows us to quickly access info in angular
    if ($cloudProduct -eq "Google Cloud Storage") {
        $cmdletDocObjectsGcs.Add($name,$cmdletDocObjFull)
    }
    else {
        $cmdletDocObjectsGce.Add($name,$cmdletDocObjFull)
    }
}

# We wrap the project specific cmdlets with their project.
$cmdletDocObjectsFull = @{
    "Google Compute Engine" = $cmdletDocObjectsGce
    "Google Cloud Storage" = $cmdletDocObjectsGcs
}

# Finally we write the json files.
Write-Host "Saving cmdlets.json"
$cmdletsOutputPath = Join-Path $PSScriptRoot "\..\website\_data\cmdlets.json"
$cmdletDocObjects `
| ConvertTo-Json -Depth 10 `
| Out-File -FilePath $cmdletsOutputPath -Encoding "UTF8"

Write-Host "Saving cmdletsFull.json"
$cmdletsOutputPath = Join-Path $PSScriptRoot "\..\Fix-Website\_data\cmdletsFull.json"
$cmdletDocObjectsFull `
| ConvertTo-Json -Depth 10 `
| Out-File -FilePath $cmdletsOutputPath -Encoding "UTF8"
