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
    "GcSql" = "Google Cloud SQL"
    "Gcd" = "Google Cloud DNS"
}

function convertToString ($obj)
{
    ($obj | Out-String).replace("`r`n","`n")
}

# Get ParameterSets creates the parameter set hashtable for each cmdlet.
# It takes in the System.Management.Automation.PSCustomObject object
# for the cmdlet, which is acquired with Get-Help.
function getParameterSets ($docObj) {
    $parameterSets = (Get-command $docObj.name).ParameterSets
    # First, the cmdlet's parameters are accrued.
    $parameterTable = @{}
    ForEach ($par in $docObj.parameters.parameter) {
        $parInfo = @{
            "name" = $par.Name
            "description" = $par.description.text
            "mandatory" = $par.required
        }
        $parameterTable.Add($par.Name,$parInfo)
    }

    # then they are organized by parameter set.
    $sets = @{}
    ForEach ($set in $parameterSets) {
        $setInfo = @{}
        ForEach ($par in ($set | select -ExpandProperty parameters)) {
            if ($parameterTable.ContainsKey($par.Name)) {
                $setInfo.Add($par.name,$parameterTable.($par.name))
            }
        }
        $sets.Add($set.name,$setInfo)
    }
    return $sets
}

# getLinks creates the related link hashtable for each cmdlet.
# It takes in the System.Management.Automation.PSCustomObject object
# for the cmdlet, which is acquired with Get-Help.
function getLinks ($docObj) {
    $relatedLinks = $docObj.relatedLinks
    $links = @{}
    ForEach ($link in $relatedLinks.navigationLink) {
        $uri = $link.uri.TrimStart('(').TrimEnd(')')
        $links.Add($link.linkText,$uri)
    }
    return $links
}

# Generate a single JSON file containing all the documentation for all the
# cmdlets. Unfortunately we can't split these into multiple files because of
# the way we generating web pages in Jekyll/angular.
$cmdletDocObjects = @()

#Allows us to separate out cmdlets by product.
$cmdletDocObjectsGcs = @{}
$cmdletDocObjectsGce = @{}
$cmdletDocObjectsGcSql = @{}
$cmdletDocObjectsGcd = @{}
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
        "syntax" = convertToString($docObj.Syntax)
        "name" = $name
        "synopsis" = convertToString($docObj.Synopsis)
        
        "description" = ($docObj.Description | Out-String).Trim()
        "parameters" = getParameterSets($docObj)
        "inputs" = convertToString($docObj.inputTypes)
        "outputs" = convertToString($docObj.returnValues)
        "examples" = convertToString($docObj.Examples)
        "links" = getLinks($docObj)
    }

    $cmdletDocObjects += $cmdletDocObj

    # Making the JSON a hashtable from cmdlet name to cmdlet information allows us to quickly access info in angular
    if ($cloudProduct -eq "Google Cloud Storage") {
        $cmdletDocObjectsGcs.Add($name,$cmdletDocObjFull)
    }
    if ($cloudProduct -eq "Google Compute Engine") {
        $cmdletDocObjectsGce.Add($name,$cmdletDocObjFull)
    }
    if ($cloudProduct -eq "Google Cloud SQL") {
        $cmdletDocObjectsGcSql.Add($name,$cmdletDocObjFull)
    }
    if ($cloudProduct -eq "Google Cloud DNS") {
        $cmdletDocObjectsGcd.Add($name,$cmdletDocObjFull)
    }
}

# We wrap the project specific cmdlets with their project.
$cmdletDocObjectsFull = @{
    "Google Compute Engine" = $cmdletDocObjectsGce
    "Google Cloud Storage" = $cmdletDocObjectsGcs
    "Google Cloud SQL" = $cmdletDocObjectsGcSql
    "Google Cloud DNS" = $cmdletDocObjectsGcd
}

# Finally we write the json files.
Write-Host "Saving cmdlets.json"
$cmdletsOutputPath = Join-Path $PSScriptRoot "\..\website\_data\cmdlets.json"
$cmdletDocObjects `
| ConvertTo-Json -Depth 10 `
| Out-File -FilePath $cmdletsOutputPath -Encoding "UTF8"

Write-Host "Saving cmdletsFull.json"
$cmdletsOutputPath = Join-Path $PSScriptRoot `
    "\..\website_v2\gcloud-powershell-website-v2\www\static\_data\cmdletsFull.json"
$cmdletDocObjectsFull `
| ConvertTo-Json -Depth 10 `
| Out-File -FilePath $cmdletsOutputPath -Encoding "UTF8"
