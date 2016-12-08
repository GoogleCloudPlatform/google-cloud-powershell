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

Describe "New-GcpsTopic" {
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
