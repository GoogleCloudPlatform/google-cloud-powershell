. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcLogMetric" {
    $r = Get-Random
    $script:metricName = "gcps-get-gclogmetric-$r"
    $script:metricNameTwo = "gcps-get-gclogmetric-2-$r"
    $logFilter = "this is a filter"
    $logFilterTwo = "this is another filter"
    $description = "This is the first description"
    $descriptionTwo = "This is the second description"
    gcloud beta logging metrics create $script:metricName --description=$description --log-filter=$logFilter --quiet 2>$null
    gcloud beta logging metrics create $script:metricNameTwo --description=$descriptionTwo --log-filter=$logFilterTwo --quiet 2>$null
    

    AfterAll {
        gcloud beta logging metrics delete $metricName --quiet 2>$null
        gcloud beta logging metrics delete $metricNameTwo --quiet 2>$null
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
}
