# Generates a JSON file containing all the documentation information for all cmdlets in the
# GoogleCloud PowerShell module. The resulting data is used to power the cmdlet reference
# website.
[CmdletBinding()]
Param(
    [Parameter()]
    [ValidateSet("Debug", "Release")]
    [string]$configuration = "Release"
)

# Load the list of Beta cmdlets. We'll use this list to annotate the generated website data.
Write-Host "Loading Beta cmdlets."
$cmdletsToBeExported = & (Join-Path $PSScriptRoot "BetaCmdlets.ps1")
if (($cmdletsToBeExported -eq $null) -or ($cmdletsToBeExported.Length -eq 0)) {
    Write-Warning "Unable to locate Beta cmdlets. File got renamed?"
    return
}
$debugCmdletsList = $cmdletsToBeExported  # Rename variable for clarity.

$script:defaultParameterSetName = "Default"

# Unload the module if already loaded. (Weird things happen when debugging...)
if (Get-Module Google.PowerShell) {
    Write-Warning "Previous version of Google.PowerShell loaded. Unloadig the module."
    Remove-Module Google.PowerShell
}

$modulePath = Join-Path $PSScriptRoot "\..\Google.PowerShell\bin\$configuration\Google.PowerShell.dll"

if (-Not (Test-Path $modulePath)) {
    Write-Warning "Unable to locate PowerShell module '$modulePath'. Not built?"
    return
}

Import-Module $modulePath
$cmdlets = Get-Command -Module "Google.PowerShell"

# Collapses an array of objects with a Text property into an array of strings.
# We return $null rather than @() to work around a quirk of PowerShell JSON conversion.
function Collapse-TextArray($textObjs) {
    $lines = @()
    ForEach ($textObj in $textObjs) {
        $lines += $textObj.Text.TrimEnd("`r"[0], "`n"[0])
    }

    if ($lines.Length -gt 0) {
        return $lines
    }
    return $null
}

# Get-Examples converts the PowerShell examples object into a more JSON-friendly type.
# Specifically, it collapses arrays of custom objects with a Text property, to just
# arrays of strings.
function Get-Examples($rawExamples) {
    $examples = @()
    ForEach ($rawExample in $rawExamples) {
        $example = @{
            "introduction" = Collapse-TextArray($rawExample.introduction)
            # The second <code> block in an example is ignored by XmlDoc2CmdletDoc. But
            # <para> blocks are added, separated by \n characters.
            "code" = $rawExample.Code
            "remarks" = Collapse-TextArray($rawExample.remarks)
        }
        $examples += $example
    }

    if ($examples.Length -gt 0) {
        return $examples
    }
    return $null
}

# Get-Links creates an array of link objects for the given cmdlet.
# $docObj is of type PSCustomObject, obtained via Get-Help.
function Get-Links ($docObj) {
    $links = @()
    $relatedLinks = $docObj.relatedLinks
    ForEach ($link in $relatedLinks.navigationLink) {
        $uri = $link.uri.TrimStart('(').TrimEnd(')')
        $linkObj = @{ "text" = $link.linkText; "uri" = $uri }
        $links += $linkObj
    }

    if ($links.Length -gt 0) {
        return $links
    }
    return $null
}

