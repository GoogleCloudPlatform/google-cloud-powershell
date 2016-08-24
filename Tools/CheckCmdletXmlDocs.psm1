function Check-CmdletDoc() {
    <#
    .SYNOPSIS
    Write warnings relating to cmdlet XML dcoumentation.

    .DESCRIPTION
    Get the XML documentation for Google Cloud Powershell cmdlets and write relevant warnings. 

    .EXAMPLE
    PS C:\> Check-CmdletDoc

    Checking Add-GcdChange...

    Checking Get-GcdChange...

    (contd...)

    Checking Get-GcSqlTiers...
    WARNING: Ddoes not have an output type.
    WARNING: Does not have any examples.

    .EXAMPLE
    PS C:\> Check-CmdletDoc -CloudProducts 'Google Cloud DNS'

    Checking Add-GcdChange...

    Checking Get-GcdChange...

    Checking Add-GcdManagedZone...

    Checking Remove-GcdManagedZone...
    WARNING: Does not have an output type.

    Checking Get-GcdManagedZone...

    Checking Get-GcdQuota...

    Checking New-GcdResourceRecordSet...

    Checking Get-GcdResourceRecordSet...

    .EXAMPLE
    PS C:\> Check-CmdletDoc -CmdletNames "Get-GcdManagedZone","Get-GcsObject"

    Checking Get-GcdManagedZone...

    Checking Get-GcsObject...
    WARNING: Does not have an output type.
    WARNING: Does not have any examples.

    .EXAMPLE
    PS C:\> Check-CmdletDoc -CmdletNames "Get-GcsBucket" -DeepExampleCheck

    Checking Get-GcsBucket...
    WARNING: Does not have an output type.
    WARNING: Example number(s) 1,2 does(do) not have commands starting with the expected PS C:\>. (Thus, cannot check for command intro or example output.)

    .PARAMETER CloudProducts
    Alias: Products
    List the Cloud Product(s) (by full name or cmdlet abbreviation) whose cmdlet docs you want to check.

    .PARAMETER CmdletNames
    Alias: Cmdlets
    List the Cmdlet(s) (by full name) whose docs you want to check.

    .PARAMETER DeepExampleCheck
    Aliases: DEC, ExampleCheck
    Include this optional switch to check example format, intro, and output.
    #>   

    [CmdletBinding(DefaultParameterSetName="AllCmdlets")]
    param
    (
        [Parameter(Mandatory=$true,
                   ParameterSetName = "ProductNames",
                   HelpMessage='List the Cloud Product(s) (by full name or cmdlet abbreviation)' +
                               'whose cmdlet docs you want to check.')]
        [ValidateSet('Gcs','Gce','Gcd','GcSql',
                     'Google Cloud Storage','Google Compute Engine','Google Cloud DNS','Google Cloud SQL')]
        [Alias('Products')]
        [String[]] $CloudProducts,

        [Parameter(Mandatory=$true,
                   ValueFromPipeline=$true,
                   ValueFromPipelineByPropertyName=$true,
                   ParameterSetName = "CmdletNames",
                   HelpMessage='List the full names of the cmdlets that you want to check.')]
        [ValidateNotNullOrEmpty()]
        [Alias('Cmdlets')]
        [String[]] $CmdletNames,

        [Parameter(Mandatory=$false, 
                   HelpMessage='Include this optional switch to check example format, intro, and output.')]
        [Alias('DEC','ExampleCheck')]
        [Switch] $DeepExampleCheck 
    )

    # Retrieve all GCloud cmdlets. 
    $binDirectory = Join-Path $PSScriptRoot "\..\Google.PowerShell\bin\"
    Import-Module "$binDirectory\Debug\Google.PowerShell.dll"    
    $allCmdlets = $cmdlets = Get-Command -Module "Google.PowerShell" | Sort Noun

    # Create mapping between cmdlet name and the Cloud resource.
    $apiMappings = @{
        "Gcs" = "Google Cloud Storage"
        "Gce" = "Google Compute Engine"
        "Gcd" = "Google Cloud DNS"
        "GcSql" = "Google Cloud SQL"
    }

    # If the CmdletNames parameter is specified, get the cmdlets explicitly named.
    if ($CmdletNames) {
         $cmdlets = GetCmdletsByName $cmdletNames $cmdlets
    }

    # If the CloudProducts parameter is specified, get the cmdlets in the named products. 
    if ($CloudProducts) {
        $cmdlets = GetCmdletsByProduct $CloudProducts $apiMappings $cmdlets
    }

    # Cmdlets can be whitelisted for valid cases. For example, if they intentionally don't
    # produce any output we should not warn because they do not have an OutputType specified.
    $outputWhitelistDirectory = "$PSScriptRoot\OutputTypeWhitelist.txt"
    $outputWhitelist = GetOutputTypeWhitelist $outputWhitelistDirectory $allCmdlets

    $oldProductMapping = $null 
    # Check each cmdlet's documentation and output relevant warnings.
    foreach ($cmdlet in $cmdlets) {
        $productMapping = FindAssociatedCloudProduct $cmdlet.Noun $apiMappings

        # Write footer (number of passing cmdlets) for previous product and header for new product. 
        if ($productMapping -ne $oldProductMapping) {
            if ($oldProductMapping -ne $null) {
                Write-Host "`nNumber of passing cmdlets: $numPassedCmdlets`n" -ForegroundColor Green -BackgroundColor Black
            }
            Write-Host "`n---------- Checking `"$($productMapping.Value)`" cmdlets ----------"
            $numPassedCmdlets = 0
            $oldProductMapping = $productMapping
        }
        
        # Get the cmdlet's documentation.
        $docObj = Get-Help -Full $cmdlet.Name

        # Check the documentation for important information/categories and write relevant warnings.
        $warnings = @()
        $warnings += (GetAllFieldWarnings $docObj $productMapping.Value $outputWhitelist)

        # If there are examples, and the user has chosen a DeepExampleCheck, check if the examples include a command
        # starting with the usual PS C:\>. 
        # If they do, check that they also have an intro and sample output for the command.
        if ($DeepExampleCheck) {
            $warnings += (DoDeepExampleCheck $docObj)
        }

        # Check that all parameters have a valid (non-null, non-whitespace) description. 
        $warnings += (GetParameterDescriptionWarnings $docObj)

        if ($warnings.Count -gt 0) {
            Write-Host "`n$($cmdlet.Name)" -ForegroundColor Red -BackgroundColor Black
            $warnings | Write-Warning
        } else {
            $numPassedCmdlets++
        }

        # If this is the last cmdlet, write the footer for this last product. 
        if ($cmdlet -eq $cmdlets[-1]) {
            Write-Host "`nNumber of passing cmdlets: $numPassedCmdlets`n" -ForegroundColor Green -BackgroundColor Black
        }
    }
}

