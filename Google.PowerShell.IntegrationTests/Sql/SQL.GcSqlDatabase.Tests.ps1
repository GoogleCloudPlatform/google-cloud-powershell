﻿. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
$project, $_, $oldActiveConfig, $configName = Set-GCloudConfig
$instance = "test-fg"

Describe "Get-GcSqlDatabase" {
    It "should error if given a non-first-generation instance" {
        {$databases = Get-GcSqlDatabase "test-db2"} | Should Throw 
    }

    It "should not error if given a second-generation instance" {
        {$databases = Get-GcSqlDatabase "test-fg"} | Should Not Throw 
    }

    It "should list databases for an instance" {
        $databases = Get-GcSqlDatabase $instance
        $databases.Length | Should BeGreaterThan 0
        ($databases.Name -contains "guestbook") | Should Be true
    }

    It "should get the correct information for a database" {
        $database = Get-GcSqlDatabase $instance "guestbook"
        $database.SelfLink | Should Be "https://www.googleapis.com/sql/v1beta4/projects/gcloud-powershell-testing/instances/test-fg/databases/guestbook"
        $database.Collation | Should Be "utf8_general_ci"
    }

    It "should compound with the List parameter set" {
        $databases = Get-GcSqlDatabase $instance
        $firstDatabase = $databases | Select-Object -First 1
        $databaseName = $firstDatabase.Name
        $database = Get-GcSqlDatabase $instance $databaseName
        $database.SelfLink | Should Be $firstDatabase.SelfLink
    }

    gcloud config set project google.com:g-cloudsharp

    It "should be able to take in a project different than the base" {
        $databases = Get-GcSqlDatabase -Project $project $instance
        $databases.Length | Should BeGreaterThan 0
        ($databases.Name -contains "guestbook") | Should Be true
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