# Generate a PSObject that will be used to populate the cmdlet JSON file based on $parameter.
function New-ParamObject($parameter) {
    $position = "named"

    if ($parameter.Position -ge 0) {
        $position = $parameter.Position
    }

    if ($parameter.ValueFromPipeline -and $parameter.ValueFromPipelineByPropertyName) {
        $pipelineInput = "true (ByValue, ByPropertyName)"
    }
    elseif ($parameter.ValueFromPipeline) {
        $pipelineInput = "true (ByValue)"
    }
    elseif ($parameter.ValueFromPipelineByPropertyName) {
        $pipelineInput = "true (ByPropertyName)"
    }
    else {
        $pipelineInput = "false"
    }

    $parameterTypeName = $parameter.ParameterType.FullName
    # Nullable type has a really long fullname (with public key token and assembly name)
    # so we have to get the underlying type.
    if ($parameter.ParameterType.Name -eq "Nullable``1") {
        $firstGenericArg = $parameter.ParameterType.GetGenericArguments()[0]
        $parameterTypeName = $firstGenericArg.FullName
    }

    $parameterType = New-Object PSObject -Property @{"name" = $parameterTypeName; "uri" = ""}
    $parameterValue = New-Object PSObject -Property @{"value" = $parameter.ParameterType.Name}
    $parameterDescription = New-Object PSObject -Property @{"Text" = $parameter.HelpMessage}

    $parameterSyntaxObject = New-Object PSObject -Property @{"name" = $parameter.Name
                                                                    "required" = "$($parameter.IsMandatory)".ToLower();
                                                                    "position" = $position;
                                                                    "pipelineInput" = $pipelineInput;
                                                                    "globbing" = "false";
                                                                    "type" = $parameterType;
                                                                    "parameterValue" = $parameterValue;
                                                                    "description" = $parameterDescription}

    # If there is a validate set, add the set to parameterValue.
    $validateSetAttribute = $parameter.Attributes | Where-Object {$_.TypeId.Name -eq "ValidateSetAttribute"}
    if ($null -ne $validateSetAttribute -and $null -ne $validateSetAttribute.ValidValues) {
        $validateSetJoined = $validateSetAttribute.ValidValues -join " "
        $parameterValueGroup = @{"parameterValue" = $validateSetJoined}
        $parameterSyntaxObject | Add-Member "parameterValueGroup" $parameterValueGroup
    }

    # If there is an alias, add that to the object too.
    $aliasAttribute = $parameter.Attributes | Where-Object {$_.TypeId.Name -eq "AliasAttribute"}
    if ($null -ne $aliasAttribute -and $aliasAttribute.AliasNames.Count -gt 0) {
        $parameterSyntaxObject | Add-Member "aliases" $aliasAttribute.AliasNames
    }

    return $parameterSyntaxObject
}

# The PowerShell objects returned from Get-Help are missing a few, key pieces of data. This method
# updates the Syntax and Parameter objects to cross reference parameters, parameter sets, and
# cmdlet invocation syntax.
#
# - Each Parameter ($docObj.parameters.parameter) will give given a new field "parameterSets".
#
# We update the objects in-memory, and rely on their persistance until we convert them to JSON.
function Annotate-ParametersAndSyntaxObjects($cmdletInfo, $docObj) {
    $parameterSets = $cmdletInfo.ParameterSets

    # Go through each parameter set and annotate the parameters that belong to it.
    ForEach ($parameterSet in $parameterSets) {
        # Rewrite "__AllParameterSets" which is the name apparently given as default.
        # Since Name is read-only, we refer to the parameter set name via HackName.
        $parameterSet | Add-Member "HackName" $parameterSet.Name
        if ($parameterSet.HackName -eq "__AllParameterSets") {
            $parameterSetWithAllParametersName = $parameterSets | Where Name -eq "__AllParameterSets"
            if ($parameterSetWithAllParametersName.Count -ne 1) {
                Write-Warning "Assumed only one parameter set named __AllParameterSets"
                exit
            }
            $parameterSet.HackName = $script:defaultParameterSetName
        }

        ForEach ($parameter in $parameterSet.Parameters) {
            # Sneaky hack. We switch from the parameterSet's parameter object to a similar (but
            # different reference) in the $docObject's parameters list.
            $docParam = $docObj.parameters.parameter | Where Name -eq $parameter.Name
            if ($docParam -eq $null) {
                # Parameters found in the parameter set but not in the actual cmdlet are common
                # parameters, like Verbose, ErrorAction, etc. Ignore.
                continue
            }
            if (($docParam | Get-Member "parameterSet") -eq $null) {
                $docParam | Add-Member "parameterSet" @()
            }
            $docParam.parameterSet += $parameterSet.HackName
        }
    }
}