# Get the cmdlets explicitly named as a subset of all Google Cloud cmdlets.
function GetCmdletsByName ($cmdletNames, $allCmdlets) {
    PrintElementsNotFound $cmdletNames $allCmdlets.Name "`nThe following cmdlets you named were not found:"
    return @($allCmdlets | where Name -in $cmdletNames)
}

# Get the cmdlets explicitly named as a subset of all Google Cloud cmdlets.
function GetCmdletsByProduct ($CloudProducts, $apiMappings, $cmdlets) {
    return @($cmdlets | where { InSpecifiedCloudProducts $CloudProducts (FindAssociatedCloudProduct $_.Noun $apiMappings) })
}

# Check if the cmdlet product is one of the specified products.
function InSpecifiedCloudProducts($specifiedProducts, $productMapping) {
    return (($specifiedProducts -contains $productMapping.Key) -or 
            ($specifiedProducts -contains $productMapping.Value))
}

# Given a cmdlet name and mappings from api name and cloud products, find the cmdlet's associated cloud product.
function FindAssociatedCloudProduct($cmdletNoun, $apiMappings) {
    foreach ($apiMapping in $apiMappings.GetEnumerator()) {
        if ($cmdletNoun.StartsWith($apiMapping.Key)) {
            return $apiMapping
        }
    }

    return ""
}

# Get the names of the cmdlets in the OuputType whitelist.
function GetOutputTypeWhitelist ($outputWhitelistDirectory, $allCmdlets) {
    $outputWhitelist = (Get-Content $outputWhitelistDirectory)
    if (-not $outputWhitelist) {
        return $null
    } 
    $outputWhitelist = $outputWhitelist.Split(" *`n+", [System.StringSplitOptions]::RemoveEmptyEntries)
    PrintElementsNotFound $outputWhitelist $allCmdlets.Name "`nThe following cmdlets from the OutputType whitelist were not found:"
    return @($allCmdlets.Name | where { $outputWhitelist -contains $_ })
}

