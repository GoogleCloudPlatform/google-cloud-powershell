# Generates a JSON file containing the documentation information for all
# PowerShell cmdlets. This can be passed to the documentation website and
# -- by the magic of Angular.js -- be used to generate cmdlet-specific pages.

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
# the way we are generating web pages in Angular.

$cmdletDocObjectsGcs = @{}
$cmdletDocObjectsGce = @{}
foreach ($cmdlet in $cmdlets) {
    Write-Host "Building $($cmdlet.Name)..."

    $cmdletVerb = $cmdlet.Name.Split("-")[0]
    $cmdletResource = $cmdlet.Name.Split("-")[1]

    # Get documentation on the cmdlet.
    $docObj = Get-Help -Full $cmdlet.Name 
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
        "name" = $name
        "synopsis" = ($docObj.Synopsis | Out-String).Trim()
        "description" = ($docObj.Description | Out-String).Trim()
        "parameters" = ($docObj.Parameters | Out-String).replace("`r`n","`n")
        "inputs" = ($docObj.Inputs | Out-String).Trim()
        "outputs" = ($docObj.Outputs | Out-String).Trim()
        "examples" = ($docObj.Examples | Out-String).replace("`r`n","`n")
        }
    
    # Making the JSON be a hashtable from cmdlet name to cmdlet information allows us to quickly access info in info.ng
    if ($cloudProduct -eq "Google Cloud Storage") {
        $cmdletDocObjectsGcs.Add($name,$cmdletDocObj)
    }
    else {
        $cmdletDocObjectsGce.Add($name,$cmdletDocObj)
    }

}
$cmdletDocObjects = @{
    "Google Compute Engine" = $cmdletDocObjectsGce
    "Google Cloud Storage" = $cmdletDocObjectsGcs
}

Write-Host "Saving cmdlets.json"
$cmdletsOutputPath = Join-Path $PSScriptRoot "\..\Fix-Website\cmdletsfull.json"
$cmdletDocObjects `
| ConvertTo-Json -Depth 10 `
| Out-File -FilePath $cmdletsOutputPath -Encoding "UTF8"
