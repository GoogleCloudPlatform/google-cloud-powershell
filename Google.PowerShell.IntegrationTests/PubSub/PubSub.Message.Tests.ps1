. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

# Returns all available messages from the subscription (auto-acknowledge all of them in the process).
function Get-SubscriptionMessage($subscription)
{
    while ($true) {
        $messageString = [string](gcloud beta pubsub subscriptions pull $subscription --format=json --auto-ack 2>$null)
        $messageObject = ConvertFrom-Json $messageString
        # Output the messages to the pipeline if we get any, else we are done.
        if ($messageObject.Count -ne 0) {
            $messageObject
        }
        else {
            return
        }
    }
}

function ConvertFrom-Base64String($base64String)
{
    return [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($base64String))
}

Describe "New-GcpsMessage" {
    It "should work with -Data" {
        $testData = "Test Data"
        $gcpsMessage = New-GcpsMessage -Data $testData
        $gcpsMessage.Data | Should BeExactly $testData
    }

    It "should work with -Attributes" {
        $attributes = @{"Key" = "Value"; "Key2" = "Value2"}
        $gcpsMessage = New-GcpsMessage -Attributes $attributes
        $gcpsMessage.Attributes["Key"] | Should BeExactly "Value"
        $gcpsMessage.Attributes["Key2"] | Should BeExactly "Value2"
    }

    It "should work with both -Data and -Attributes" {
        $testData = "Test Data"
        $attributes = @{"Key" = "Value"; "Key2" = "Value2"}
        $gcpsMessage = New-GcpsMessage -Data $testData -Attributes $attributes
        $gcpsMessage.Data | Should BeExactly $testData
        $gcpsMessage.Attributes["Key"] | Should BeExactly "Value"
        $gcpsMessage.Attributes["Key2"] | Should BeExactly "Value2"
    }

    It "should throw error if there are no data or attributes" {
        { New-GcpsMessage } | Should Throw "Cannot construct a PubSub message"
    }
}