# Add dynamic parameter to $docObj.parameters
function Add-DynamicParameterToDocObj($cmdletName, $docObj) {
    $dynamicParameters = @()
    $parameterSets = (Get-Command $cmdletName).ParameterSets
    ForEach($parameterSet in $parameterSets) {
        ForEach ($paramSetParam in $parameterSet.Parameters) {
            if ($paramSetParam.IsDynamic) {
                $dynamicParameterSyntaxObject = New-ParamObject $paramSetParam

                if ($parameterSet.ParameterSetName -eq "__AllParameterSets") {
                    $dynamicParameterSyntaxObject | Add-Member "parameterSet" $script:defaultParameterSetName
                }
                else {
                    $dynamicParameterSyntaxObject | Add-Member "parameterSet" $parameterSet.Name
                }

                $dynamicParameters += $dynamicParameterSyntaxObject
            }
        }
    }

    if ($dynamicParameters.Count -gt 0) {
        Collapse-ParameterDescriptions($dynamicParameters)
        $docObj.parameters = $dynamicParameters + $docObj.parameters
    }
}

# Generate an object similar to what (Get-Help -Full $cmdletName).Syntax.SyntaxItem
# looks like. We have to do this because for some reason (most likely a bug?), the
# Get-Help cmdlet always generate incorrect value when a cmdlet has a dynamic parameter.
function New-SyntaxObjectArray($cmdletInfo) {
    $syntaxObjectArray = @()
    ForEach ($parameterSet in $cmdletInfo.ParameterSets) {
        $syntaxObjParams = @()
        ForEach($parameter in $parameterSet.Parameters) {
            if ([System.Management.Automation.PSCmdlet]::CommonParameters.Contains($parameter.Name) -or
                [System.Management.Automation.PSCmdlet]::OptionalCommonParameters.Contains($parameter.Name)) {
                continue
            }

            $syntaxObjParams += (New-ParamObject $parameter)
        }

        $parameterSetName = $parameterSet.Name;
        if ($parameterSetName -eq "__AllParameterSets") {
            $parameterSetName = $script:defaultParameterSetName
        }

        $syntaxObject = New-Object PSObject -Property @{
            "parameterSet" = $parameterSetName;
            "isDefault" = $parameterSet.IsDefault;
            "parameter" = $syntaxObjParams;
            "name" = $cmdletInfo.Name
        }
        $syntaxObjectArray += $syntaxObject
    }

    return $syntaxObjectArray
}

# Each parameter description is either a single string, or an array of PSAutomationObjects with a
# single Text parameter. Collapse these to just be an array of strings. This makes processing the
# JSON easier on the frontend.
function Collapse-ParameterDescriptions($parameters) {
    ForEach ($parameter in $parameters) {
        if (($parameter | Get-Member "description") -eq $null) {
            Write-Warning "Parameter $($parameter.Name) has no description."
        } else {
            $result = $parameter.description | Select -ExpandProperty Text
            $parameter.description = $result
        }
    }
}

# Generate a ProductInfo object. The $isBeta flag should only be used if every cmdlet in the
# GCP product is in Beta. Otherwise, we'll just mark individual cmdlets as beta or not.
# TODO(chrsmith): We can do this analysis after the fact (using the $isBeta field on cmdlets)
# so we can obviate hard-coding this here.
function GenerateProductInfo($longName, $shortName, $isBeta = $false) {
    return @{
        name = $longName;
        shortName = $shortName;
        isBeta = $isBeta;
        resources = @()
    }
}

