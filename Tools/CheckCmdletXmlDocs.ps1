function Check-CmdletDoc() {
    <#
    .SYNOPSIS
    Write warnings relating to cmdlet XML dcoumentation.

    .DESCRIPTION
    Get the XML documentation for every Google Cloud Powershell cmdlet and write relevant warnings. 

    .EXAMPLE
    Give an example of how to use it

    .EXAMPLE
    Give another example of how to use it

    .PARAMETER CloudProducts
    List the Cloud Product(s) (by full name or cmdlet abbreviation) whose cmdlet docs you want to check.

    .PARAMETER DeepExampleCheck
    Include this optional switch to check example format, intro, and output.
    #>   

    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory=$false,
                   ParameterSetName = "ProductNames",
                   HelpMessage='List the Cloud Product(s) (by full name or cmdlet abbreviation)' +
                               'whose cmdlet docs you want to check.')]
        [ValidateSet('Gcs','Gce','Gcd','GcSql',
                     'Google Cloud Storage','Google Compute Engine','Google Cloud DNS','Google Cloud SQL')]
        [Alias('Products')]
        [String[]]
        $CloudProducts,

        [Parameter(Mandatory=$false,
                   ValueFromPipeline=$true,
                   ValueFromPipelineByPropertyName=$true,
                   ParameterSetName = "CmdletNames",
                   HelpMessage='List the full names of the cmdlets that you want to check.')]
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

    if ($CmdletNames.Count -gt 0) {
         $cmdlets = $cmdlets | where { $CmdletNames -contains $_.Name}
         $cmdletsNotFound = $CmdletNames | where { -not ($cmdlets.Name -contains $_) }
         if ($cmdletsNotFound.Count -gt 0) {
            Write-Host ("`nThe following cmdlets you named were not found:")
            Write-Host ($cmdletsNotFound)
         }
         if ($cmdlets.Count -le 0) {
            Write-Host ("`nThere are no cmdlets to check.")
         }
    }

    # Mapping between cmdlet name and the Cloud resource.
    $apiMappings = @{
        "Gcs" = "Google Cloud Storage"
        "Gce" = "Google Compute Engine"
        "Gcd" = "Google Cloud DNS"
        "GcSql" = "Google Cloud SQL"
    }

    # Check each cmdlet's documentation and output relevant warnings.
    foreach ($cmdlet in $cmdlets) {
        # Initially assume that either that no products were specified,
        # or all cmdlets are in one of the specified products.
        $inChosenProducts = $true

        # Determine which product the cmdlet belongs to, e.g., "Google Compute Engine".
        $cmdletResource = $cmdlet.Name.Split("-")[1]
        $cloudProduct = "";
        foreach ($apiMapping in $apiMappings.GetEnumerator()) {
            if ($cmdletResource.StartsWith($apiMapping.Key)) {
                $cloudProduct = $apiMapping.Value
                
                # Check if the cmdlet product is one of the specified products.
                if (($CloudProducts.Count -gt 0) -and 
                    (-not (($CloudProducts -contains $apiMapping.Key) -or 
                           ($CloudProducts -contains $apiMapping.Value)))) {
                    $inChosenProducts = $false
                }

                break
            }
        }

        # If the cmdlet is not within one of the specified products, abort the check.
        if (-not $inChosenProducts) {
            continue
        }

        Write-Host "`nChecking $($cmdlet.Name)..."
        
        # Get the cmdlet's documentation.
        $docObj = Get-Help -Full $cmdlet.Name
        
        if ($cmdlet.Name -match "Get-GcdManagedZone")
        {
            $docObj2 = $docObj
        }
        
        $name = ($docObj.Name | Out-String).Trim()
        $synopsis = ($docObj.Synopsis | Out-String).Trim()
        $description = ($docObj.Description | Out-String).Trim()
        $examples = ($docObj.examples | Out-String).Trim()
        $outputType = ($docObj.returnValues | Out-String).Trim()

        # Check the cmdlet's documentation for important information/categories and add relevant warnings.
        $warnings = New-Object System.Collections.Generic.List[System.Object];

        if ($cloudProduct -eq "") {
            $warnings.Add("Cmdlet $($cmdlet.Name) is not associated with a product.")
        }

        if ($name -eq "") {
            $warnings.Add("Cmdlet $($cmdlet.Name)'s documentation lacks its name.")
        }

        if ($synopsis -eq "") {
            $warnings.Add("Cmdlet $($cmdlet.Name) does not have a synopsis.")
        }

        if ($description -eq "") {
            $warnings.Add("Cmdlet $($cmdlet.Name) does not have a description.")
        }

        if ($examples -eq "") {
            $warnings.Add("Cmdlet $($cmdlet.Name) does not have any examples.")
        }
        # If there are examples, and the user has chosen a DeepExampleCheck, check if the examples include a command
        # starting with the usual PS C:\>.
        # If they do, check that they also have an intro and sample output for the command.
        elseif ($DeepExampleCheck ){
            $noPSStart = New-Object System.Collections.Generic.List[System.Object];
            $noIntro = New-Object System.Collections.Generic.List[System.Object];
            $noOutput = New-Object System.Collections.Generic.List[System.Object];
            $currentExample = 1; 

            foreach ($example in $docObj.examples.example) {
                $exampleString = ($example  | Out-String).Trim()
  
                if (-not ($exampleString -match [regex]::Escape('PS C:\>'))) {
                    $noPSStart.Add($currentExample)
                }
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
                $warnings.Add("Cmdlet $($cmdlet.Name)'s example number(s) " + ($noPSStart -join ",") + 
                              " does(do) not have commands starting with the expected PS C:\>. " + 
                              "(Thus, cannot check for command intro or example output.)")
            }
            if ($noIntro.Count -gt 0) {
                $warnings.Add("Cmdlet $($cmdlet.Name)'s example number(s) " + ($noIntro -join ",") + " has(have) no introduction.")
            }
            if ($noOutput.Count -gt 0) {
                $warnings.Add("Cmdlet $($cmdlet.Name)'s example number(s) " + ($noOutput -join ",") + " has(have) no outputs.")
            }
        }

        if ($outputType -eq "") {
            $warnings.Add("Cmdlet $($cmdlet.Name) does not have an output type.")
        }

        if ($warnings.Count -gt 0) {
            $warnings | Write-Warning
        }
    }
}