Describe "Publish-GcpsMessage" {
    It "should work with -Data" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicName
            $publishedMessage.MessageId | Should Not BeNullOrEmpty

            $subscriptionMessage = Get-SubscriptionMessage -Subscription $subscriptionName

            $subscriptionMessage.Message.MessageId | Should BeExactly $publishedMessage.MessageId
            (ConvertFrom-Base64String $subscriptionMessage.Message.Data) | Should BeExactly $testData
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with topic object" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"

        try {
            $topicObject = New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicObject
            $publishedMessage.MessageId | Should Not BeNullOrEmpty

            $subscriptionMessage = Get-SubscriptionMessage -Subscription $subscriptionName

            $subscriptionMessage.Message.MessageId | Should BeExactly $publishedMessage.MessageId
            (ConvertFrom-Base64String $subscriptionMessage.Message.Data) | Should BeExactly $testData
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with -Attributes" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $attributes = @{"Key" = "Value"; "Key2" = "Value2"}

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            $publishedMessage = Publish-GcpsMessage -Attributes $attributes -Topic $topicName
            $publishedMessage.MessageId | Should Not BeNullOrEmpty

            $subscriptionMessage = Get-SubscriptionMessage -Subscription $subscriptionName

            $subscriptionMessage.Message.MessageId | Should BeExactly $publishedMessage.MessageId
            $subscriptionMessage.Message.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessage.Message.Attributes.Key2 | Should BeExactly "Value2"
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with -Data and -Attributes" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"
        $attributes = @{"Key" = "Value"; "Key2" = "Value2"}

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            $publishedMessage = Publish-GcpsMessage -Data $testData -Attributes $attributes -Topic $topicName
            $publishedMessage.MessageId | Should Not BeNullOrEmpty

            $subscriptionMessage = Get-SubscriptionMessage -Subscription $subscriptionName

            $subscriptionMessage.Message.MessageId | Should BeExactly $publishedMessage.MessageId
            (ConvertFrom-Base64String $subscriptionMessage.Message.Data) | Should BeExactly $testData
            $subscriptionMessage.Message.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessage.Message.Attributes.Key2 | Should BeExactly "Value2"
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with multiple messages" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"
        $attributes = @{"Key" = "Value"; "Key2" = "Value2"}

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicName
            $publishedMessage.MessageId | Should Not BeNullOrEmpty
            $publishedMessageTwo = Publish-GcpsMessage -Attributes $attributes -Topic $topicName
            $publishedMessageTwo.MessageId | Should Not BeNullOrEmpty
            $publishedMessageThree = Publish-GcpsMessage -Data $testData -Attributes $attributes -Topic $topicName
            $publishedMessageThree.MessageId | Should Not BeNullOrEmpty

            $subscriptionMessages = Get-SubscriptionMessage -Subscription $subscriptionName
            $subscriptionMessages.Count | Should Be 3

            $subscriptionMessageOne = $subscriptionMessages | Where-Object {$_.Message.MessageId -eq $publishedMessage.MessageId}
            (ConvertFrom-Base64String $subscriptionMessageOne.Message.Data) | Should BeExactly $testData

            $subscriptionMessageTwo = $subscriptionMessages | Where-Object {$_.Message.MessageId -eq $publishedMessageTwo.MessageId}
            $subscriptionMessageTwo.Message.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessageTwo.Message.Attributes.Key2 | Should BeExactly "Value2"

            $subscriptionMessageThree = $subscriptionMessages | Where-Object {$_.Message.MessageId -eq $publishedMessageThree.MessageId}
            (ConvertFrom-Base64String $subscriptionMessageThree.Message.Data) | Should BeExactly $testData
            $subscriptionMessageThree.Message.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessageThree.Message.Attributes.Key2 | Should BeExactly "Value2"
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with an array of messages" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"
        $attributes = @{"Key" = "Value"; "Key2" = "Value2"}

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $messageOne = New-GcpsMessage -Data $testData
            $messageTwo = New-GcpsMessage -Attributes $attributes
            $messageThree = New-GcpsMessage -Attributes $attributes -Data $testData
            $messages = @($messageOne, $messageTwo, $messageThree)

            $publishedMessages = Publish-GcpsMessage -Message $messages -Topic $topicName
            $publishedMessages.Count | Should Be 3

            $subscriptionMessages = Get-SubscriptionMessage -Subscription $subscriptionName
            $subscriptionMessages.Count | Should Be 3

            $subscriptionMessageOne = $subscriptionMessages | Where-Object {$_.Message.MessageId -eq $publishedMessages[0].MessageId}
            (ConvertFrom-Base64String $subscriptionMessageOne.Message.Data) | Should BeExactly $testData

            $subscriptionMessageTwo = $subscriptionMessages | Where-Object {$_.Message.MessageId -eq $publishedMessages[1].MessageId}
            $subscriptionMessageTwo.Message.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessageTwo.Message.Attributes.Key2 | Should BeExactly "Value2"

            $subscriptionMessageThree = $subscriptionMessages | Where-Object {$_.Message.MessageId -eq $publishedMessages[2].MessageId}
            (ConvertFrom-Base64String $subscriptionMessageThree.Message.Data) | Should BeExactly $testData
            $subscriptionMessageThree.Message.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessageThree.Message.Attributes.Key2 | Should BeExactly "Value2"
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with multiple subscriptions" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $subscriptionNameTwo = "gcp-test-publish-gcps-message-subscription2-$r"
        $testData = "Test Data"
        $attributes = @{"Key" = "Value"; "Key2" = "Value2"}

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionNameTwo -Topic $topicName

            $publishedMessage = Publish-GcpsMessage -Data $testData -Attributes $attributes -Topic $topicName
            $publishedMessage.MessageId | Should Not BeNullOrEmpty

            $subscriptionMessage = Get-SubscriptionMessage -Subscription $subscriptionName

            $subscriptionMessage.Message.MessageId | Should BeExactly $publishedMessage.MessageId
            (ConvertFrom-Base64String $subscriptionMessage.Message.Data) | Should BeExactly $testData
            $subscriptionMessage.Message.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessage.Message.Attributes.Key2 | Should BeExactly "Value2"

            $subscriptionMessage = Get-SubscriptionMessage -Subscription $subscriptionNameTwo

            $subscriptionMessage.Message.MessageId | Should BeExactly $publishedMessage.MessageId
            (ConvertFrom-Base64String $subscriptionMessage.Message.Data) | Should BeExactly $testData
            $subscriptionMessage.Message.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessage.Message.Attributes.Key2 | Should BeExactly "Value2"
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
            Remove-GcpsSubscription $subscriptionNameTwo
        }
    }

    It "should error out for non-existent topic" {
        $topicName = "gcloud-powershell-non-existent-topic"
        { Publish-GcpsMessage -Data "Test Data" -Topic $topicName -ErrorAction Stop } | Should Throw "does not exist"
    }

    It "should error out for invalid topic name" {
        { Publish-GcpsMessage -Data "Test Data" -Topic "!!" -ErrorAction Stop } | Should Throw "Invalid resource name given"
    }
}

