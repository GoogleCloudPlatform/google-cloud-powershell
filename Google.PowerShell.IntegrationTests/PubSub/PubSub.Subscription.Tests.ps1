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

            $subscription = GetSubScription -Subscription $subscriptionName -Topic $topicName
            $subscription | Should Not BeNullOrEmpty
            $subscription.AckDeadlineSeconds | Should Be 10
            $subscription.PushConfig.PushEndpoint | Should BeNullOrEmpty
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
        }
    }

    It "should work with topic object" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"

        try {
            $topicObject = New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicObject

            $subscription = GetSubScription -Subscription $subscriptionName -Topic $topicName
            $subscription | Should Not BeNullOrEmpty
            $subscription.AckDeadlineSeconds | Should Be 10
            $subscription.PushConfig.PushEndpoint | Should BeNullOrEmpty
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

            $subscription = GetSubScription -Subscription $subscriptionName -Topic $topicName
            $subscription | Should Not BeNullOrEmpty
            $subscription.AckDeadlineSeconds | Should Be 20
            $subscription.PushConfig.PushEndpoint | Should BeNullOrEmpty
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

            $subscription = GetSubScription -Subscription $subscriptionName -Topic $topicName
            $subscription | Should Not BeNullOrEmpty
            $subscription.AckDeadlineSeconds | Should Be 10
            $subscription.PushConfig.PushEndpoint | Should Be $endpoint
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
            $topicObject = New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName2 -Topic $topicName
            $topicObject2 = New-GcpsTopic -Topic $topicName2
            New-GcpsSubscription -Subscription $subscriptionName3 -Topic $topicName2
            New-GcpsSubscription -Subscription $subscriptionName4 -Topic $topicName2


            $subscriptions = Get-GcpsSubscription -Topic $topicName
            $subscriptions.Count | Should Be 2
            $subscriptions | Where-Object {$_.Name -like "*$subscriptionName*"} | Should Not BeNullOrEmpty
            $subscriptions | Where-Object {$_.Name -like "*$subscriptionName2*"} | Should Not BeNullOrEmpty

            # Should also work if we give a topic object.
            $subscriptions = Get-GcpsSubscription -Topic $topicObject
            $subscriptions.Count | Should Be 2
            $subscriptions | Where-Object {$_.Name -like "*$subscriptionName*"} | Should Not BeNullOrEmpty
            $subscriptions | Where-Object {$_.Name -like "*$subscriptionName2*"} | Should Not BeNullOrEmpty

            $subscriptions2 = Get-GcpsSubscription -Topic $topicName2
            $subscriptions2.Count | Should Be 2
            $subscriptions2 | Where-Object {$_.Name -like "*$subscriptionName3*"} | Should Not BeNullOrEmpty
            $subscriptions2 | Where-Object {$_.Name -like "*$subscriptionName4*"} | Should Not BeNullOrEmpty

            # Should also work if we give a topic object.
            $subscriptions2 = Get-GcpsSubscription -Topic $topicObject2
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
            Start-Sleep -Seconds 5

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

Describe "Set-GcpsSubscriptionConfig" {
    # The endpoint to be used for Push Config. We get this endpoint when we set up an app.
    $endpoint = "https://gcloud-powershell-testing.appspot.com/_ah/push-handlers/"

    It "should work with -PullConfig" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName `
                                 -Topic $topicName `
                                 -PushEndpoint $endpoint

            Set-GcpsSubscriptionConfig -Subscription $subscriptionName -PullConfig

            $subscription = Get-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $subscription.PushConfig.PushEndpoint | Should BeNullOrEmpty

            # Checks that if we call the same cmdlet a second time, the result will not be changed
            # and no error will be thrown.
            Set-GcpsSubscriptionConfig -Subscription $subscriptionName -PullConfig

            $subscription = Get-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $subscription.PushConfig.PushEndpoint | Should BeNullOrEmpty
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

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            Set-GcpsSubscriptionConfig -Subscription $subscriptionName -PushEndpoint $endpoint

            $subscription = Get-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $subscription.PushConfig.PushEndpoint | Should Be $endpoint

            # Checks that if we call the same cmdlet a second time, the result will not be changed
            # and no error will be thrown.
            Set-GcpsSubscriptionConfig -Subscription $subscriptionName -PushEndpoint $endpoint

            $subscription = Get-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $subscription.PushConfig.PushEndpoint | Should Be $endpoint
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
        }
    }

    It "should work with subscription objects" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName `
                                 -Topic $topicName `
                                 -PushEndpoint $endpoint

            Start-Sleep -Seconds 5

            $subscription = Get-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            Set-GcpsSubscriptionConfig -Subscription $subscription -PullConfig

            $subscription = Get-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $subscription.PushConfig.PushEndpoint | Should BeNullOrEmpty
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
        }
    }

    It "should work with pipelining" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"
        $subscriptionName2 = "gcp-test-new-subscription2-$r"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName `
                                 -Topic $topicName `
                                 -PushEndpoint $endpoint
            New-GcpsSubscription -Subscription $subscriptionName2 `
                                 -Topic $topicName `
                                 -PushEndpoint $endpoint
            
            # Set all subscriptions of topic $topicName to pull config.
            Get-GcpsSubscription -Topic $topicName | Set-GcpsSubscriptionConfig -PullConfig

            Start-Sleep -Seconds 5

            $subscriptions = Get-GcpsSubscription -Topic $topicName
            $subscriptions | ForEach-Object { $_.PushConfig.PushEndpoint | Should BeNullOrEmpty }
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName2 --quiet 2>$null
        }
    }

    It "should error out if subscription does not exist" {
        $subscriptionName = "non-existent-gcps-subscription"
        { Set-GcpsSubscriptionConfig -Subscription $subscriptionName -PullConfig -ErrorAction Stop } |
            Should Throw "Subscription 'projects/$project/subscriptions/$subscriptionName' does not exist"
    }

    It "should error out for bad subscription name" {
        { Set-GcpsSubscriptionConfig -Subscription "!!" -PullConfig -ErrorAction Stop } | Should Throw "Invalid resource name"
    }

    It "should error out for invalid endpoint" {
        $r = Get-Random
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"
        $invalidEndpoint = "http://www.example.com"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            { Set-GcpsSubscriptionConfig -Subscription $subscriptionName `
                                 -PushEndpoint $invalidEndpoint `
                                 -ErrorAction Stop } |
                                 Should Throw "Invalid push endpoint given"
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub subscriptions delete $subscriptionName --quiet 2>$null
        }
    }
}

