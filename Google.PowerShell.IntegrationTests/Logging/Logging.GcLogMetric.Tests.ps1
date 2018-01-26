. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

# Given a filter, extract the first time stamp from the filter and returns the DateTime.
function Get-DateTimeFromFilter($filter) {
    # This regex matches "timestamp = "(DateTime)"
    if ($filter -match ".*timestamp\s*[<>]=\s*`"(.*)`"") {
        return [DateTime]$Matches[1]
    }
    return null
}

Describe "Get-GcLogMetric" {
    $r = Get-Random
    $script:metricName = "gcps-get-gclogmetric-$r"
    $script:metricNameTwo = "gcps-get-gclogmetric-2-$r"
    $logFilter = "this is a filter"
    $logFilterTwo = "this is another filter"
    $description = "This is the first description"
    $descriptionTwo = "This is the second description"
    gcloud logging metrics create $script:metricName --description=$description --log-filter=$logFilter --quiet 2>$null
    gcloud logging metrics create $script:metricNameTwo --description=$descriptionTwo --log-filter=$logFilterTwo --quiet 2>$null
    

    AfterAll {
        gcloud logging metrics delete $metricName --quiet 2>$null
        gcloud logging metrics delete $metricNameTwo --quiet 2>$null
    }

    It "should work without any parameters" {
        $metrics = Get-GcLogMetric

        $firstMetric = $metrics | Where-Object {$_.Name -eq $metricName}
        $firstMetric | Should Not BeNullOrEmpty
        $firstMetric.Description | Should BeExactly $description
        $firstMetric.Filter | Should BeExactly $logFilter

        $secondMetric = $metrics | Where-Object {$_.Name -eq $metricNameTwo}
        $secondMetric | Should Not BeNullOrEmpty
        $secondMetric.Description | Should BeExactly $descriptionTwo
        $secondMetric.Filter | Should BeExactly $logFilterTwo
    }

    It "should work with -MetricName parameter" {
        $metric = Get-GcLogMetric -MetricName $metricName
        $metric | Should Not BeNullOrEmpty
        $metric.Name | Should BeExactly "$metricName"
        $metric.Description | Should BeExactly $description
        $metric.Filter | Should BeExactly $logFilter
    }

    It "should work with an array of metrics" {
        $metrics = Get-GcLogMetric -MetricName $metricName, $metricNameTwo
        $metrics.Count | Should Be 2

        $firstMetric = $metrics | Where-Object {$_.Name -eq $metricName}
        $firstMetric | Should Not BeNullOrEmpty
        $firstMetric.Description | Should BeExactly $description
        $firstMetric.Filter | Should BeExactly $logFilter

        $secondMetric = $metrics | Where-Object {$_.Name -eq $metricNameTwo}
        $secondMetric | Should Not BeNullOrEmpty
        $secondMetric.Description | Should BeExactly $descriptionTwo
        $secondMetric.Filter | Should BeExactly $logFilterTwo
    }

    It "should throw an error for non-existent metric" {
        { Get-GcLogMetric -Metric "non-existent-metric-name" -ErrorAction Stop } | Should Throw "does not exist"
    }
}

Describe "New-GcLogMetric" {
    It "should work with -LogName" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $logName = "gcps-new-gclogmetric-log-$r"
        try {
            $createdMetric = New-GcLogMetric $metricName -LogName $logName
            $onlineMetric = Get-GcLogMetric $metricName

            ForEach ($metric in @($createdMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                $metric.Filter | Should BeExactly "logName = `"projects/$project/logs/$logName`""
                $metric.Description | Should BeNullOrEmpty
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }

    It "should work with -Before and -After" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $metricNameTwo = "gcps-new-gclogmetric-2-$r"
        $before = [DateTime]::new(2017, 1, 1)
        $after = [DateTime]::new(2017, 12, 12)
        try {
            $createdMetricOne = New-GcLogMetric $metricName -Before $before
            $createdMetricTwo = New-GcLogMetric $metricNameTwo -After $after
            $onlineMetricOne = Get-GcLogMetric $metricName
            $onlineMetricTwo = Get-GcLogMetric $metricNameTwo

            ForEach ($metric in @($createdMetricOne, $onlineMetricOne)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                Get-DateTimeFromFilter $metric.Filter | Should Be $before
                $metric.Description | Should BeNullOrEmpty
            }

            ForEach ($metric in @($createdMetricTwo, $onlineMetricTwo)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricNameTwo
                Get-DateTimeFromFilter $metric.Filter | Should Be $after
                $metric.Description | Should BeNullOrEmpty
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
            gcloud logging metrics delete $metricNameTwo --quiet 2>$null
        }
    }

    It "should work with -Severity" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        try {
            $createdMetric = New-GcLogMetric $metricName -Severity ERROR
            $onlineMetric = Get-GcLogMetric $metricName

            ForEach ($metric in @($createdMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                $metric.Filter | Should BeExactly "severity = ERROR"
                $metric.Description | Should BeNullOrEmpty
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }

    It "should work with -ResourceType" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $resourceType = "gce_instance"
        try {
            $createdMetric = New-GcLogMetric $metricName -ResourceType $resourceType
            $onlineMetric = Get-GcLogMetric $metricName

            ForEach ($metric in @($createdMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                $metric.Filter | Should BeExactly "resource.type = `"$resourceType`""
                $metric.Description | Should BeNullOrEmpty
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }

    It "should work with -Filter" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $filter = "textPayload = testing"
        try {
            $createdMetric = New-GcLogMetric $metricName -Filter $filter
            $onlineMetric = Get-GcLogMetric $metricName

            ForEach ($metric in @($createdMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                $metric.Filter | Should BeExactly $filter
                $metric.Description | Should BeNullOrEmpty
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }

    It "should work with -Description" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $logName = "gcps-new-gclogmetric-log-$r"
        $description = "This is a log metric"
        try {
            $createdMetric = New-GcLogMetric $metricName -LogName $logName -Description $description
            $onlineMetric = Get-GcLogMetric $metricName

            ForEach ($metric in @($createdMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                $metric.Filter | Should BeExactly "logName = `"projects/$project/logs/$logName`""
                $metric.Description | Should BeExactly $description
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }

    It "should work with multiple parameters" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $metricNameTwo = "gcps-new-gclogmetric-2-$r"
        $logName = "gcps-new-gclogmetric-log-$r"
        $description = "This is a log metric"
        $after = [DateTime]::new(2017, 12, 12)
        try {
            $createdMetric = New-GcLogMetric $metricName -LogName $logName -Description $description -Severity INFO
            $onlineMetric = Get-GcLogMetric $metricName

            ForEach ($metric in @($createdMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                $metric.Filter |
                    Should BeExactly "logName = `"projects/$project/logs/$logName`" AND severity = INFO"
                $metric.Description | Should BeExactly $description
            }

            $createdMetric = New-GcLogMetric $metricNameTwo -Description $description -Severity ERROR -After $after
            $onlineMetric = Get-GcLogMetric $metricNameTwo

            ForEach ($metric in @($createdMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricNameTwo
                $metric.Filter | Should Match "severity = ERROR AND timestamp"
                Get-DateTimeFromFilter $metric.Filter | Should Be $after
                $metric.Description | Should BeExactly $description
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
            gcloud logging metrics delete $metricNameTwo --quiet 2>$null
        }
    }

    It "should throw error for existing log metric" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $logName = "gcps-new-gclogmetric-log-$r"
        try {
            New-GcLogMetric $metricName -LogName $logName
            { New-GcLogMetric $metricName -LogName $logName -ErrorAction Stop } |
                Should Throw "already exists."
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }

    It "should throw error if filter cannot be constructed" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $logName = "gcps-new-gclogmetric-log-$r"
        try {
            { New-GcLogMetric $metricName -ErrorAction Stop } |
                Should Throw "Cannot construct filter"
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }
}

Describe "Set-GcLogMetric" {
    It "should work with -LogName" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $logName = "gcps-new-gclogmetric-log-$r"
        $secondLogName = "gcps-set-gclogmetric-log-$r"
        try {
            New-GcLogMetric $metricName -LogName $logName

            $updatedMetric = Set-GcLogMetric $metricName -LogName $secondLogName
            $onlineMetric = Get-GcLogMetric $metricName

            ForEach ($metric in @($updatedMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                $metric.Filter | Should BeExactly "logName = `"projects/$project/logs/$secondLogName`""
                $metric.Description | Should BeNullOrEmpty
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }

    It "should work with -Before and -After" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $metricNameTwo = "gcps-new-gclogmetric-2-$r"
        $before = [DateTime]::new(2017, 1, 1)
        $after = [DateTime]::new(2017, 12, 12)
        try {
            New-GcLogMetric $metricName -Before $after
            New-GcLogMetric $metricNameTwo -After $before

            $updatedMetricOne = Set-GcLogMetric $metricName -Before $before
            $updatedMetricTwo = Set-GcLogMetric $metricNameTwo -After $after
            $onlineMetricOne = Get-GcLogMetric $metricName
            $onlineMetricTwo = Get-GcLogMetric $metricNameTwo

            ForEach ($metric in @($updatedMetricOne, $onlineMetricOne)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                Get-DateTimeFromFilter $metric.Filter | Should Be $before
                $metric.Description | Should BeNullOrEmpty
            }

            ForEach ($metric in @($updatedMetricTwo, $onlineMetricTwo)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricNameTwo
                Get-DateTimeFromFilter $metric.Filter | Should Be $after
                $metric.Description | Should BeNullOrEmpty
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
            gcloud logging metrics delete $metricNameTwo --quiet 2>$null
        }
    }

    It "should work with -Severity" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        try {
            New-GcLogMetric $metricName -Severity INFO

            $updatedMetric = Set-GcLogMetric $metricName -Severity ERROR
            $onlineMetric = Get-GcLogMetric $metricName

            ForEach ($metric in @($updatedMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                $metric.Filter | Should BeExactly "severity = ERROR"
                $metric.Description | Should BeNullOrEmpty
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }

    It "should work with -ResourceType" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $resourceType = "gce_instance"
        try {
            New-GcLogMetric $metricName -ResourceType global

            $updatedMetric = Set-GcLogMetric $metricName -ResourceType $resourceType
            $onlineMetric = Get-GcLogMetric $metricName

            ForEach ($metric in @($updatedMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                $metric.Filter | Should BeExactly "resource.type = `"$resourceType`""
                $metric.Description | Should BeNullOrEmpty
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }

    It "should work with -Filter" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $filter = "textPayload = testing"
        $secondFilter = "textPayload = second-payload"
        try {
            New-GcLogMetric $metricName -Filter $filter

            $updatedMetric = Set-GcLogMetric $metricName -Filter $secondFilter
            $onlineMetric = Get-GcLogMetric $metricName

            ForEach ($metric in @($updatedMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                $metric.Filter | Should BeExactly $secondFilter
                $metric.Description | Should BeNullOrEmpty
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }

    It "should work with -Description" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $logName = "gcps-new-gclogmetric-log-$r"
        $description = "This is a log metric"
        try {
            New-GcLogMetric $metricName -LogName $logName

            $updatedMetric = Set-GcLogMetric $metricName -Description $description
            $onlineMetric = Get-GcLogMetric $metricName

            ForEach ($metric in @($updatedMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                $metric.Filter | Should BeExactly "logName = `"projects/$project/logs/$logName`""
                $metric.Description | Should BeExactly $description
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }

    It "should work with multiple parameters" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $metricNameTwo = "gcps-new-gclogmetric-2-$r"
        $logName = "gcps-new-gclogmetric-log-$r"
        $description = "This is a log metric"
        $after = [DateTime]::new(2017, 12, 12)
        try {
            New-GcLogMetric $metricName -Filter "Testing" -Severity ERROR -Description $description

            $updatedMetric = Set-GcLogMetric $metricName -LogName $logName -Severity INFO
            $onlineMetric = Get-GcLogMetric $metricName

            ForEach ($metric in @($updatedMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                $metric.Filter |
                    Should BeExactly "logName = `"projects/$project/logs/$logName`" AND severity = INFO"
                $metric.Description | Should BeExactly $description
            }

            New-GcLogMetric $metricNameTwo -LogName $logName -Description $description

            $updatedMetric = Set-GcLogMetric $metricNameTwo -Severity ERROR -After $after
            $onlineMetric = Get-GcLogMetric $metricNameTwo

            ForEach ($metric in @($updatedMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricNameTwo
                $metric.Filter | Should Match "severity = ERROR AND timestamp"
                Get-DateTimeFromFilter $metric.Filter | Should Be $after
                $metric.Description | Should BeExactly $description
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
            gcloud logging metrics delete $metricNameTwo --quiet 2>$null
        }
    }

    It "should create a metric if it doesn't exist" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $logName = "gcps-new-gclogmetric-log-$r"
        try {
            $createdMetric = Set-GcLogMetric $metricName -LogName $logName
            $onlineMetric = Get-GcLogMetric $metricName

            ForEach ($metric in @($createdMetric, $onlineMetric)) {
                $metric | Should Not BeNullOrEmpty
                $metric.Name | Should BeExactly $metricName
                $metric.Filter | Should BeExactly "logName = `"projects/$project/logs/$logName`""
                $metric.Description | Should BeNullOrEmpty
            }
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }

    It "should throw an error if it cannot create a metric" {
        $r = Get-Random
        $metricName = "gcps-new-gclogmetric-$r"
        $logName = "gcps-new-gclogmetric-log-$r"
        try {
            { Set-GcLogMetric $metricName } | Should Throw "Cannot construct filter"
        }
        finally {
            gcloud logging metrics delete $metricName --quiet 2>$null
        }
    }
}

Describe "Remove-GcLogMetric" {
    It "should throw error for non-existent log metric" {
        { Remove-GcLogMetric -MetricName "non-existent-log-metric-powershell-testing" -ErrorAction Stop } |
            Should Throw "does not exist"
    }

    It "should work" {
        $r = Get-Random
        $metricName = "gcps-remove-gclogmetric-$r"
        New-GcLogMetric $metricName -Filter "This is a filter"
        Get-GcLogMetric -MetricName $metricName | Should Not BeNullOrEmpty

        Remove-GcLogMetric $metricName
        { Get-GcLogMetric -MetricName $metricName -ErrorAction Stop } | Should Throw "does not exist"
    }


    It "should work for multiple metrics" {
        $r = Get-Random
        $metricName = "gcps-remove-gclogmetric-$r"
        $metricNameTwo = "gcps-remove-gclogmetric-2-$r"
        New-GcLogMetric $metricName -Filter "This is a filter"
        New-GcLogMetric $metricNameTwo -Filter "This is a filter"
        Get-GcLogMetric -MetricName $metricName | Should Not BeNullOrEmpty
        Get-GcLogMetric -MetricName $metricNameTwo | Should Not BeNullOrEmpty

        Remove-GcLogMetric $metricName, $metricNameTwo
        { Get-GcLogMetric -MetricName $metricName -ErrorAction Stop } | Should Throw "does not exist"
        { Get-GcLogMetric -MetricName $metricNameTwo -ErrorAction Stop } | Should Throw "does not exist"
    }

    It "should work for log metric object" {
        $r = Get-Random
        $metricName = "gcps-remove-gclogmetric-$r"
        New-GcLogMetric $metricName -Filter "This is a filter"

        $createdMetricObject = Get-GcLogMetric -MetricName $metricName

        Remove-GcLogMetric $createdMetricObject
        { Get-GcLogMetric -MetricName $metricName -ErrorAction Stop } | Should Throw "does not exist"
    }

    It "should work with pipelining" {
        $r = Get-Random
        $metricName = "gcps-remove-gclogmetric-$r"
        $metricNameTwo = "gcps-remove-gclogmetric-2-$r"
        New-GcLogMetric $metricName -Filter "This is a filter"
        New-GcLogMetric $metricNameTwo -Filter "This is a filter"
        Get-GcLogMetric -MetricName $metricName | Should Not BeNullOrEmpty
        Get-GcLogMetric -MetricName $metricNameTwo | Should Not BeNullOrEmpty

        Get-GcLogMetric -MetricName $metricName, $metricNameTwo | Remove-GcLogMetric
        { Get-GcLogMetric -MetricName $metricName -ErrorAction Stop } | Should Throw "does not exist"
        { Get-GcLogMetric -MetricName $metricNameTwo -ErrorAction Stop } | Should Throw "does not exist"
    }
}