# Print a list of the elements in sublist that are not part of list. 
function PrintElementsNotFound ($sublist, $list, $message) {
    $notFound = $sublist | where { -not ($list -contains $_) } 

    if ($notFound) {
        Write-Host $message
        $notFound | Write-Host
    }
}

# Get warnings for all important fields in a cmdlet's documentation.
function GetAllFieldWarnings ($docObj, $cloudProduct, $outputWhitelist) {
    # Creating mapping for field name and value in this cmdlet's documentation.
    $docFields = @{
        "CloudProduct" = $cloudProduct
        "Name" = ($docObj.Name | Out-String).Trim()
        "Synopsis" = ($docObj.Synopsis | Out-String).Trim()
        "Description" = ($docObj.Description | Out-String).Trim()
        "OutputType" = ($docObj.returnValues | Out-String).Trim()
        "Examples" = ($docObj.examples | Out-String).Trim()
    }

    # Add warnings for each empty field.
    foreach ($docField in $docFields.GetEnumerator()) {
        if (($docField.Value -eq "") -and 
            (-not (($docField.Key -eq "OutputType") -and ($outputWhitelist -contains $docFields.Get_Item("Name"))))) {
            Write-Output (GetMissingFieldWarning $docField.Key)
        }
    }
}

# Given a field name, create and return a warning specifically for the missing field.
function GetMissingFieldWarning($fieldName) {
    $warningText = "Does not have "; 

    switch ($fieldName) {
        "CloudProduct" { $warningText += "an associated cloud product." }
        "Name" { $warningText += "a name in its documentation." }
        "Synopsis" { $warningText += "a synopsis." }
        "Description" { $warningText += "a description." }
        "OutputType" { $warningText += "an output type." }
        "Examples" { $warningText += "any examples." }
    }

    return $warningText
}

# Given a cmdlet's documention, conduct a deep example check and return relevant warnings for its examples.
function DoDeepExampleCheck($docObj) { 
    # Only do deep check if the documentation has at least 1 example.
    if (($docObj.examples | Out-String).Trim() -ne "") { 
        $noPSStart = @()
        $noIntro = @()
        $noOutput = @()
        $currentExample = 1; 

        foreach ($example in $docObj.examples.example) {
            $exampleString = ($example | Out-String).Trim()
                
            # Check if example has command starting with PS C:\>.
            if (-not ($exampleString -match [regex]::Escape('PS C:\>'))) {
                $noPSStart += $currentExample
            } else {
                # If yes, check if the command has an intro and example output.
                $lineSplitExample = $exampleString.Split("`n")
                $PSlines = ($lineSplitExample | Select-string "PS C:\\>" | Select LineNumber).LineNumber
                $firstPSline = $PSlines[0]
                $lastPSline = $PSlines[-1]

                if ($firstPSline -le 3) {
                    $noIntro += $currentExample
                }

                if (($lineSplitExample.Count - $lastPSline) -le 0) {
                    $noOutput += $currentExample
                }
            }

            $currentExample++
        }

        if ($noPSStart.Count -gt 0) {
            Write-Output ("Example number(s) " + ($noPSStart -join ", ") + " does(do) not have commands " +
                "starting with the expected PS C:\>. (Thus, cannot check for command intro or example output.)")
        }

        if ($noIntro.Count -gt 0) {
            Write-Output ("Example number(s) " + ($noIntro -join ", ") + " has(have) no introduction.")
        }

        if ($noOutput.Count -gt 0) {
            Write-Output ("Example number(s) " + ($noOutput -join ", ") + " has(have) no outputs.")
        }
    }
}

# Check that all cmdlet parameters have a valid (non-null, non-whitespace) description, returning warnings otherwise.
function GetParameterDescriptionWarnings ($docObj) {
    foreach ($parameter in $docObj.parameters.parameter) {
        if ([String]::IsNullOrWhiteSpace($parameter.description.Text)) {
            Write-Output ("Parameter `"" + $parameter.name + "`" does not have a valid description.")
        }
    }
}