Describe "Remove-GcpsSubscription" {
    It "should work" {
        $r = Get-Random
        $topic = "gcp-test-remove-subscription-topic-$r"
        $subscription = "gcp-test-remove-subscription-$r"
        $subscriptionTwo = "gcp-test-remove-subscription-two-$r"
        $subscriptionThree = "gcp-test-remove-subscription-three-$r"

        New-GcpsTopic -Topic $topic
        New-GcpsSubscription -Topic $topic -Subscription $subscription
        New-GcpsSubscription -Topic $topic -Subscription $subscriptionTwo
        New-GcpsSubscription -Topic $topic -Subscription $subscriptionThree

        try {
            (Get-GcpsSubscription -Subscription $subscription, $subscriptionTwo, $subscriptionThree).Count | Should Be 3

            # Remove a single subscription.
            Remove-GcpsSubscription -Subscription $subscription
            { Get-GcpsSubscription -Subscription $subscription -ErrorAction Stop } | Should Throw "does not exist"

            # Remove an array of subscriptions.
            Remove-GcpsSubscription -Subscription $subscriptionTwo, $subscriptionThree
            { Get-GcpsSubscription -Subscription $subscriptionTwo, $subscriptionThree -ErrorAction Stop } | Should Throw "does not exist"
        }
        finally {
            gcloud beta pubsub topics delete $topic --quiet 2>$null
        }
    }

    It "should work with pipeline" {
        $r = Get-Random
        $topic = "gcp-test-remove-subscription-topic-$r"
        $subscription = "gcp-test-remove-subscription-$r"

        New-GcpsTopic -Topic $topic
        New-GcpsSubscription -Topic $topic -Subscription $subscription

        try {
            Get-GcpsSubscription -Subscription $subscription | Should Not BeNullOrEmpty

            # Remove through pipeline
            Get-GcpsSubscription -Subscription $subscription | Remove-GcpsSubscription
            { Get-GcpsSubscription -Subscription $subscription -ErrorAction Stop } | Should Throw "does not exist"
        }
        finally {
            gcloud beta pubsub topics delete $topic --quiet 2>$null
        }
    }

    It "should work with subscription object" {
        $r = Get-Random
        $topic = "gcp-test-remove-subscription-topic-$r"
        $subscription = "gcp-test-remove-subscription-$r"

        New-GcpsTopic -Topic $topic
        New-GcpsSubscription -Topic $topic -Subscription $subscription

        try {
            $subscriptionObject = Get-GcpsSubscription -Subscription $subscription
            $subscription | Should Not BeNullOrEmpty

            Remove-GcpsSubscription -Subscription $subscriptionObject
            { Get-GcpsSubscription -Subscription $subscription -ErrorAction Stop } | Should Throw "does not exist"
        }
        finally {
            gcloud beta pubsub topics delete $topic --quiet 2>$null
        }
    }

    It "should throw error for non-existent subscription" {
        { Remove-GcpsSubscription -Subscription "non-existent-topic-powershell-testing" -ErrorAction Stop } | Should Throw "does not exist"
    }

    It "should throw error for invalid subscription name" {
        { Remove-GcpsSubscription -Subscription "!!" -ErrorAction Stop } | Should Throw "Invalid resource name"
    }

    It "should not remove subscription if -WhatIf is used" {
        $r = Get-Random
        $topic = "gcp-test-remove-subscription-topic-$r"
        $subscription = "gcp-test-remove-subscription-$r"

        New-GcpsTopic -Topic $topic
        New-GcpsSubscription -Topic $topic -Subscription $subscription

        try {
            Get-GcpsSubscription -Subscription $subscription | Should Not BeNullOrEmpty

            # Subscription should not be removed.
            Remove-GcpsSubscription -Subscription $subscription -WhatIf
            Get-GcpsSubscription -Subscription $subscription | Should Not BeNullOrEmpty

            Remove-GcpsSubscription -Subscription $subscription
        }
        finally {
            gcloud beta pubsub topics delete $topic --quiet 2>$null
        }
    }
}
