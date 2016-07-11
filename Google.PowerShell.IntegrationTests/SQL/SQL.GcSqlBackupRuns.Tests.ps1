. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
$project, $_, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcSqlBackupRun" {
    #An instance to test cannot be ceated for these tests because it will not have a backuprun upon creation.
    $instance = "test-db-fg"

    It "should get a reasonable response" {
        $backups = Get-GcSqlBackupRun -Project $project -Instance $instance
        # Backupruns are unique to instances. This means there's very little concrete
        # evidence we can test for.
        $backups.Length | Should BeGreaterThan 0
    }

    It "shouldn't need the project parameter if configuration is set up correctly" {
        $backups = Get-GcSqlBackupRun $instance
        $backups.Length | Should BeGreaterThan 0
    }

    It "should get a reasonable response from a given query" {
        # A specific Id has to be used because backup Id's are unique to backupRuns. 
        # See the next test for if a name exclusive to test-back is used.
        $backup = Get-GcSqlBackupRun $instance "1467183600370"
        $backup.Status | Should Be "SKIPPED"
        $backup.Instance | Should Be $instance
        $backup.Id | Should Be "1467183600370"
    }

    It "should compound with the list parameter set" {
        $backups = Get-GcSqlBackupRun $instance
        $firstBackup = $backups | Select-Object -first 1
        $backup = Get-GcSqlBackupRun $instance $firstBackup.Id
        $backup.SelfLink | Should Be $firstBackup.SelfLink
        $backup.Id | Should Be $firstBackup.Id
    }
}

Describe "Remove-GcSqlBackupRun" {
    # This is currently extremely hard to test for, as backup runs are made once a day and cannot be 
    # forced to be made after making an instance.

    # First test would verify it works well (This was done within powershell).

    # Second test verifies a backuprun can be passed in through pipeline to delete it. Also done within powershell.
}
Reset-GCloudConfig $oldActiveConfig $configName
