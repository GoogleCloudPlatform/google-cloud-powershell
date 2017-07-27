. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcpsTopic" {
    $r = Get-Random
    $script:topicName = "gcps-testing-topic-$r"
    $script:secondTopicName = "gcps-testing-topic2-$r"
    $previousCount = (Get-GcpsTopic).Count
    gcloud beta pubsub topics create $script:topicName 2>$null
    gcloud beta pubsub topics create $script:secondTopicName 2>$null
    Start-Sleep -Seconds 5

    AfterAll {
        gcloud beta pubsub topics delete $topicName --quiet 2>$null
        gcloud beta pubsub topics delete $secondTopicName --quiet 2>$null
    }

    It "should work without any parameters" {
        $topics = Get-GcpsTopic
        $topics.Count - $previousCount | Should Be 2
    }

    It "should work with -Topic parameter" {
        $firstTopic = Get-GcpsTopic -Topic $topicName
        $firstTopic.Name | Should BeExactly "projects/$project/topics/$topicName"

        # Should work with an array of topics names.
        $topics = Get-GcpsTopic -Topic $topicName, $secondTopicName
        $topics.Count | Should Be 2
        $topics.Name -contains "projects/$project/topics/$topicName" | Should Be $true
        $topics.Name -contains "projects/$project/topics/$secondTopicName" | Should Be $true
    }

    It "should throw an error for non-existent topic" {
        { Get-GcpsTopic -Topic "non-existent-topic-name" -ErrorAction Stop } | Should Throw "does not exist"
    }

    It "should throw error for invalid topic name" {
        { Get-GcpsTopic -Topic "!!" -ErrorAction Stop } | Should Throw "Invalid resource name"
    }
}

Describe "New-GcpsTopic" {
    It "should work" {
        $r = Get-Random
        $topicName = "gcp-creating-topic-$r"
        $secondTopicName = "gcp-creating-topic2-$r"
        $thirdTopicName = "gcp-creating-topic3-$r"

        try {
            New-GcpsTopic -Topic $topicName
            (Get-GcpsTopic -Topic $topicName).Name | Should BeExactly "projects/$project/topics/$topicName"

            # Should work with an array of topics names.
            New-GcpsTopic -Topic $secondTopicName, $thirdTopicName
            $topics = Get-GcpsTopic -Topic $secondTopicName, $thirdTopicName
            $topics.Count | Should Be 2
            $topics.Name -contains "projects/$project/topics/$secondTopicName" | Should Be $true
            $topics.Name -contains "projects/$project/topics/$thirdTopicName" | Should Be $true
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
            gcloud beta pubsub topics delete $secondTopicName --quiet 2>$null
            gcloud beta pubsub topics delete $thirdTopicName --quiet 2>$null
        }
    }

    It "should throw an error for topic that is already created" {
        $r = Get-Random
        $topicName = "gcp-creating-topic-$r"

        try {
            New-GcpsTopic -Topic $topicName
            (Get-GcpsTopic -Topic $topicName).Name | Should BeExactly "projects/$project/topics/$topicName"
            { New-GcpsTopic -Topic $topicName -ErrorAction Stop } | Should Throw "already exists" 
        }
        finally {
            gcloud beta pubsub topics delete $topicName --quiet 2>$null
        }
    }

    It "should throw error for invalid topic name" {
        { New-GcpsTopic -Topic "!!" -ErrorAction Stop } | Should Throw "Invalid resource name"
    }
}

Describe "Remove-GcpsTopic" {
    It "should work" {
        $r = Get-Random
        $topicName = "gcp-creating-topic-$r"
        $secondTopicName = "gcp-creating-topic2-$r"
        $thirdTopicName = "gcp-creating-topic3-$r"
        New-GcpsTopic -Topic $topicName, $secondTopicName, $thirdTopicName
        (Get-GcpsTopic -Topic $topicName, $secondTopicName, $thirdTopicName).Count | Should Be 3

        # Remove a single topic.
        Remove-GcpsTopic -Topic $topicName
        { Get-GcpsTopic -Topic $topicName -ErrorAction Stop } | Should Throw "does not exist"

        # Remove an array of topics.
        Remove-GcpsTopic -Topic $secondTopicName, $thirdTopicName
        { Get-GcpsTopic -Topic $secondTopicName, $thirdTopicName -ErrorAction Stop } | Should Throw "does not exist"
    }

    It "should work with topic object" {
        $r = Get-Random
        $topicName = "gcp-creating-topic-$r"
        $secondTopicName = "gcp-creating-topic2-$r"
        $thirdTopicName = "gcp-creating-topic3-$r"
        New-GcpsTopic -Topic $topicName, $secondTopicName, $thirdTopicName
        (Get-GcpsTopic -Topic $topicName, $secondTopicName, $thirdTopicName).Count | Should Be 3

        # Remove a single topic.
        $oneTopic = Get-GcpsTopic $topicName
        Remove-GcpsTopic -Topic $oneTopic
        { Get-GcpsTopic -Topic $topicName -ErrorAction Stop } | Should Throw "does not exist"

        # Remove an array of topics.
        $twoTopics = Get-GcpsTopic $secondTopicName, $thirdTopicName
        Remove-GcpsTopic -Topic $twoTopics
        { Get-GcpsTopic -Topic $secondTopicName, $thirdTopicName -ErrorAction Stop } | Should Throw "does not exist"
    }

    It "should work with pipeline" {
        $r = Get-Random
        $topicName = "gcp-creating-topic-$r"
        New-GcpsTopic -Topic $topicName
        Get-GcpsTopic -Topic $topicName | Should Not BeNullOrEmpty

        # Remove through pipeline
        Get-GcpsTopic -Topic $topicName | Remove-GcpsTopic
        { Get-GcpsTopic -Topic $topicName -ErrorAction Stop } | Should Throw "does not exist"
    }

    It "should throw error for non-existent topic" {
        { Remove-GcpsTopic -Topic "non-existent-topic-powershell-testing" -ErrorAction Stop } | Should Throw "does not exist"
    }

    It "should throw error for invalid topic name" {
        { Remove-GcpsTopic -Topic "!!" -ErrorAction Stop } | Should Throw "Invalid resource name"
    }

    It "should not remove topic if -WhatIf is used" {
        $r = Get-Random
        $topicName = "gcp-creating-topic-$r"
        New-GcpsTopic -Topic $topicName
        Get-GcpsTopic -Topic $topicName | Should Not BeNullOrEmpty

        # Topic should not be removed.
        Remove-GcpsTopic -Topic $topicName -WhatIf
        Get-GcpsTopic -Topic $topicName | Should Not BeNullOrEmpty

        Remove-GcpsTopic -Topic $topicName
    }
}