Describe "Get-GcpsMessage" {
    It "should work" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"
        $attributes = @{"Key" = "Value"; "Key2" = "Value2"}

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            $publishedMessage = Publish-GcpsMessage -Data $testData -Attributes $attributes -Topic $topicName

            $subscriptionMessage = Get-GcpsMessage -Subscription $subscriptionName

            $subscriptionMessage.MessageId | Should BeExactly $publishedMessage.MessageId
            $subscriptionMessage.Data | Should BeExactly $testData
            $subscriptionMessage.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessage.Attributes.Key2 | Should BeExactly "Value2"
            $subscriptionMessage.AckId | Should Not BeNullOrEmpty
            $subscriptionMessage.Subscription | Should Match $subscriptionName

            # Since we does not acknowledge the message, another call should return the same message.
            $subscriptionMessageTwo = Get-GcpsMessage -Subscription $subscriptionName

            $subscriptionMessageTwo.MessageId | Should BeExactly $publishedMessage.MessageId
            $subscriptionMessageTwo.Data | Should BeExactly $testData
            $subscriptionMessageTwo.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessageTwo.Attributes.Key2 | Should BeExactly "Value2"
            $subscriptionMessageTwo.AckId | Should Not BeNullOrEmpty
            $subscriptionMessageTwo.Subscription | Should Match $subscriptionName

            # Acknowledge the message with gcloud.
            gcloud beta pubsub subscriptions ack $subscriptionMessageTwo.Subscription $subscriptionMessageTwo.AckId 2>$null
            # Now if we pull again, we should get nothing.
            $subscriptionMessageThree = Get-GcpsMessage -Subscription $subscriptionName -ReturnImmediately
            $subscriptionMessageThree | Should BeNullOrEmpty
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with -AutoAck" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"
        $attributes = @{"Key" = "Value"; "Key2" = "Value2"}

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            $publishedMessage = Publish-GcpsMessage -Data $testData -Attributes $attributes -Topic $topicName

            $subscriptionMessage = Get-GcpsMessage -Subscription $subscriptionName -AutoAck

            $subscriptionMessage.MessageId | Should BeExactly $publishedMessage.MessageId
            $subscriptionMessage.Data | Should BeExactly $testData
            $subscriptionMessage.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessage.Attributes.Key2 | Should BeExactly "Value2"
            $subscriptionMessage.AckId | Should BeNullOrEmpty
            $subscriptionMessage.Subscription | Should Match $subscriptionName

            # Now if we pull again, we should get nothing.
            $subscriptionMessageThree = Get-GcpsMessage -Subscription $subscriptionName -ReturnImmediately
            $subscriptionMessageThree | Should BeNullOrEmpty
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with subscription object" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"

        try {
            New-GcpsTopic -Topic $topicName
            $subscriptionObject = New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicName

            $subscriptionMessage = Get-GcpsMessage -Subscription $subscriptionObject -AutoAck

            $subscriptionMessage.MessageId | Should BeExactly $publishedMessage.MessageId
            $subscriptionMessage.Data | Should BeExactly $testData
            $subscriptionMessage.AckId | Should BeNullOrEmpty
            $subscriptionMessage.Subscription | Should Match $subscriptionName
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with multiple messages" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"
        $attributes = @{"Key" = "Value"; "Key2" = "Value2"}

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $messageOne = New-GcpsMessage -Data $testData
            $messageTwo = New-GcpsMessage -Attributes $attributes
            $messageThree = New-GcpsMessage -Attributes $attributes -Data $testData
            $messages = @($messageOne, $messageTwo, $messageThree)

            $publishedMessages = Publish-GcpsMessage -Message $messages -Topic $topicName

            # Retrieves all 3 messages.
            $subscriptionMessages = @()
            # We will try for a maximum of 10 times.
            for ($i = 0; $i -lt 10; $i += 1)
            {
                $subscriptionMessages += (Get-GcpsMessage -Subscription $subscriptionName -AutoAck)
                if ($subscriptionMessages.Count -eq 3)
                {
                    break
                }
            }

            $subscriptionMessageOne = $subscriptionMessages | Where-Object {$_.MessageId -eq $publishedMessages[0].MessageId}
            $subscriptionMessageOne.Data | Should BeExactly $testData
            $subscriptionMessageOne.AckId | Should BeNullOrEmpty
            $subscriptionMessageOne.Subscription | Should Match $subscriptionName

            $subscriptionMessageTwo = $subscriptionMessages | Where-Object {$_.MessageId -eq $publishedMessages[1].MessageId}
            $subscriptionMessageTwo.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessageTwo.Attributes.Key2 | Should BeExactly "Value2"
            $subscriptionMessageTwo.AckId | Should BeNullOrEmpty
            $subscriptionMessageTwo.Subscription | Should Match $subscriptionName

            $subscriptionMessageThree = $subscriptionMessages | Where-Object {$_.MessageId -eq $publishedMessages[2].MessageId}
            $subscriptionMessageThree.Data | Should BeExactly $testData
            $subscriptionMessageThree.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessageThree.Attributes.Key2 | Should BeExactly "Value2"
            $subscriptionMessageThree.AckId | Should BeNullOrEmpty
            $subscriptionMessageThree.Subscription | Should Match $subscriptionName

            # Now if we pull again, we should get nothing.
            $subscriptionMessageThree = Get-GcpsMessage -Subscription $subscriptionName -ReturnImmediately
            $subscriptionMessageThree | Should BeNullOrEmpty
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with -MaxMessages" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"
        $attributes = @{"Key" = "Value"; "Key2" = "Value2"}

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $messageOne = New-GcpsMessage -Data $testData
            $messageTwo = New-GcpsMessage -Attributes $attributes
            $messages = @($messageOne, $messageTwo)

            $publishedMessages = Publish-GcpsMessage -Message $messages -Topic $topicName

            # Retrieves all 2 messages, making sure that when we set -MaxMessage to 1, we get 1 at a time.
            $subscriptionMessages = @()
            # We will try for a maximum of 10 times.
            for ($i = 0; $i -lt 10; $i += 1)
            {
                $subscriptionMessage = @(Get-GcpsMessage -Subscription $subscriptionName -AutoAck -MaxMessage 1)
                $subscriptionMessage.Count | Should Be 1
                $subscriptionMessages += $subscriptionMessage
                if ($subscriptionMessages.Count -eq 2)
                {
                    break
                }
            }

            $subscriptionMessageOne = $subscriptionMessages | Where-Object {$_.MessageId -eq $publishedMessages[0].MessageId}
            $subscriptionMessageOne.Data | Should BeExactly $testData
            $subscriptionMessageOne.AckId | Should BeNullOrEmpty
            $subscriptionMessageOne.Subscription | Should Match $subscriptionName

            $subscriptionMessageTwo = $subscriptionMessages | Where-Object {$_.MessageId -eq $publishedMessages[1].MessageId}
            $subscriptionMessageTwo.Attributes.Key | Should BeExactly "Value"
            $subscriptionMessageTwo.Attributes.Key2 | Should BeExactly "Value2"
            $subscriptionMessageTwo.AckId | Should BeNullOrEmpty
            $subscriptionMessageTwo.Subscription | Should Match $subscriptionName

            # Now if we pull again, we should get nothing.
            $subscriptionMessageThree = Get-GcpsMessage -Subscription $subscriptionName -ReturnImmediately
            $subscriptionMessageThree | Should BeNullOrEmpty
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }        
    }

    It "should work with -ReturnImmediately" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            $subscriptionMessage = Get-GcpsMessage -Subscription $subscriptionName -ReturnImmediately
            $subscriptionMessage | Should BeNullOrEmpty
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should block without -ReturnImmediately" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            # We need the GcloudCmdlets path to call Install-GCloudCmdlets so the cmdlets Get-GcpsMessage will be loaded.
            $gcloudCmdletsPath = (Resolve-Path "$PSScriptRoot\..\GcloudCmdlets.ps1").Path
            $sb = [scriptblock]::Create(". $gcloudCmdletsPath; Install-GCloudCmdlets; Get-GcpsMessage -Subscription $subscriptionName")
            $job = Start-Job -ScriptBlock $sb

            Start-Sleep -Seconds 5

            # After 5 seconds, job should still be running and not completed because of the blocking call Get-GcpsMessage.
            $job.State | Should Be "Running"

            # Now we publish 1 message, the job should finish running.
            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicName
            Start-Sleep -Seconds 5
            $job.State | Should Be "Completed"
            $subscriptionMessage = $job | Receive-Job
            $subscriptionMessage.MessageId | Should BeExactly $publishedMessage.MessageId
            $subscriptionMessage.Data | Should BeExactly $testData
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
            Get-Job | Remove-Job -Force
        }
    }

    It "should error out for non-existent subscription" {
        $subscription = "gcloud-powershell-non-existent-subscription"
        { Get-GcpsMessage -Subscription $subscription -ReturnImmediately -ErrorAction Stop } | Should Throw "does not exist"
    }

    It "should error out for invalid subscription name" {
        { Get-GcpsMessage -Subscription "!!" -ReturnImmediately -ErrorAction Stop } | Should Throw "Invalid resource name given"
    }

    It "should error out if MaxMessages has value less than or equal to zero" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            { Get-GcpsMessage -Subscription $subscriptionName -MaxMessages 0 } | Should Throw "should have a value greater than 0."
            { Get-GcpsMessage -Subscription $subscriptionName -MaxMessages -10 } | Should Throw "should have a value greater than 0."
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }
}

