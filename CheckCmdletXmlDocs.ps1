# Get the XML documentation for every Gcloud Powershell cmdlet and 
# output relevant warnings for potentially incomplete docs. 

function Check-CmdletDoc() {
    $binDirectory = Join-Path $PSScriptRoot "\..\Google.PowerShell\bin\"
    Import-Module "$binDirectory\Debug\Google.PowerShell.dll"

    $cmdlets = Get-Command -Module "Google.PowerShell" | sort { $_ -replace ".*-",'' }

    # Mapping between cmdlet name and the Cloud resource.
    $apiMappings = @{
        "Gcs" = "Google Cloud Storage"
        "Gce" = "Google Compute Engine"
        "Gcd" = "Google Cloud DNS"
        "GcSql" = "Google Cloud SQL"
    }

    $cmdletDocText = ""
    # Check each cmdlet's documentation and output relevant warnings.
    foreach ($cmdlet in $cmdlets) {
        Write-Host "`nChecking $($cmdlet.Name)..."

        $cmdletVerb = $cmdlet.Name.Split("-")[0]
        $cmdletResource = $cmdlet.Name.Split("-")[1]

        # Get the cmdlet's documentation.
        $docObj = Get-Help -Full $cmdlet.Name
        <#> 
        if ($cmdlet.Name -match "Get-GcdManagedZone")
        {
            $docObj2 = $docObj
        }
        <#>
        $name = ($docObj.Name | Out-String).Trim()
        $synopsis = ($docObj.Synopsis | Out-String).Trim()
        $description = ($docObj.Description | Out-String).Trim()
        $examples = ($docObj.examples | Out-String).Trim()
        $outputType = ($docObj.returnValues | Out-String).Trim()

        # Determine which product the cmdlet belongs to, e.g., "Google Compute Engine".
        $cloudProduct = "";
        foreach ($apiMapping in $apiMappings.GetEnumerator()) {
            if ($cmdletResource.StartsWith($apiMapping.Key)) {
                $cloudProduct = $apiMapping.Value
                break
            }
        }

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
        else {
            #If there are examples, check if they have a command starting with the usual PS C:\>.
            #If they do, check that they also have an intro and sample output for the command.

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
                $warnings.Add("Cmdlet $($cmdlet.Name)'s example number(s) " + ($noPSStart -join ",") + " does(do) not have commands starting with the expected PS C:\>.")
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
