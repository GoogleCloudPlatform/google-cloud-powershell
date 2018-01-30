. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
$project, $_, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcSqlBackupRun" {
    # An instance to test cannot be created for these tests because it will not have a backuprun upon creation.
    $instance = "test-db4"

    It "should get a reasonable response" {
        $backups = Get-GcSqlBackupRun -Project $project -Instance $instance
        # Backupruns are unique to instances. This means there's very little concrete
        # evidence we can test for.
        $backups.Length | Should BeGreaterThan 0
    }

    It "shouldn't need the project parameter if configuration is set up correctly" -Skip {
        $backups = Get-GcSqlBackupRun $instance
        $backups.Length | Should BeGreaterThan 0
    }

    It "should get a reasonable response for a specific backupRun" -Skip {
        # An existing backupRun is used to prevent having to update this test every
        # time the specified backup is changed/removed.
        $existingBackup = Get-GcSqlBackupRun $instance | Select-Object -first 1
        $backup = Get-GcSqlBackupRun $instance $existingBackup.Id
        $backup.Status | Should Be $existingBackup.Status
        $backup.Instance | Should Be $instance
        $backup.Id | Should Be $existingBackup.Id
    }
}

Describe "Remove-GcSqlBackupRun" {
    # This is currently extremely hard to test for, as backup runs are made once a day and cannot be 
    # forced to be made after making an instance.

    # First test would verify it works well (This was done within powershell).

    # Second test verifies a backuprun can be passed in through pipeline to delete it. Also done within powershell.
}
Reset-GCloudConfig $oldActiveConfig $configName
