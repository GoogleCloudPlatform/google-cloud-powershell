. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcLogEntry" {
    $r = Get-Random
    $script:logName = "gcp-testing-log-$r"
    $script:secondLogName = "gcp-testing-log2-$r"
    $textPayload = "Test entry"
    $secondTextPayload = "Second test entry"
    $jsonPayload = "{\`"Key\`":\`"Value\`"}"
    $timeBeforeCreatingLogs = [DateTime]::Now
    gcloud beta logging write $logName $textPayload --severity=ALERT > $null
    gcloud beta logging write $logName $jsonPayload --severity=INFO --payload-type=json
    gcloud beta logging write $secondLogName $secondTextPayload --severity=ALERT
    gcloud beta logging write $secondLogName $jsonPayload --severity=INFO --payload-type=json
    # We add 2 minutes to account for delay in log creation
    $timeAfterCreatingLogs = [DateTime]::Now.AddMinutes(2)

    AfterAll {
        gcloud beta logging logs delete $logName --quiet
        gcloud beta logging logs delete $secondLogName --quiet
    }

    It "should work without any parameters" {
        # There are a lot of logs so we just want to get the first 10
        $logEntries = Get-GcLogEntry | Select -First 10
        $logEntries.Count | Should Be 10
    }

    It "should work with -LogName parameter" {
        $logEntries = Get-GcLogEntry -LogName $logName
        $logEntries.Count | Should Be 2
        $textLogEntry = $logEntries | Where-Object { -not [string]::IsNullOrWhiteSpace($_.TextPayload) }
        $textLogEntry.TextPayload | Should BeExactly $textPayload
        $jsonLogEntry = $logEntries | Where-Object { $null -ne $_.JsonPayload }
        $jsonLogEntry.JsonPayload["Key"] | Should BeExactly "Value"

        # Should not return anything for non-existent parameter
        (Get-GcLogEntry -LogName "non-existent-log-name") | Should BeNullOrEmpty
    }

    It "should work with -Severity parameter" {
        $logEntries = Get-GcLogEntry -Severity Alert
        $logEntries.Count -ge 2 | Should Be $true
        $logEntries | Where-Object { $_.TextPayload -eq $textPayload } | Should Not BeNullOrEmpty
        $logEntries | Where-Object { $_.TextPayload -eq $secondTextPayload } | Should Not BeNullOrEmpty
        $logEntries | ForEach-Object { $_.Severity | Should Be ALERT }
        
        # Tests with -LogName parameter too.
        $logEntries = Get-GcLogEntry -LogName $secondLogName -Severity INFO
        $logEntries.Count | Should Be 1
        $logEntries.Severity | Should Be INFO
    }

    It "should work with -After and -Before parameter" {
        # We should get the logs we created in the test.
        $logEntries = Get-GcLogEntry -After $timeBeforeCreatingLogs
        $logEntries.Count -ge 4 | Should Be $true
        $textLogEntries = $logEntries | Where-Object { -not [string]::IsNullOrWhiteSpace($_.TextPayload) }
        $textLogEntries.Count -ge 2 | Should Be $true
        $jsonLogEntries = $logEntries | Where-Object { $null -ne $_.JsonPayload }
        $jsonLogEntries.Count -ge 2 | Should Be $true

        # Tests with -LogName parameter.
        $logEntries = Get-GcLogEntry -After $timeAfterCreatingLogs -LogName $logName
        $logEntries | Should BeNullOrEmpty
        $logEntries = Get-GcLogEntry -Before $timeBeforeCreatingLogs -LogName $secondLogName
        $logEntries | Should BeNullOrEmpty
        $logEntries = Get-GcLogEntry -After $timeBeforeCreatingLogs -Before $timeAfterCreatingLogs -LogName $logName
        $logEntries.Count | Should Be 2
        $textLogEntry = $logEntries | Where-Object { -not [string]::IsNullOrWhiteSpace($_.TextPayload) }
        $textLogEntry.TextPayload | Should BeExactly $textPayload
        $jsonLogEntry = $logEntries | Where-Object { $null -ne $_.JsonPayload }
        $jsonLogEntry.JsonPayload["Key"] | Should BeExactly "Value"
    }

    It "should work with -ResourceType parameter" {
        $gceInstanceLogEntries = Get-GcLogEntry -ResourceType gce_instance | Select -First 2
        $gceInstanceLogEntries.Resource | ForEach-Object { $_.Type | Should BeExactly gce_instance }

        # The gcloud beta logging write uses global resource type.
        $globalResourceTypeLogEntries = Get-GcLogEntry -ResourceType global -LogName $logName
        $globalResourceTypeLogEntries.Count | Should Be 2
        $globalResourceTypeLogEntries[0].Resource.Type | Should BeExactly global
    }

    It "should work with -Filter parameter" {
        # Simple filter.
        $logEntries = Get-GcLogEntry -Filter "logName=`"projects/$project/logs/$logName`""
        $logEntries.Count | Should Be 2
        $textLogEntry = $logEntries | Where-Object { -not [string]::IsNullOrWhiteSpace($_.TextPayload) }
        $textLogEntry.TextPayload | Should BeExactly $textPayload
        $jsonLogEntry = $logEntries | Where-Object { $null -ne $_.JsonPayload }
        $jsonLogEntry.JsonPayload["Key"] | Should BeExactly "Value"

        # More advanced filter.
        $textLogEntry = Get-GcLogEntry -Filter "logName=`"projects/$project/logs/$logName`" AND textPayload=`"$textPayload`""
        $textLogEntry.TextPayload | Should BeExactly $textPayload
    }
}
