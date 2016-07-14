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
        [String[]]
        $CloudProducts,

        [Parameter(Mandatory=$true,
                   ValueFromPipeline=$true,
                   ValueFromPipelineByPropertyName=$true,
                   ParameterSetName = "CmdletNames",
                   HelpMessage='List the full names of the cmdlets that you want to check.')]
        [ValidateNotNullOrEmpty()]
        [Alias('Cmdlets')]
        [String[]]
        $CmdletNames,

        [Parameter(Mandatory=$false, 
                   HelpMessage='Include this optional switch to check example format, intro, and output.')]
        [Alias('DEC','ExampleCheck')]
        [Switch]
        $DeepExampleCheck
    )

    $binDirectory = Join-Path $PSScriptRoot "\..\Google.PowerShell\bin\"
    Write-Host($PSScriptRoot)
    Import-Module "$binDirectory\Debug\Google.PowerShell.dll"

    $cmdlets = Get-Command -Module "Google.PowerShell" | sort { $_ -replace ".*-",'' }

    # Get the cmdlets explicitly named if the CmdletNames parameter is specified.
    if ($CmdletNames.Count -gt 0) {
         $cmdlets = GetCmdletsByName $cmdletNames $cmdlets
    }

    # Create mapping between cmdlet name and the Cloud resource.
    $apiMappings = @{
        "Gcs" = "Google Cloud Storage"
        "Gce" = "Google Compute Engine"
        "Gcd" = "Google Cloud DNS"
        "GcSql" = "Google Cloud SQL"
    }

    # Check each cmdlet's documentation and output relevant warnings.
    foreach ($cmdlet in $cmdlets) {
        $productMapping = FindAssociatedCloudProduct $cmdlet.Name $apiMappings

        # If cloud products were specified and the cmdlet is not associated with any of them, abort the check.
        if ($CloudProducts.Count -gt 0) {
            if (-not (InSpecifiedCloudProducts $CloudProducts $productMapping $apiMappings)) {
                continue
            }
        }

        Write-Host "`nChecking $($cmdlet.Name)..."
        
        # Get the cmdlet's documentation.
        $docObj = Get-Help -Full $cmdlet.Name

        # Check the documentation for important information/categories and add relevant warnings.
        [System.Collections.Generic.List[System.Object]]$warnings = AllFieldWarnings $docObj $productMapping.Value

        # If there are examples, and the user has chosen a DeepExampleCheck, check if the examples include a command
        # starting with the usual PS C:\>.
        # If they do, check that they also have an intro and sample output for the command.
        if ($DeepExampleCheck) {
            $deepExampleWarnings = DoDeepExampleCheck $docObj
            
            if ($deepExampleWarnings.Count -gt 0) {
                if ($warnings.Count -gt 0) {
                    $warnings.Add($deepExampleWarnings) 
                }
                else {
                    [System.Collections.Generic.List[System.Object]]$warnings = $deepExampleWarnings
                }
                
            } 
              
        }

        if ($warnings.Count -gt 0) {
            $warnings | Write-Warning
        }
    }
}

# Create and return warnings for all important fields in one cmdlet's documentation.
function AllFieldWarnings ($docObj, $cloudProduct) {
    $fieldWarnings = New-Object System.Collections.Generic.List[System.Object]
    $cmdletName = ($docObj.Name | Out-String).Trim()

    # Creating mapping for field name and value in this cmdlet's documentation.
    $docFields = @{
        "CloudProduct" = $cloudProduct
        "Name" = $cmdletName
        "Synopsis" = ($docObj.Synopsis | Out-String).Trim()
        "Description" = ($docObj.Description | Out-String).Trim()
        "OutputType" = ($docObj.returnValues | Out-String).Trim()
        "Examples" = ($docObj.examples | Out-String).Trim()
    }

    # Add warnings for each empty field.
    foreach ($docField in $docFields.GetEnumerator()) {
        if ($docField.Value -eq "") {
            $warning = CreateMissingFieldWarning $docField.Key
            $fieldWarnings.Add($warning)
        }
    }

    return $fieldWarnings
}

# Given a field name, create and return a warning specifically for the missing field.
function CreateMissingFieldWarning($fieldName) {
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

# Given a cmdlet name and mappings from api name and cloud products, find the cmdlet's associated cloud product.
function FindAssociatedCloudProduct($cmdletName, $apiMappings) {
    $cmdletResource = $cmdletName.Split("-")[1]

    foreach ($apiMapping in $apiMappings.GetEnumerator()) {
        if ($cmdletResource.StartsWith($apiMapping.Key)) {
            return $apiMapping
        }
    }

    return ""
}

# Check if the cmdlet product is one of the specified products.
function InSpecifiedCloudProducts($specifiedProducts, $productMapping, $apiMappings) {
    if (-not (($specifiedProducts -contains $productMapping.Key) -or 
               ($specifiedProducts -contains $productMapping.Value))) {
        return $false
    }

    return $true
}

# Given a cmdlet's documention, conduct a deep example check and return relevant warnings for its examples.
function DoDeepExampleCheck($docObj) { 
    $deepExampleWarnings = New-Object System.Collections.Generic.List[System.Object]

    # Only do deep check if the documentation has at least 1 example.
    if (($docObj.examples | Out-String).Trim() -ne "") { 
        $noPSStart = New-Object System.Collections.Generic.List[System.Object]
        $noIntro = New-Object System.Collections.Generic.List[System.Object]
        $noOutput = New-Object System.Collections.Generic.List[System.Object]
        $currentExample = 1; 

        foreach ($example in $docObj.examples.example) {
            $exampleString = ($example  | Out-String).Trim()
                
            # Check if example has command starting with PS C:\>.
            if (-not ($exampleString -match [regex]::Escape('PS C:\>'))) {
                $noPSStart.Add($currentExample)
            }
            # If yes, check if the command has an intro and example output.
            else
            {
                $lineSplitExample = $exampleString.Split("`n")
                $PSline = ($lineSplitExample | select-string "PS C:\\>" | select LineNumber).LineNumber

                if ($PSline -le 3) {
                    $noIntro.Add($currentExample)
                }

                if (($lineSplitExample.Count - $PSline) -le 0) {
                    $noOutput.Add($currentExample)
                }
            }

            $currentExample++
        }

        if ($noPSStart.Count -gt 0) {
            $deepExampleWarnings.Add("Example number(s) " + ($noPSStart -join ",") + 
                                     " does(do) not have commands starting with the expected PS C:\>. " + 
                                     "(Thus, cannot check for command intro or example output.)")
        }

        if ($noIntro.Count -gt 0) {
            $deepExampleWarnings.Add("Example number(s) " + ($noIntro -join ",") + " has(have) no introduction.")
        }

        if ($noOutput.Count -gt 0) {
            $deepExampleWarnings.Add("Example number(s) " + ($noOutput -join ",") + " has(have) no outputs.")
        }
        
        return $deepExampleWarnings
    }
}

# Get the cmdlets explicitly named as a subset of all Google Cloud cmdlets.
function GetCmdletsByName ($cmdletNames, $allCmdlets) {
    $cmdlets = $allCmdlets | where { $CmdletNames -contains $_.Name }
    $cmdletsNotFound = $CmdletNames | where { -not ($allCmdlets.Name -contains $_) }

    if ($cmdletsNotFound.Count -gt 0) {
    Write-Host ("`nThe following cmdlets you named were not found: ")
    $cmdletsNotFound -join "`n" | Write-Host
    }

    return $cmdlets
}