# Mapping of cmdlet prefixes to Google Cloud Platform productInfo objects.
$productInfoLookup = @{
    "Gcs"   = GenerateProductInfo      "Google Cloud Storage"    "google-cloud-storage"
    "Gce"   = GenerateProductInfo      "Google Compute Engine"   "google-compute-engine"
    "GcSql" = GenerateProductInfo      "Google Cloud SQL"        "google-cloud-sql"
    "Gcd"   = GenerateProductInfo      "Google Cloud DNS"        "google-cloud-dns"
    "Gcps"  = GenerateProductInfo      "Google Cloud PubSub"     "google-cloud-pubsub"
    "GcLog" = GenerateProductInfo      "Google Cloud Logging"    "google-cloud-logging"
    "GcIam" = GenerateProductInfo      "Google Cloud IAM"        "google-cloud-iam"
    "GcpProject" = GenerateProductInfo "Google Cloud Project"    "google-cloud-project"
    "Gke"   = GenerateProductInfo      "Google Container Engine" "google-cloud-container"    $true
    "Bq"    = GenerateProductInfo      "Google Cloud BigQuery"   "google-cloud-bigquery"     $true
}

# Generate a giant JSON file containing all of our cmdlet's documentation. We later write this as
# JSON and serve it from the web frontend. The object will be arrays of objects of the form:
# - Product[] > Resource[] > Cmdlet[]
$allDocumentationObj = @{
    "products" = @()
}

ForEach ($cmdlet in $cmdlets) {
    Write-Host "Building $($cmdlet.Name)..."

    $cmdletVerb = $cmdlet.Name.Split("-")[0]
    $cmdletResource = $cmdlet.Name.Split("-")[1]

    $helpObj = Get-Help -Full $cmdlet.Name
    $cmdletInfo = Get-Command $cmdlet.Name

    # Generate the object to be written out.
    $docObj = @{
        "name"        = $cmdlet.Name
        "isBeta"      = $debugCmdletsList.Contains($cmdlet.Name)
        "synopsis"    = $helpObj.Synopsis
        "description" = Collapse-TextArray($helpObj.Description)
        "syntax"      = New-SyntaxObjectArray($cmdletInfo)
        "parameters"  = $helpObj.parameters.parameter
        "inputs"      = $helpObj.inputTypes
        "outputs"     = $helpObj.returnValues
        "examples"    = Get-Examples($helpObj.examples.example)
        "links"       = Get-Links($helpObj)
    }

    Add-DynamicParameterToDocObj $cmdlet.Name $docObj
    Annotate-ParametersAndSyntaxObjects $cmdletInfo $helpObj
    Collapse-ParameterDescriptions $helpObj.parameters.parameter

    # Determine which product the cmdlet belongs to
    $productObj = $null
    foreach ($prefixKvp in $productInfoLookup.GetEnumerator()) {
        if ($cmdletResource.StartsWith($prefixKvp.Key)) {
            $productObj = $prefixKvp.Value
            break
        }
    }
    if ($productObj -eq $null) {
        Write-Warning "No product found for cmdlet $($cmdlet.Name). Please update productInfLookup."
        exit
    }

    # Init product or resource arrays if needed and add cmdletDocObjFull.
    if (-Not ($allDocumentationObj.products.Contains($productObj))) {
        $allDocumentationObj.products += $productObj
    }

    $resourceObj = $productObj.resources | Where Name -eq $cmdletResource
    if ($resourceObj -eq $null) {
        $resourceObj = @{
            "name" = $cmdletResource
            "cmdlets" = @()
        }
        $productObj.resources += $resourceObj
    }

    $resourceObj.cmdlets += $docObj
}

$cmdletDocOutputPath = Join-Path $PSScriptRoot "\..\website\data\cmdletsFull.json"
# If the folder "website\data" doesn't exist, then you probably haven't initialized the
# git submodule which points to the gh-pages branch.
Write-Host "Writing cmdlet documentation to '$cmdletDocOutputPath'."
$allDocumentationObj |
    ConvertTo-Json -Depth 15 -Compress |
    Out-File -FilePath $cmdletDocOutputPath -Encoding "UTF8"

Write-Host "`tWrote $([Math]::Round((Get-Item $cmdletDocOutputPath).Length / 1MB, 2))MiB"
Write-Host "Done"
