. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

function GetSubscription($subscription, $topic)
{
    $subscriptionName = "projects/$project/subscriptions/$subscription"
    $subscriptionsJson = [string](gcloud beta pubsub subscriptions list --quiet 2>$null --format=json)
    $subscriptions = ConvertFrom-Json $subscriptionsJson
    $subscriptions | Where-Object {$_.Name -eq "$subscriptionName"}
}

Describe "New-GcpsSubscription" {
    It "should work" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            $subscription = GetSubScription -subscription $subscriptionName -topic $topicName
            $subscription | Should Not BeNullOrEmpty
            $subscription.AckDeadlineSeconds | Should Be 10
            $subscription.PushConfig.PushEndPoint | Should BeNullOrEmpty
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
        }
    }

    It "should work with -AckDeadline" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName -AckDeadline 20

            $subscription = GetSubScription -subscription $subscriptionName -topic $topicName
            $subscription | Should Not BeNullOrEmpty
            $subscription.AckDeadlineSeconds | Should Be 20
            $subscription.PushConfig.PushEndPoint | Should BeNullOrEmpty
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
        }
    }

    It "should work with -PushEndpoint" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"
        # We created an app and this endpoint is set up for us.
        $endpoint = "https://gcloud-powershell-testing.appspot.com/_ah/push-handlers/"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName `
                                 -Topic $topicName `
                                 -PushEndpoint $endpoint

            $subscription = GetSubScription -subscription $subscriptionName -topic $topicName
            $subscription | Should Not BeNullOrEmpty
            $subscription.AckDeadlineSeconds | Should Be 10
            $subscription.PushConfig.PushEndPoint | Should Be $endpoint
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
        }
    }

    It "should error out for bad subscription name" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"

        try {
            New-GcpsTopic -Topic $topicName
            { New-GcpsSubscription -Topic $topicName -Subscription "!!" -ErrorAction Stop } | Should Throw "Invalid resource name"
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
        }
    }

    It "should error out for bad ack deadline" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"

        try {
            New-GcpsTopic -Topic $topicName
            # Ack deadline has to be between 10 and 600.
            { New-GcpsSubscription -Topic $topicName -Subscription $subscriptionName -AckDeadline -30 -ErrorAction Stop } |
                Should Throw "Invalid ack deadline given"
            { New-GcpsSubscription -Topic $topicName -Subscription $subscriptionName -AckDeadline 5 -ErrorAction Stop } |
                Should Throw "Invalid ack deadline given"
            { New-GcpsSubscription -Topic $topicName -Subscription $subscriptionName -AckDeadline 1000 -ErrorAction Stop } |
                Should Throw "Invalid ack deadline given"
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
        }
    }

    It "should error out if topic does not exist" {
        $r = Get-Random
        $topicName = "gcloud-powershell-non-existent-topic"
        $subscriptionName = "gcp-test-new-subscription-$r"

        try {
            { New-GcpsSubscription -Topic $topicName -Subscription $subscriptionName -ErrorAction Stop } |
                Should Throw "Topic 'projects/$project/topics/$topicName' does not exist"
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
        }
    }

    It "should error out if subscription already exists" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            { New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName -ErrorAction Stop } |
                Should Throw "it already exists"
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
        }
    }

    It "should error out for invalid endpoint" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"
        $endpoint = "http://www.example.com"

        try {
            New-GcpsTopic -Topic $topicName
            { New-GcpsSubscription -Subscription $subscriptionName `
                                 -Topic $topicName `
                                 -PushEndpoint $endpoint `
                                 -ErrorAction Stop } |
                                 Should Throw "Invalid push endpoint given"
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
        }
    }
}

Describe "Get-GcpsSubscription" {
    It "should work without any parameters" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"
        $subscriptionName2 = "gcp-test-new-subscription2-$r"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName2 -Topic $topicName

            $subscriptions = Get-GcpsSubscription
            $subscriptions | Where-Object {$_.Name -like "*$subscriptionName*"} | Should Not BeNullOrEmpty
            $subscriptions | Where-Object {$_.Name -like "*$subscriptionName2*"} | Should Not BeNullOrEmpty
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName2 --quiet 2>$null
        }
    }

    It "should work with -Topic" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"
        $subscriptionName2 = "gcp-test-new-subscription2-$r"
        $topicName2 = "gcp-test-new-subscription-topic2-$r"
        $subscriptionName3 = "gcp-test-new-subscription3-$r"
        $subscriptionName4 = "gcp-test-new-subscription4-$r"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName2 -Topic $topicName
            New-GcpsTopic -Topic $topicName2
            New-GcpsSubscription -Subscription $subscriptionName3 -Topic $topicName2
            New-GcpsSubscription -Subscription $subscriptionName4 -Topic $topicName2


            $subscriptions = Get-GcpsSubscription -Topic $topicName
            $subscriptions.Count | Should Be 2
            $subscriptions | Where-Object {$_.Name -like "*$subscriptionName*"} | Should Not BeNullOrEmpty
            $subscriptions | Where-Object {$_.Name -like "*$subscriptionName2*"} | Should Not BeNullOrEmpty

            $subscriptions2 = Get-GcpsSubscription -Topic $topicName2
            $subscriptions2.Count | Should Be 2
            $subscriptions2 | Where-Object {$_.Name -like "*$subscriptionName3*"} | Should Not BeNullOrEmpty
            $subscriptions2 | Where-Object {$_.Name -like "*$subscriptionName4*"} | Should Not BeNullOrEmpty
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName2 --quiet 2>$null
            gcloud beta pubsub topics delete $topicName2 --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName3 --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName4 --quiet 2>$null
        }
    }

    It "should work with -Subscription" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"
        $subscriptionName2 = "gcp-test-new-subscription2-$r"
        $subscriptionName3 = "gcp-test-new-subscription3-$r"
        $subscriptionArray = @($subscriptionName, $subscriptionName2, $subscriptionName3)

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName2 -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName3 -Topic $topicName

            $subscriptions = Get-GcpsSubscription -Subscription $subscriptionName
            $subscriptions.Name | Should Match $subscriptionName

            $subscriptions2 = Get-GcpsSubscription -Subscription $subscriptionArray
            $subscriptions2.Count | Should Be 3
            $subscriptions2 | Where-Object {$_.Name -like "*$subscriptionName*"} | Should Not BeNullOrEmpty
            $subscriptions2 | Where-Object {$_.Name -like "*$subscriptionName2*"} | Should Not BeNullOrEmpty
            $subscriptions2 | Where-Object {$_.Name -like "*$subscriptionName3*"} | Should Not BeNullOrEmpty
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName2 --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName3 --quiet 2>$null
        }
    }

    It "should work with -Subscription and -Topic" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"
        $subscriptionName2 = "gcp-test-new-subscription2-$r"
        $subscriptionName3 = "gcp-test-new-subscription3-$r"
        $subscriptionArray = @($subscriptionName, $subscriptionName2, $subscriptionName3)
        $topicName2 = "gcp-test-new-subscription-topic2-$r"
        $subscriptionName4 = "gcp-test-new-subscription4-$r"
        $subscriptionName5 = "gcp-test-new-subscription5-$r"
        $subscriptionName6 = "gcp-test-new-subscription6-$r"
        $subscriptionArray2 = @($subscriptionName4, $subscriptionName5, $subscriptionName6)

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName2 -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName3 -Topic $topicName
            New-GcpsTopic -Topic $topicName2
            New-GcpsSubscription -Subscription $subscriptionName4 -Topic $topicName2
            New-GcpsSubscription -Subscription $subscriptionName5 -Topic $topicName2
            New-GcpsSubscription -Subscription $subscriptionName6 -Topic $topicName2

            $subscriptions = Get-GcpsSubscription -Topic $topicName -Subscription $subscriptionName
            $subscriptions.Name | Should Match $subscriptionName

            $subscriptions2 = Get-GcpsSubscription -Topic $topicName2 -Subscription $subscriptionName6
            $subscriptions2.Name | Should Match $subscriptionName6

            $subscriptions3 = Get-GcpsSubscription -Topic $topicName -Subscription $subscriptionArray
            $subscriptions3.Count | Should Be 3
            $subscriptions3 | Where-Object {$_.Name -like "*$subscriptionName*"} | Should Not BeNullOrEmpty
            $subscriptions3 | Where-Object {$_.Name -like "*$subscriptionName2*"} | Should Not BeNullOrEmpty
            $subscriptions3 | Where-Object {$_.Name -like "*$subscriptionName3*"} | Should Not BeNullOrEmpty

            $subscriptions3 = Get-GcpsSubscription -Topic $topicName2 -Subscription $subscriptionArray2
            $subscriptions3.Count | Should Be 3
            $subscriptions3 | Where-Object {$_.Name -like "*$subscriptionName4*"} | Should Not BeNullOrEmpty
            $subscriptions3 | Where-Object {$_.Name -like "*$subscriptionName5*"} | Should Not BeNullOrEmpty
            $subscriptions3 | Where-Object {$_.Name -like "*$subscriptionName6*"} | Should Not BeNullOrEmpty
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName2 --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName3 --quiet 2>$null
            gcloud beta pubsub topics delete $topicName2 --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName4 --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName5 --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName6 --quiet 2>$null
        }
    }

    It "should error out if topic does not exist" {
        $topicName = "non-existent-gcps-topic"
        { Get-GcpsSubscription -Topic $topicName -ErrorAction Stop } |
            Should Throw "Topic 'projects/$project/topics/$topicName' does not exist"
    }

    It "should error out if subscription does not exist" {
        $subscriptionName = "non-existent-gcps-subscription"
        { Get-GcpsSubscription -Subscription $subscriptionName -ErrorAction Stop } |
            Should Throw "Subscription 'projects/$project/subscriptions/$subscriptionName' does not exist"
    }

    It "should error out for bad topic name" {
        { Get-GcpsSubscription -Topic "!!" -ErrorAction Stop } | Should Throw "Invalid resource name"
    }

    It "should error out for bad subscription name" {
        { Get-GcpsSubscription -Subscription "!!" -ErrorAction Stop } | Should Throw "Invalid resource name"
    }
}