Describe "Send-GcpsAck" {
    It "should work" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicName
            $subscriptionMessage = Get-GcpsMessage -Subscription $subscriptionName

            $subscriptionMessage.MessageId | Should BeExactly $publishedMessage.MessageId

            # Since we does not acknowledge the message, another call should return the same message.
            $subscriptionMessageTwo = Get-GcpsMessage -Subscription $subscriptionName
            $subscriptionMessageTwo.MessageId | Should BeExactly $publishedMessage.MessageId

            # Acknowledge the message.
            Send-GcpsAck -Subscription $subscriptionName -AckId $subscriptionMessageTwo.AckId

            # Now if we pull again, we should get nothing.
            $subscriptionMessageThree = Get-GcpsMessage -Subscription $subscriptionName -ReturnImmediately
            $subscriptionMessageThree | Should BeNullOrEmpty
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with subscription object" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"

        try {
            New-GcpsTopic -Topic $topicName
            $subscription = New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicName
            $subscriptionMessage = Get-GcpsMessage -Subscription $subscriptionName

            $subscriptionMessage.MessageId | Should BeExactly $publishedMessage.MessageId

            # Acknowledge the message.
            Send-GcpsAck -Subscription $subscription -AckId $subscriptionMessage.AckId

            # Now if we pull again, we should get nothing.
            $subscriptionMessageThree = Get-GcpsMessage -Subscription $subscriptionName -ReturnImmediately
            $subscriptionMessageThree | Should BeNullOrEmpty
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with multiple messages" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"
        $attributes = @{"Key" = "Value"; "Key2" = "Value2"}

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            Start-Sleep -Seconds 5

            $messageOne = New-GcpsMessage -Data $testData
            $messageTwo = New-GcpsMessage -Attributes $attributes
            $messageThree = New-GcpsMessage -Attributes $attributes -Data $testData
            $messages = @($messageOne, $messageTwo, $messageThree)

            $publishedMessages = Publish-GcpsMessage -Message $messages -Topic $topicName

            # Retrieves and acknowledges all 3 messages.
            $subscriptionMessages = @()
            # We will try for a maximum of 10 times.
            for ($i = 0; $i -lt 10; $i += 1)
            {
                $subscriptionMessages += (Get-GcpsMessage -Subscription $subscriptionName)
                if ($subscriptionMessages.Count -eq 3)
                {
                    break
                }
            }

            Send-GcpsAck -InputObject $subscriptionMessages

            # Now if we pull again, we should get nothing.
            $subscriptionMessageThree = Get-GcpsMessage -Subscription $subscriptionName -ReturnImmediately
            $subscriptionMessageThree | Should BeNullOrEmpty
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should error out for non-existent subscription" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            Start-Sleep -Seconds 5

            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicName
            $subscriptionMessage = Get-GcpsMessage -Subscription $subscriptionName

            $subscriptionMessage.MessageId | Should BeExactly $publishedMessage.MessageId
            $subscriptionMessage.AckId | Should Not BeNullOrEmpty

            $subscription = "gcloud-powershell-non-existent-subscription"
            { Send-GcpsAck -Subscription $subscription -AckId $subscriptionMessage.AckId -ErrorAction Stop } | Should Throw "does not exist"
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should error out for invalid Ack Id" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            Start-Sleep -Seconds 5

            { Send-GcpsAck -Subscription $subscriptionName -AckId "Invalid Ack" -ErrorAction Stop } | Should Throw "invalid ack ID"
            { Send-GcpsAck -Subscription $subscriptionName -AckId "!!" -ErrorAction Stop } | Should Throw "invalid ack ID"
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should error out for invalid subscription name" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            Start-Sleep -Seconds 5

            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicName
            $subscriptionMessage = Get-GcpsMessage -Subscription $subscriptionName

            $subscriptionMessage.MessageId | Should BeExactly $publishedMessage.MessageId
            $subscriptionMessage.AckId | Should Not BeNullOrEmpty

            $subscription = "gcloud-powershell-non-existent-subscription"
            { Send-GcpsAck -Subscription "!!" -AckId $subscriptionMessage.AckId -ErrorAction Stop } | Should Throw "Invalid resource name given"
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }
}

