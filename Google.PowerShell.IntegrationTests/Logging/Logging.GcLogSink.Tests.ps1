. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcpsLogSink" {
    $r = Get-Random
    $script:sinkName = "gcps-get-gclogsink-$r"
    $script:secondSinkName = "gcps-get-gclogsink2-$r"
    $destination = "storage.googleapis.com/random-destination-will-do-$r"
    $destinationTwo = "storage.googleapis.com/random-destination-will-do2-$r"
    $logFilter = "this is a filter"
    $previousCount = (Get-GcLogSink).Count
    gcloud beta logging sinks create $script:sinkName $destination `
                                     --log-filter=$logFilter --quiet 2>$null
    gcloud beta logging sinks create $script:secondSinkName $destinationTwo `
                                     --output-version-format=V2 --quiet 2>$null
    

    AfterAll {
        gcloud beta logging sinks delete $sinkName --quiet 2>$null
        gcloud beta logging sinks delete $secondSinkName --quiet 2>$null
    }

    It "should work without any parameters" {
        $sinks = Get-GcLogSink

        $firstSink = $sinks | Where-Object {$_.Name -eq $sinkName}
        $firstSink | Should Not BeNullOrEmpty
        $firstSink.Destination | Should BeExactly $destination
        $firstSink.OutputVersionFormat | Should BeExactly V1
        $firstSink.Filter | Should BeExactly $logFilter
        $firstSink.WriterIdentity | Should Not BeNullOrEmpty

        $secondSink = $sinks | Where-Object {$_.Name -eq $secondSinkName}
        $secondSink | Should Not BeNullOrEmpty
        $secondSink.Destination | Should BeExactly $destinationTwo
        $secondSink.OutputVersionFormat | Should BeExactly V2
        $secondSink.Filter | Should BeNullOrEmpty
        $secondSink.WriterIdentity | Should Not BeNullOrEmpty
    }

    It "should work with -Sink parameter" {
        $firstSink = Get-GcLogSink -Sink $sinkName
        $firstSink | Should Not BeNullOrEmpty
        $firstSink.Name | Should BeExactly "$sinkName"
        $firstSink.Destination | Should BeExactly $destination
        $firstSink.OutputVersionFormat | Should BeExactly V1
        $firstSink.Filter | Should BeExactly $logFilter
        $firstSink.WriterIdentity | Should Not BeNullOrEmpty
    }

    It "should work with an array of sinks names" {
        $sinks = Get-GcLogSink -Sink $sinkName, $secondSinkName
        $sinks.Count | Should Be 2

        $firstSink = $sinks | Where-Object {$_.Name -eq $sinkName}
        $firstSink | Should Not BeNullOrEmpty
        $firstSink.Destination | Should BeExactly $destination
        $firstSink.OutputVersionFormat | Should BeExactly V1
        $firstSink.Filter | Should BeExactly $logFilter
        $firstSink.WriterIdentity | Should Not BeNullOrEmpty

        $secondSink = $sinks | Where-Object {$_.Name -eq $secondSinkName}
        $secondSink | Should Not BeNullOrEmpty
        $secondSink.Destination | Should BeExactly $destinationTwo
        $secondSink.OutputVersionFormat | Should BeExactly V2
        $secondSink.Filter | Should BeNullOrEmpty
        $secondSink.WriterIdentity | Should Not BeNullOrEmpty    }

    It "should throw an error for non-existent sink" {
        { Get-GcLogSink -Sink "non-existent-sink-name" -ErrorAction Stop } | Should Throw "does not exist"
    }
}
