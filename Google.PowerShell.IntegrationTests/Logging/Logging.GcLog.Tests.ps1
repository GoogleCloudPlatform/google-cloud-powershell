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
    gcloud beta logging write $logName $textPayload --severity=ALERT 2>$null
    gcloud beta logging write $logName $jsonPayload --severity=INFO --payload-type=json 2>$null
    gcloud beta logging write $secondLogName $secondTextPayload --severity=ALERT 2>$null
    gcloud beta logging write $secondLogName $jsonPayload --severity=INFO --payload-type=json 2>$null
    # We add 2 minutes to account for delay in log creation
    $timeAfterCreatingLogs = [DateTime]::Now.AddMinutes(2)

    AfterAll {
        gcloud beta logging logs delete $logName --quiet
        $logs = Get-GcLogEntry -LogName $logName
        if ($null -ne $logs)
        {
            Write-Host "Log $logName is not deleted."
        }
        gcloud beta logging logs delete $secondLogName --quiet
        $logs = Get-GcLogEntry -LogName $secondLogName
        if ($null -ne $logs)
        {
            Write-Host "Log $secondLogName is not deleted."
        }
    }

    It "should work without any parameters" {
        # There are a lot of logs so we just want to get the first 10.
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
    }

    It "should not return anything for non-existent log" {
        (Get-GcLogEntry -LogName "non-existent-log-name") | Should BeNullOrEmpty
    }

    It "should work with -Severity parameter" {
        $logEntries = Get-GcLogEntry -Severity Alert
        # We can't use exact value here because this will include entries from other logs.
        $logEntries.Count -ge 2 | Should Be $true
        $logEntries | Where-Object { $_.TextPayload -eq $textPayload } | Should Not BeNullOrEmpty
        $logEntries | Where-Object { $_.TextPayload -eq $secondTextPayload } | Should Not BeNullOrEmpty
        $logEntries | ForEach-Object { $_.Severity | Should Be ALERT }
    }

    It "should work with -Severity and -LogName parameters" {
        # Tests with -LogName parameter too.
        $logEntries = Get-GcLogEntry -LogName $secondLogName -Severity INFO
        $logEntries.Count | Should Be 1
        $logEntries.Severity | Should Be INFO
    }

    It "should work with -After and -Before parameter" {
        # We should get the logs we created in the test.
        $logEntries = Get-GcLogEntry -After $timeBeforeCreatingLogs
        # We can't use exact value here because this will include entries from other logs.
        $logEntries.Count -ge 4 | Should Be $true
        $textLogEntries = $logEntries | Where-Object { -not [string]::IsNullOrWhiteSpace($_.TextPayload) }
        $textLogEntries.Count -ge 2 | Should Be $true
        $jsonLogEntries = $logEntries | Where-Object { $null -ne $_.JsonPayload }
        $jsonLogEntries.Count -ge 2 | Should Be $true
    }
    It "should work with -LogName and -After and -Before parameters" {
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

Describe "New-GcLogEntry" {
    It "should work" {
        $r = Get-Random
        # This log entry does not exist before so the cmdlet should create it.
        $logName = "gcp-testing-new-gclogentry-$r"
        $textPayload = "This is a log entry"
        try {
            New-GcLogEntry -TextPayload $textPayload -LogName $logName
            Start-Sleep 5
            (Get-GcLogEntry -LogName $logName).TextPayload | Should BeExactly $textPayload

            # Create a JSON entry in the same log, the cmdlet should write to the same log.
            New-GcLogEntry -JsonPayload @{ "Key" = "Value" } -LogName $logName
            Start-Sleep 5
            $logEntries = Get-GcLogEntry -LogName $logName
            $logEntries.Count | Should Be 2
            $jsonLogEntry = $logEntries | Where-Object { $null -ne $_.JsonPayload }
            $jsonLogEntry.JsonPayload.Key | Should BeExactly "Value"

            # Create a Proto entry.
            $proto = @{ "@type" = "type.googleapis.com/google.cloud.audit.AuditLog";
                        "serviceName" = "cloudresourcemanager.googleapis.com" }
            New-GcLogEntry -ProtoPayload $proto -LogName $logName
            Start-Sleep 5
            $logEntries = Get-GcLogEntry -LogName $logName
            $logEntries.Count | Should Be 3
            $protoLogEntry = $logEntries | Where-Object { $null -ne $_.ProtoPayload }
            $protoLogEntry.ProtoPayload["@type"] | Should BeExactly $proto["@type"]
            $protoLogEntry.ProtoPayload["serviceName"] | Should BeExactly $proto["serviceName"]
        }
        finally {
            gcloud beta logging logs delete $logName --quiet
        }
    }

    It "should work for array" {
        $r = Get-Random
        $logName = "gcp-testing-new-gclogentry-$r"
        $firstTextPayload = "This is the first payload."
        $secondTextPayload = "This is the second payload."
        $firstJsonPayload = @{ "Key" = "Value" }
        $secondJsonPayload = @{ "Key2" = "Value2" }
        $firstProtoPayload = @{ "@type" = "type.googleapis.com/google.cloud.audit.AuditLog";
                    "serviceName" = "cloudresourcemanager.googleapis.com" }
        $secondProtoPayload = @{ "@type" = "type.googleapis.com/google.cloud.audit.AuditLog";
                    "serviceName" = "www.cloudresourcemanager.googleapis.com" }
        try {
            New-GcLogEntry -TextPayload @($firstTextPayload, $secondTextPayload) -LogName $logName
            Start-Sleep 5
            $logEntriesPayloads = (Get-GcLogEntry -LogName $logName).TextPayload
            $logEntriesPayloads.Count | Should Be 2

            New-GcLogEntry -JsonPayload @($firstJsonPayload, $secondJsonPayload) -LogName $logName
            Start-Sleep 5
            $logEntriesJsonPayloads = Get-GcLogEntry -LogName $logName | Where-Object { $null -ne $_.JsonPayload }
            $logEntriesJsonPayloads.Count | Should Be 2

            New-GcLogEntry -ProtoPayload @($firstProtoPayload, $secondProtoPayload) -LogName $logName
            Start-Sleep 5
            $logEntriesProtoPayloads = Get-GcLogEntry -LogName $logName | Where-Object { $null -ne $_.ProtoPayload }
            $logEntriesProtoPayloads.Count | Should Be 2
        }
        finally {
            gcloud beta logging logs delete $logName --quiet 2>$null
        }
    }

    It "should work with pipeline" {
        $r = Get-Random
        $logName = "gcp-testing-new-gclogentry-$r"
        $textPayload = "This is the text payload."
        $jsonPayload = @{ "Key" = "Value" }

        try {
            $textPayload | New-GcLogEntry -LogName $logName
            Start-Sleep 5
            (Get-GcLogEntry -LogName $logName).TextPayload | Should BeExactly $textPayload

            $jsonPayload | New-GcLogEntry -LogName $logName
            Start-Sleep 5
            $logEntriesJsonPayload = Get-GcLogEntry -LogName $logName | Where-Object { $null -ne $_.JsonPayload }
            $logEntriesJsonPayload.JsonPayload["Key"] | Should BeExactly "Value"
        }
        finally {
            gcloud beta logging logs delete $logName --quiet 2>$null
        }
    }

    It "should work with -MonitoredResource" {
        $r = Get-Random
        $logName = "gcp-testing-new-gclogentry-$r"
        $textPayload = "This is the text payload."
        $resourceType = "cloudsql_database"
        $resourceLabels = @{ "project_id" = "my-project" ; "database_id" = "mydatabaseid" }
        $monitoredResource = New-GcLogMonitoredResource -ResourceType $resourceType -Labels $resourceLabels

        try {
            New-GcLogEntry -LogName $logName -MonitoredResource $monitoredResource -TextPayload $textPayload
            Start-Sleep 5
            $logEntry = Get-GcLogEntry -LogName $logName
            $logEntry.TextPayload | Should BeExactly $textPayload
            $logEntry.Resource.Type | Should BeExactly $resourceType
            $logEntry.Resource.Labels["project_id"] | Should BeExactly $resourceLabels["project_id"]
            $logEntry.Resource.Labels["database_id"] | Should BeExactly $resourceLabels["database_id"]
        }
        finally {
            gcloud beta logging logs delete $logName --quiet 2>$null
        }
    }
}

Describe "Remove-GcLog" {
    It "should throw error for non-existent log" {
        { Remove-GcLog -LogName "non-existent-log-powershell-testing" } | Should Throw "404"
    }

    It "should work" {
        $r = Get-Random
        $logName = "gcp-testing-new-gclogentry-$r"
        $textPayload = "This is the text payload."
        New-GcLogEntry -LogName $logName -TextPayload $textPayload
        Start-Sleep 5
        (Get-GcLogEntry -LogName $logName) | Should Not BeNullOrEmpty
        Remove-GcLog -LogName $logName
        Start-Sleep 5
        (Get-GcLogEntry -LogName $logName) | Should BeNullOrEmpty
    }
}
