. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

function CheckMessage($message, $topic)
{
    ADD CHECK FOR MESSAGE HERE
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
        $topicName = "gcp-test-new-subscription-topic-$r"
        $subscriptionName = "gcp-test-new-subscription-$r"
        $testData = "Test Data"

        try {
            New-GcpsTopic -Topic $topicName
            New-GcpsSubscription -Subscription $subscriptionName -Topic $topicName

            $publishedMessage = Publish-GcpsMessage -Data $testData
            $publishedMessage.MessageId | Should Not BeNullOrEmpty

            ADD MORE CHECK FOR MESSAGE HERE
        }
        finally {
            Remove-GcpsTopic $topicName
            Remove-GcpsSubscription $subscriptionName
        }
    }
}
