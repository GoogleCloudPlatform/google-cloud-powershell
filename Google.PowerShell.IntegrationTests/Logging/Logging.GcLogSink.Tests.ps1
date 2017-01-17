. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcLogSink" {
    $r = Get-Random
    $script:sinkName = "gcps-get-gclogsink-$r"
    $script:secondSinkName = "gcps-get-gclogsink2-$r"
    $destination = "storage.googleapis.com/random-destination-will-do-$r"
    $destinationTwo = "storage.googleapis.com/random-destination-will-do2-$r"
    $logFilter = "this is a filter"
    gcloud beta logging sinks create $script:sinkName $destination --log-filter=$logFilter --quiet 2>$null
    gcloud beta logging sinks create $script:secondSinkName $destinationTwo --output-version-format=V2 --quiet 2>$null
    

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

Describe "New-GcLogSink" {
    function Test-GcLogSink (
        [string]$name,
        [string]$destination,
        [string]$outputVersionFormat,
        [string]$writerIdentity,
        [DateTime]$startTime,
        [DateTime]$endTime,
        [Google.Apis.Logging.v2.Data.LogSink]$sink)
    {
        $sink | Should Not BeNullOrEmpty
        $sink.Name | Should BeExactly $name
        $sink.Destination | Should BeExactly $destination
        $sink.OutputVersionFormat | Should BeExactly $outputVersionFormat
        $sink.WriterIdentity | Should Not BeNullOrEmpty

        # Only checks for writer identity if it is provided.
        if (-not [string]::IsNullOrWhiteSpace($writerIdentity)) {
            $sink.WriterIdentity | Should BeExactly $writerIdentity
        }

        if ($null -eq $startTime) {
            $sink.StartTime | Should BeNullOrEmpty
        }
        else {
            [DateTime]$sink.StartTime | Should Be $startTime
        }

        if ($null -eq $endTime) {
            $sink.EndTime | Should BeNullOrEmpty
        }
        else {
            [DateTime]$sink.EndTime | Should Be $endTime
        }
    }

    $script:cloudLogServiceAccount = "serviceAccount:cloud-logs@system.gserviceaccount.com"
    It "should work with -GcsBucketDestination" {
        $r = Get-Random
        $bucket = "gcloud-powershell-testing-logsink-bucket-$r"
        $sinkName = "gcps-new-gclogsink-$r"
        try {
            New-GcLogSink $sinkName -GcsBucketDestination $bucket
            Start-Sleep -Seconds 1

            $createdSink = Get-GcLogSink -Sink $sinkName
            Test-GcLogSink -Name $sinkName `
                           -Destination "storage.googleapis.com/$bucket" `
                           -OutputVersionFormat "V2" `
                           -Sink $createdSink
        }
        finally {
            gcloud beta logging sinks delete $sinkName --quiet 2>$null
        }
    }

    It "should work with -BigQueryDataSetDestination" {
        $r = Get-Random
        $dataset = "gcloud_powershell_testing_dataset_$r"
        $sinkName = "gcps-new-gclogsink-$r"
        try {
            New-GcLogSink $sinkName -BigQueryDataSetDestination $dataset
            Start-Sleep -Seconds 1

            $createdSink = Get-GcLogSink -Sink $sinkName
            Test-GcLogSink -Name $sinkName `
                           -Destination "bigquery.googleapis.com/projects/$project/datasets/$dataset" `
                           -OutputVersionFormat "V2" `
                           -Sink $createdSink
        }
        finally {
            gcloud beta logging sinks delete $sinkName --quiet 2>$null
        }
    }

    It "should work with -PubSubTopicDestination" {
        $r = Get-Random
        $pubsubTopic = "gcloud-powershell-testing-pubsubtopic-$r"
        $sinkName = "gcps-new-gclogsink-$r"
        try {
            New-GcLogSink $sinkName -PubSubTopicDestination $pubsubTopic
            Start-Sleep -Seconds 1

            $createdSink = Get-GcLogSink -Sink $sinkName
            Test-GcLogSink -Name $sinkName `
                           -Destination "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic" `
                           -OutputVersionFormat "V2" `
                           -Sink $createdSink
        }
        finally {
            gcloud beta logging sinks delete $sinkName --quiet 2>$null
        }
    }

    It "should work with -OutputVersionFormat" {
        $r = Get-Random
        $bucket = "gcloud-powershell-testing-pubsubtopicv1-$r"
        $bucketTwo = "gcloud-powershell-testing-pubsubtopicv2-$r"
        $sinkName = "gcps-new-gclogsink-$r"
        $sinkNameTwo = "gcps-new-gclogsink2-$r"
        try {
            New-GcLogSink $sinkName -GcsBucketDestination $bucket -OutputVersionFormat V1
            # Have to be a different topic because sinks with diffferent output formats cannot share destination.
            New-GcLogSink $sinkNameTwo -GcsBucketDestination $bucketTwo -OutputVersionFormat V2
            Start-Sleep -Seconds 1

            $createdSink = Get-GcLogSink -Sink $sinkName
            Test-GcLogSink -Name $sinkName `
                           -Destination "storage.googleapis.com/$bucket" `
                           -OutputVersionFormat "V1" `
                           -Sink $createdSink

            $createdSink = Get-GcLogSink -Sink $sinkNameTwo
            Test-GcLogSink -Name $sinkNameTwo `
                           -Destination "storage.googleapis.com/$bucketTwo" `
                           -OutputVersionFormat "V2" `
                           -Sink $createdSink
        }
        finally {
            gcloud beta logging sinks delete $sinkName --quiet 2>$null
            gcloud beta logging sinks delete $sinkNameTwo --quiet 2>$null
        }
    }

    It "should work with -NoUniqueWriterIdentity" {
        $r = Get-Random
        $pubsubTopic = "gcloud-powershell-testing-pubsubtopic-$r"
        $sinkName = "gcps-new-gclogsink-$r"
        $sinkNameTwo = "gcps-new-gclogsink2-$r"
        try {
            New-GcLogSink $sinkName -PubSubTopicDestination $pubsubTopic -NoUniqueWriterIdentity
            New-GcLogSink $sinkNameTwo -PubSubTopicDestination $pubsubTopic
            Start-Sleep -Seconds 1

            $createdSink = Get-GcLogSink -Sink $sinkName
            Test-GcLogSink -Name $sinkName `
                           -Destination "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic" `
                           -OutputVersionFormat "V2" `
                           -WriterIdentity $script:cloudLogServiceAccount `
                           -Sink $createdSink

            $createdSink = Get-GcLogSink -Sink $sinkNameTwo
            $createdSink.WriterIdentity | Should Not Be $script:cloudLogServiceAccount
            Test-GcLogSink -Name $sinkNameTwo `
                           -Destination "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic" `
                           -OutputVersionFormat "V2" `
                           -Sink $createdSink
        }
        finally {
            gcloud beta logging sinks delete $sinkName --quiet 2>$null
            gcloud beta logging sinks delete $sinkNameTwo --quiet 2>$null
        }
    }


    It "should work with -Before and -After" {
        $r = Get-Random
        # Cloud-logs already has permission to write to these topics.
        $pubsubTopic = "gcloud-powershell-exported-log"
        $sinkName = "gcps-new-gclogsink-$r"
        $secondSinkName = "gcps-new-gclogsink2-$r"
        $thirdSinkName = "gcps-new-gclogsink3-$r"
        $logName = "gcps-new-gclogsink-log-$r"
        try {
            $firstTime = ([DateTime]::Now).AddMinutes(4)
            $secondTime = $firstTime.AddMinutes(4)
            New-GcLogSink $sinkName -PubSubTopicDestination $pubsubTopic -Before $firstTime
            New-GcLogSink $secondSinkName -PubSubTopicDestination $pubsubTopic -After $secondTime
            New-GcLogSink $thirdSinkName -PubSubTopicDestination $pubsubTopic -Before $secondTime -After $firstTime
            Start-Sleep -Seconds 1

            $createdSink = Get-GcLogSink -Sink $sinkName
            Test-GcLogSink -Name $sinkName `
                           -Destination "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic" `
                           -OutputVersionFormat "V2" `
                           -EndTime $firstTime `
                           -Sink $createdSink

            $createdSink = Get-GcLogSink -Sink $secondSinkName
            Test-GcLogSink -Name $secondSinkName `
                           -Destination "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic" `
                           -OutputVersionFormat "V2" `
                           -StartTime $secondTime `
                           -Sink $createdSink

            $createdSink = Get-GcLogSink -Sink $thirdSinkName
            Test-GcLogSink -Name $thirdSinkName `
                           -Destination "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic" `
                           -OutputVersionFormat "V2" `
                           -StartTime $firstTime `
                           -EndTime $secondTime `
                           -Sink $createdSink
        }
        finally {
            gcloud beta logging sinks delete $sinkName --quiet 2>$null
            gcloud beta logging sinks delete $secondSinkName --quiet 2>$null
            gcloud beta logging sinks delete $thirdSinkName --quiet 2>$null
        }
    }

    It "should work with -LogName" {
        $r = Get-Random
        # Cloud-logs already has permission to write to this topic.
        $pubsubTopic = "gcloud-powershell-exported-log"
        $sinkName = "gcps-new-gclogsink-$r"
        $logName = "gcps-new-gclogsink-log-$r"
        $secondLogName = "gcps-new-gclogsink-log2-$r"
        $subscriptionName = "gcps-new-gclogsink-subscription-$r"
        $textPayload = "This is a message with $r."
        try {
            New-GcLogSink $sinkName -PubSubTopicDestination $pubsubTopic -LogName $logName -NoUniqueWriterIdentity
            New-GcpsSubscription -Subscription $subscriptionName -Topic $pubsubTopic
            # We need to sleep before creating the log entry to account for the time the logsink
            # and the subscription is created.
            Start-Sleep -Seconds 20

            $createdSink = Get-GcLogSink -Sink $sinkName
            Test-GcLogSink -Name $sinkName `
                           -Destination "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic" `
                           -WriterIdentity $script:cloudLogServiceAccount `
                           -OutputVersionFormat "V2" `
                           -Sink $createdSink

            # Write a log entry to the log, we should be able to get it from the subscription since it will be exported to the topic.
            New-GcLogEntry -LogName $logName -TextPayload $textPayload
            # Write a different entry to a different log (we should not get this).
            New-GcLogEntry -LogName $secondLogName -TextPayload "You should not get this."
            # We need to sleep before getting the message to account for the delay before the log is exported to the topic.
            Start-Sleep -Seconds 20

            $message = Get-GcpsMessage -Subscription $subscriptionName -AutoAck
            $messageJson = ConvertFrom-Json $message.Data
            $messageJson.LogName | Should Match $logName
            $messageJson.TextPayload | Should BeExactly $textPayload
        }
        finally {
            gcloud beta logging sinks delete $sinkName --quiet 2>$null
            Remove-GcpsSubscription -Subscription $subscriptionName -ErrorAction SilentlyContinue
            Remove-GcLog $logName -ErrorAction SilentlyContinue
            Remove-GcLog $secondLogName -ErrorAction SilentlyContinue
        }
    }

    It "should work with -Filter" {
        $r = Get-Random
        # Cloud-logs already has permission to write to this topic.
        $pubsubTopic = "gcloud-powershell-exported-log"
        $sinkName = "gcps-new-gclogsink-$r"
        $logName = "gcps-new-gclogsink-log-$r"
        $subscriptionName = "gcps-new-gclogsink-subscription-$r"
        $textPayload = "This is a message with $r."
        $secondTextPayload = "This is the second text payload with $r."
        $filter = "textPayload=`"$secondTextPayload`""
        try {
            New-GcLogSink $sinkName -PubSubTopicDestination $pubsubTopic -LogName $logName -NoUniqueWriterIdentity -Filter $filter
            New-GcpsSubscription -Subscription $subscriptionName -Topic $pubsubTopic
            # We need to sleep before creating the log entry to account for the time the logsink
            # and the subscription is created.
            Start-Sleep -Seconds 20

            $createdSink = Get-GcLogSink -Sink $sinkName
            Test-GcLogSink -Name $sinkName `
                           -Destination "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic" `
                           -WriterIdentity $script:cloudLogServiceAccount `
                           -OutputVersionFormat "V2" `
                           -Sink $createdSink

            New-GcLogEntry -LogName $logName -TextPayload $textPayload
            New-GcLogEntry -LogName $logName -TextPayload $secondTextPayload
            # We need to sleep before getting the message to account for the delay before the log is exported to the topic.
            Start-Sleep -Seconds 20

            $message = Get-GcpsMessage -Subscription $subscriptionName -AutoAck
            $messageJson = ConvertFrom-Json $message.Data
            $messageJson.LogName | Should Match $logName
            $messageJson.TextPayload | Should BeExactly $secondTextPayload
        }
        finally {
            gcloud beta logging sinks delete $sinkName --quiet 2>$null
            Remove-GcpsSubscription -Subscription $subscriptionName -ErrorAction SilentlyContinue
            Remove-GcLog $logName -ErrorAction SilentlyContinue
        }
    }

    It "should work with -Severity" {
        $r = Get-Random
        # Cloud-logs already has permission to write to this topic.
        $pubsubTopic = "gcloud-powershell-exported-log"
        $sinkName = "gcps-new-gclogsink-$r"
        $logName = "gcps-new-gclogsink-log-$r"
        $subscriptionName = "gcps-new-gclogsink-subscription-$r"
        $debugPayload = "This is a message with severity debug."
        $infoPayload = "This is a message with severity info."
        $errorPayload = "This is a message with severity error."
        try {
            New-GcLogSink $sinkName -PubSubTopicDestination $pubsubTopic -LogName $logName -NoUniqueWriterIdentity -Severity Error
            New-GcpsSubscription -Subscription $subscriptionName -Topic $pubsubTopic
            # We need to sleep before creating the log entry to account for the time the logsink
            # and the subscription is created.
            Start-Sleep -Seconds 20

            $createdSink = Get-GcLogSink -Sink $sinkName
            Test-GcLogSink -Name $sinkName `
                           -Destination "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic" `
                           -WriterIdentity $script:cloudLogServiceAccount `
                           -OutputVersionFormat "V2" `
                           -Sink $createdSink

            New-GcLogEntry -LogName $logName -TextPayload $debugPayload -Severity Debug
            New-GcLogEntry -LogName $logName -TextPayload $infoPayload -Severity Info
            New-GcLogEntry -LogName $logName -TextPayload $errorPayload -Severity Error
            # We need to sleep before getting the message to account for the delay before the log is exported to the topic.
            Start-Sleep -Seconds 20

            $message = Get-GcpsMessage -Subscription $subscriptionName -AutoAck
            $messageJson = ConvertFrom-Json $message.Data
            $messageJson.LogName | Should Match $logName
            $messageJson.TextPayload | Should BeExactly $errorPayload
            $messageJson.Severity | Should BeExactly ERROR
        }
        finally {
            gcloud beta logging sinks delete $sinkName --quiet 2>$null
            Remove-GcpsSubscription -Subscription $subscriptionName -ErrorAction SilentlyContinue
            Remove-GcLog $logName -ErrorAction SilentlyContinue
        }
    }

    It "should throw an error for creating a sink that already exists." {
        $r = Get-Random
        $bucket = "gcloud-powershell-testing-logsink-bucket-$r"
        $sinkName = "gcps-new-gclogsink-$r"
        try {
            New-GcLogSink $sinkName -GcsBucketDestination $bucket
            Start-Sleep -Seconds 1

            $createdSink = Get-GcLogSink -Sink $sinkName
            $createdSink | Should Not BeNullOrEmpty

            { New-GcLogSink $sinkName -GcsBucketDestination $bucket -ErrorAction Stop }  | Should Throw "already exists"
        }
        finally {
            gcloud beta logging sinks delete $sinkName --quiet 2>$null
        }
    }
}
