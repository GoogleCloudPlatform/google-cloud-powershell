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

Describe "New-GcpsLogSink" {
    It "should work with -GcsBucketDestination" {
        $r = Get-Random
        $bucket = "gcloud-powershell-testing-logsink-bucket-$r"
        $sinkName = "gcps-new-gclogsink-$r"
        try {
            New-GcLogSink $sinkName -GcsBucketDestination $bucket
            Start-Sleep -Seconds 1

            $createdSink = Get-GcLogSink -Sink $sinkName
            $createdSink | Should Not BeNullOrEmpty
            $createdSink.Name | Should BeExactly $sinkName
            $createdSink.Destination | Should BeExactly "storage.googleapis.com/$bucket"
            $createdSink.OutputVersionFormat | Should BeExactly V2
            $createdSink.WriterIdentity | Should Not BeNullOrEmpty
            $createdSink.Filter | Should BeNullOrEmpty
            $createdSink.StartTime | Should BeNullOrEmpty
            $createdSink.EndTime | Should BeNullOrEmpty
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
            $createdSink | Should Not BeNullOrEmpty
            $createdSink.Name | Should BeExactly $sinkName
            $createdSink.Destination | Should BeExactly "bigquery.googleapis.com/projects/$project/datasets/$dataset"
            $createdSink.OutputVersionFormat | Should BeExactly V2
            $createdSink.WriterIdentity | Should Not BeNullOrEmpty
            $createdSink.Filter | Should BeNullOrEmpty
            $createdSink.StartTime | Should BeNullOrEmpty
            $createdSink.EndTime | Should BeNullOrEmpty
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
            $createdSink | Should Not BeNullOrEmpty
            $createdSink.Name | Should BeExactly $sinkName
            $createdSink.Destination | Should BeExactly "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic"
            $createdSink.OutputVersionFormat | Should BeExactly V2
            $createdSink.WriterIdentity | Should Not BeNullOrEmpty
            $createdSink.Filter | Should BeNullOrEmpty
            $createdSink.StartTime | Should BeNullOrEmpty
            $createdSink.EndTime | Should BeNullOrEmpty
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
            $createdSink | Should Not BeNullOrEmpty
            $createdSink.Name | Should BeExactly $sinkName
            $createdSink.Destination | Should BeExactly "storage.googleapis.com/$bucket"
            $createdSink.OutputVersionFormat | Should BeExactly V1
            $createdSink.WriterIdentity | Should Not BeNullOrEmpty
            $createdSink.Filter | Should BeNullOrEmpty
            $createdSink.StartTime | Should BeNullOrEmpty
            $createdSink.EndTime | Should BeNullOrEmpty

            $createdSink = Get-GcLogSink -Sink $sinkNameTwo
            $createdSink | Should Not BeNullOrEmpty
            $createdSink.Name | Should BeExactly $sinkNameTwo
            $createdSink.Destination | Should BeExactly "storage.googleapis.com/$bucketTwo"
            $createdSink.OutputVersionFormat | Should BeExactly V2
            $createdSink.WriterIdentity | Should Not BeNullOrEmpty
            $createdSink.Filter | Should BeNullOrEmpty
            $createdSink.StartTime | Should BeNullOrEmpty
            $createdSink.EndTime | Should BeNullOrEmpty
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
            $createdSink | Should Not BeNullOrEmpty
            $createdSink.Name | Should BeExactly $sinkName
            $createdSink.Destination | Should BeExactly "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic"
            $createdSink.OutputVersionFormat | Should BeExactly V2
            $createdSink.WriterIdentity | Should BeExactly "serviceAccount:cloud-logs@system.gserviceaccount.com"
            $createdSink.Filter | Should BeNullOrEmpty
            $createdSink.StartTime | Should BeNullOrEmpty
            $createdSink.EndTime | Should BeNullOrEmpty

            $createdSink = Get-GcLogSink -Sink $sinkNameTwo
            $createdSink | Should Not BeNullOrEmpty
            $createdSink.Name | Should BeExactly $sinkNameTwo
            $createdSink.Destination | Should BeExactly "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic"
            $createdSink.OutputVersionFormat | Should BeExactly V2
            $createdSink.WriterIdentity | Should Not Be "serviceAccount:cloud-logs@system.gserviceaccount.com"
            $createdSink.Filter | Should BeNullOrEmpty
            $createdSink.StartTime | Should BeNullOrEmpty
            $createdSink.EndTime | Should BeNullOrEmpty
        }
        finally {
            gcloud beta logging sinks delete $sinkName --quiet 2>$null
            gcloud beta logging sinks delete $sinkNameTwo --quiet 2>$null
        }
    }


    It "should work with -Before and -After" {
        $r = Get-Random
        # These topics already has permission for cloud-logs to write to.
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
            $createdSink | Should Not BeNullOrEmpty
            $createdSink.Name | Should BeExactly $sinkName
            $createdSink.Destination | Should BeExactly "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic"
            $createdSink.OutputVersionFormat | Should BeExactly V2
            $createdSink.WriterIdentity | Should Not BeNullOrEmpty
            $createdSink.Filter | Should BeNullOrEmpty
            $createdSink.StartTime | Should BeNullOrEmpty
            [datetime]$createdSink.EndTime -eq $firstTime | Should Be $true

            $createdSink = Get-GcLogSink -Sink $secondSinkName
            $createdSink | Should Not BeNullOrEmpty
            $createdSink.Name | Should BeExactly $secondSinkName
            $createdSink.Destination | Should BeExactly "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic"
            $createdSink.OutputVersionFormat | Should BeExactly V2
            $createdSink.WriterIdentity | Should Not BeNullOrEmpty
            $createdSink.Filter | Should BeNullOrEmpty
            [datetime]$createdSink.StartTime -eq $secondTime | Should Be $true
            $createdSink.EndTime | Should BeNullOrEmpty

            $createdSink = Get-GcLogSink -Sink $thirdSinkName
            $createdSink | Should Not BeNullOrEmpty
            $createdSink.Name | Should BeExactly $thirdSinkName
            $createdSink.Destination | Should BeExactly "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic"
            $createdSink.OutputVersionFormat | Should BeExactly V2
            $createdSink.WriterIdentity | Should Not BeNullOrEmpty
            $createdSink.Filter | Should BeNullOrEmpty
            [datetime]$createdSink.StartTime -eq $firstTime | Should Be $true
            [datetime]$createdSink.EndTime -eq $secondTime | Should Be $true
        }
        finally {
            gcloud beta logging sinks delete $sinkName --quiet 2>$null
            gcloud beta logging sinks delete $secondSinkName --quiet 2>$null
            gcloud beta logging sinks delete $thirdSinkName --quiet 2>$null
        }
    }

    It "should work with -LogName" {
        $r = Get-Random
        # This topic already has permission for cloud-logs to write to.
        $pubsubTopic = "gcloud-powershell-exported-log"
        $sinkName = "gcps-new-gclogsink-$r"
        $logName = "gcps-new-gclogsink-log-$r"
        $secondLogName = "gcps-new-gclogsink-log2-$r"
        $subscriptionName = "gcps-new-gclogsink-subscription-$r"
        $textPayload = "This is a message with $r."
        try {
            New-GcLogSink $sinkName -PubSubTopicDestination $pubsubTopic -LogName $logName -NoUniqueWriterIdentity
            New-GcpsSubscription -Subscription $subscriptionName -Topic $pubsubTopic
            Start-Sleep -Seconds 20

            $createdSink = Get-GcLogSink -Sink $sinkName
            $createdSink | Should Not BeNullOrEmpty
            $createdSink.Name | Should BeExactly $sinkName
            $createdSink.Destination | Should BeExactly "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic"
            $createdSink.OutputVersionFormat | Should BeExactly V2
            $createdSink.WriterIdentity | Should BeExactly "serviceAccount:cloud-logs@system.gserviceaccount.com"
            $createdSink.Filter | Should Not BeNullOrEmpty
            $createdSink.StartTime | Should BeNullOrEmpty
            $createdSink.EndTime | Should BeNullOrEmpty

            # Write a log entry to the log, we should be able to get it from the subscription since it will be exported to the topic.
            New-GcLogEntry -LogName $logName -TextPayload $textPayload
            # Write a different entry to a different log (we should not get this).
            New-GcLogEntry -LogName $secondLogName -TextPayload "You should not get this."
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
        # This topic already has permission for cloud-logs to write to.
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
            Start-Sleep -Seconds 20

            $createdSink = Get-GcLogSink -Sink $sinkName
            $createdSink | Should Not BeNullOrEmpty
            $createdSink.Name | Should BeExactly $sinkName
            $createdSink.Destination | Should BeExactly "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic"
            $createdSink.OutputVersionFormat | Should BeExactly V2
            $createdSink.WriterIdentity | Should BeExactly "serviceAccount:cloud-logs@system.gserviceaccount.com"
            $createdSink.Filter | Should Match $filter
            $createdSink.StartTime | Should BeNullOrEmpty
            $createdSink.EndTime | Should BeNullOrEmpty

            New-GcLogEntry -LogName $logName -TextPayload $textPayload
            New-GcLogEntry -LogName $logName -TextPayload $secondTextPayload
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
        # This topic already has permission for cloud-logs to write to.
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
            Start-Sleep -Seconds 20

            $createdSink = Get-GcLogSink -Sink $sinkName
            $createdSink | Should Not BeNullOrEmpty
            $createdSink.Name | Should BeExactly $sinkName
            $createdSink.Destination | Should BeExactly "pubsub.googleapis.com/projects/$project/topics/$pubsubTopic"
            $createdSink.OutputVersionFormat | Should BeExactly V2
            $createdSink.WriterIdentity | Should BeExactly "serviceAccount:cloud-logs@system.gserviceaccount.com"
            $createdSink.Filter | Should Not BeNullOrEmpty
            $createdSink.StartTime | Should BeNullOrEmpty
            $createdSink.EndTime | Should BeNullOrEmpty

            New-GcLogEntry -LogName $logName -TextPayload $debugPayload -Severity Debug
            New-GcLogEntry -LogName $logName -TextPayload $infoPayload -Severity Info
            New-GcLogEntry -LogName $logName -TextPayload $errorPayload -Severity Error
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