Describe "Set-GcpsAckDeadline" {
    It "should work" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicName
            $subscriptionMessage = Get-GcpsMessage -Subscription $subscriptionName

            $subscriptionMessage.MessageId | Should BeExactly $publishedMessage.MessageId

            # Wait for 12 seconds before sending an acknowledgement. If we do a pull after, we should still get the message.
            Start-Sleep -Seconds 12
            Send-GcpsAck -Subscription $subscriptionName -AckId $subscriptionMessage.AckId

            $subscriptionMessageTwo = Get-GcpsMessage -Subscription $subscriptionName
            $subscriptionMessageTwo.MessageId | Should BeExactly $publishedMessage.MessageId

            # Now modify the ack deadline to 20 and acknowledge it after 12 seconds of sleep.
            Set-GcpsAckDeadline -Subscription $subscriptionName -AckId $subscriptionMessageTwo.AckId -AckDeadline 20
            Start-Sleep -Seconds 12
            Send-GcpsAck -Subscription $subscriptionName -AckId $subscriptionMessageTwo.AckId

            # Now if we pull again, we should get nothing.
            $subscriptionMessageThree = Get-GcpsMessage -Subscription $subscriptionName -ReturnImmediately
            $subscriptionMessageThree | Should BeNullOrEmpty
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with subscription object" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"

        try {
            New-GcpsTopic -Topic $topicName
            $subscription = New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicName
            $subscriptionMessage = Get-GcpsMessage -Subscription $subscription

            $subscriptionMessage.MessageId | Should BeExactly $publishedMessage.MessageId

            # Now modify the ack deadline to 20 and acknowledge it after 12 seconds of sleep.
            Set-GcpsAckDeadline -Subscription $subscription -AckId $subscriptionMessage.AckId -AckDeadline 20
            Start-Sleep -Seconds 12
            Send-GcpsAck -Subscription $subscription -AckId $subscriptionMessage.AckId

            # Now if we pull again, we should get nothing.
            $subscriptionMessageTwo = Get-GcpsMessage -Subscription $subscription -ReturnImmediately
            $subscriptionMessageTwo | Should BeNullOrEmpty
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should work with multiple messages" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"
        $attributes = @{"Key" = "Value"; "Key2" = "Value2"}

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $messageOne = New-GcpsMessage -Data $testData
            $messageTwo = New-GcpsMessage -Attributes $attributes
            $messageThree = New-GcpsMessage -Attributes $attributes -Data $testData
            $messages = @($messageOne, $messageTwo, $messageThree)

            $publishedMessages = Publish-GcpsMessage -Message $messages -Topic $topicName

            # Retrieves and modifies subscription for all 3 messages
            $subscriptionMessages = @()
            # We will try for a maximum of 10 times
            for ($i = 0; $i -lt 10; $i += 1)
            {
                $subscriptionMessages += (Get-GcpsMessage -Subscription $subscriptionName)
                if ($subscriptionMessages.Count -eq 3)
                {
                    break
                }
            }

            Set-GcpsAckDeadline -InputObject $subscriptionMessages -AckDeadline 20
            Start-Sleep -Seconds 12
            Send-GcpsAck -InputObject $subscriptionMessages

            # Now if we pull again, we should get nothing
            $subscriptionMessageThree = Get-GcpsMessage -Subscription $subscriptionName -ReturnImmediately
            $subscriptionMessageThree | Should BeNullOrEmpty
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should error out for ack deadline not between 0 and 600 seconds" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicName
            $subscriptionMessage = Get-GcpsMessage -Subscription $subscriptionName

            $subscriptionMessage.MessageId | Should BeExactly $publishedMessage.MessageId
            $subscriptionMessage.AckId | Should Not BeNullOrEmpty

            { Set-GcpsAckDeadline -AckDeadline -10 -Subscription $subscriptionName -AckId $subscriptionMessage.AckId -ErrorAction Stop } |
                Should Throw "The ack deadline must be between 0 and 600 seconds"
            { Set-GcpsAckDeadline -AckDeadline 700 -Subscription $subscriptionName -AckId $subscriptionMessage.AckId -ErrorAction Stop } |
                Should Throw "The ack deadline must be between 0 and 600 seconds"
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should error out for non-existent subscription" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicName
            $subscriptionMessage = Get-GcpsMessage -Subscription $subscriptionName

            $subscriptionMessage.MessageId | Should BeExactly $publishedMessage.MessageId
            $subscriptionMessage.AckId | Should Not BeNullOrEmpty

            $subscription = "gcloud-powershell-non-existent-subscription"
            { Set-GcpsAckDeadline -AckDeadline 20 -Subscription $subscription -AckId $subscriptionMessage.AckId -ErrorAction Stop } |
                Should Throw "does not exist"
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should error out for invalid Ack Id" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            { Set-GcpsAckDeadline -AckDeadline 20 -Subscription $subscriptionName -AckId "Invalid Ack" -ErrorAction Stop } |
                Should Throw "invalid ack ID"
            { Set-GcpsAckDeadline -AckDeadline 20 -Subscription $subscriptionName -AckId "!!" -ErrorAction Stop } |
                Should Throw "invalid ack ID"
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }

    It "should error out for invalid subscription name" {
        $r = Get-Random
        $topicName = "gcp-test-publish-gcps-message-topic-$r"
        $subscriptionName = "gcp-test-publish-gcps-message-subscription-$r"
        $testData = "Test Data"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName
            $publishedMessage = Publish-GcpsMessage -Data $testData -Topic $topicName
            $subscriptionMessage = Get-GcpsMessage -Subscription $subscriptionName

            $subscriptionMessage.MessageId | Should BeExactly $publishedMessage.MessageId
            $subscriptionMessage.AckId | Should Not BeNullOrEmpty

            $subscription = "gcloud-powershell-non-existent-subscription"
            { Set-GcpsAckDeadline -AckDeadline 20 -Subscription "!!" -AckId $subscriptionMessage.AckId -ErrorAction Stop } |
                Should Throw "Invalid resource name given"
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }
}
