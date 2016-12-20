. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

# Returns all available messages from the subscription (auto-acknowledge all of them in the process)
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
