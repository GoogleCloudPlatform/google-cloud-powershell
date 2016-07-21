. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
$project, $_, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcSqlInstance" {
    $r = Get-Random
    # A random number is used to avoid collisions with the speed of creating
    # and deleting instances.
    $instance = "test-inst$r"
    gcloud sql instances create $instance --quiet 2>$null

    It "should get a reasonable list response" {
        $instances = Get-GcSqlInstance -Project $project
        ($instances.Name -contains $instance) | Should Be true
    }

    It "shouldn't require the Project parameter to be specified" {
        $instances = Get-GcSqlInstance 
        ($instances.Name -contains $instance) | Should Be true
    }

    It "should be able to get information on a specific instance" {
        $testInstance = Get-GcSqlInstance $instance
        $testInstance.InstanceType | Should Be "CLOUD_SQL_INSTANCE"
        $testInstance.Name | Should Be $instance
    }

    It "should compound with the list parameter set" {
        $instances = Get-GcSqlInstance 
        $firstInstance = $instances | Select-Object -first 1
        $testInstance = Get-GcSqlInstance $firstInstance.Name
        $testInstance.Name | Should Be $firstInstance.Name
        $testInstance.SelfLink | Should be $firstInstance.SelfLink
    }

    It "should take in pipeline input" {
        $firstInstance = Get-GcSqlInstance | Select-Object -first 1
        $testInstance = Get-GcSqlInstance | Select-Object -first 1 | Get-GcSqlInstance
        $testInstance.Name | Should Be $firstInstance.Name
        $testInstance.SelfLink | Should be $firstInstance.SelfLink
    }
    
    gcloud sql instances delete $instance --quiet 2>$null
}

Describe "Add-GcSqlInstance" {

    It "should work" {
        $r = Get-Random
        # A random number is used to avoid collisions with the speed of creating
        # and deleting instances.
        $instance = "test-inst$r"
        $instances = Get-GcSqlInstance
        $setting = New-GcSqlSettingConfig "db-n1-standard-1"
        $config = New-GcSqlInstanceConfig $instance -SettingConfig $setting
        Add-GcSqlInstance $config
        $newInstances = Get-GcSqlInstance
        ($instances.Name -contains $instance) | Should Be false
        ($newInstances.Name -contains $instance) | Should Be true
        gcloud sql instances delete $instance --quiet 2>$null
    }
    
    It "should be able to reflect custom settings" {
        $r = Get-Random
        # A random number is used to avoid collisions with the speed of creating
        # and deleting instances.
        $instance = "test-inst$r"
        $setting = New-GcSqlSettingConfig "db-n1-standard-1" -MaintenanceWindowDay 1 -MaintenanceWindowHour 2
        $config = New-GcSqlInstanceConfig $instance -SettingConfig $setting
        Add-GcSqlInstance $config
        $myInstance = Get-GcSqlInstance $instance
        $myInstance.Settings.MaintenanceWindow.Day | Should Be 1
        $myInstance.Settings.MaintenanceWindow.Hour | Should Be 2
        gcloud sql instances delete $instance --quiet 2>$null
    }
}

Describe "Remove-GcSqlInstance" {
    It "should work" {
        $r = Get-Random
        # A random number is used to avoid collisions with the speed of creating
        # and deleting instances.
        $instance = "test-inst$r"
        gcloud sql instances create $instance --quiet 2>$null
        $instances = Get-GcSqlInstance
        ($instances.Name -contains $instance) | Should Be true
        Remove-GcSqlInstance $instance
        $instances = Get-GcSqlInstance
        ($instances.Name -contains $instance) | Should Be false
    }

    It "should be able to take a pipelined Instance" {
        $r = Get-Random
        # A random number is used to avoid collisions with the speed of creating
        # and deleting instances.
        $instance = "test-inst$r"
        gcloud sql instances create $instance --quiet 2>$null
        $instances = Get-GcSqlInstance
        ($instances.Name -contains $instance) | Should Be true
        Get-GcSqlInstance $instance | Remove-GcSqlInstance
        $instances = Get-GcSqlInstance
        ($instances.Name -contains $instance) | Should Be false
    }

    It "shouldn't delete anything that doesn't exist" {
        { Remove-GcSqlInstance "should-fail" } | Should Throw "The client is not authorized to make this request. [403]"
    }
}

Describe "Copy-GcSqlInstance" {
    # For these tests, test-db2 was used because an instance must have a database and a binarylog 
    # for it to work. These cannot be easily instantiated like in other tests.
    It "should work" {
        $r = Get-Random
        # A random number is used to avoid collisions with the speed of creating
        # and deleting instances.
        $instance = "test-copy$r"
        Copy-GcSqlInstance "test-db2" $instance "mysql-bin.000001" 1133
        $original = Get-GcSqlInstance "test-db2"
        $clones = Get-GcSqlInstance 
        ($clones.Name -contains $instance) | Should Be true
        $clone = Get-GcSqlInstance $instance
        $clone.BackendType | Should Be $original.BackendType
        gcloud sql instances delete $instance --quiet 2>$null
    }

    It "should be able to take a pipelined Instance" {
        $r = Get-Random
        # A random number is used to avoid collisions with the speed of creating
        # and deleting instances.
        $instance = "test-copy$r"
        Get-GcSqlInstance "test-db2" | Copy-GcSqlInstance -CloneName $instance -binaryLogFileName "mysql-bin.000001" -binaryLogPosition 1133
        $clones = Get-GcSqlInstance 
        ($clones.Name -contains $instance) | Should Be true
        gcloud sql instances delete $instance --quiet 2>$null
    }

    It "shouldn't copy something if it doesn't exist" {
        { Copy-GcSqlInstance "fail" "shouldfail" "mysql-bin.000001" 1133}`
            | Should Throw "The client is not authorized to make this request. [403]"
    }
}

Describe "Export-GcSqlInstance" {
    AfterAll {
        gsutil -q rm gs://gcsql-instance-testing/*
    }

    # For these tests, test-db2 was used because an instance must have a populated database for it to work.
    # A specific nondescript bucket was also used because the permissions have to be set correctly
    $r = Get-Random
    # A random number is used to avoid collisions with the speed of creating
    # and deleting instances.
    $instance = "test-db2"


    It "should export an applicable SQL file" {
        $beforeObjects = gsutil ls gs://gcsql-instance-testing
        ($beforeObjects -contains "gs://gcsql-instance-testing/testsql$r.gz") | Should Be false
        Export-GcSqlInstance "test-db2" "gs://gcsql-instance-testing/testsql$r.gz"
        $afterObjects = gsutil ls gs://gcsql-instance-testing
        ($afterObjects -contains "gs://gcsql-instance-testing/testsql$r.gz") | Should Be true
    }

    It "should export an applicable CSV file" {
        $beforeObjects = gsutil ls gs://gcsql-instance-testing
        ($beforeObjects -contains "gs://gcsql-instance-testing/testcsv$r.csv") | Should Be false
        Export-GcSqlInstance "test-db2" "gs://gcsql-instance-testing/testcsv$r.csv" "SELECT * FROM guestbook.entries"
        $afterObjects = gsutil ls gs://gcsql-instance-testing
        ($afterObjects -contains "gs://gcsql-instance-testing/testcsv$r.csv") | Should Be true
    }

    It "should be able to export a specific SQL file" {
        $beforeObjects = gsutil ls gs://gcsql-instance-testing
        ($beforeObjects -contains "gs://gcsql-instance-testing/testothersql$r.gz") | Should Be false
        Export-GcSqlInstance "test-db2" "gs://gcsql-instance-testing/testothersql$r.gz" -Database "guestbook","guestbook2" 
        $afterObjects = gsutil ls gs://gcsql-instance-testing
        ($afterObjects -contains "gs://gcsql-instance-testing/testothersql$r.gz") | Should Be true
    }

    It "should be able to export a specific CSV file" {
        $beforeObjects = gsutil ls gs://gcsql-instance-testing
        ($beforeObjects -contains "gs://gcsql-instance-testing/testothercsv$r.csv") | Should Be false
        Export-GcSqlInstance "test-db2" "gs://gcsql-instance-testing/testothercsv$r.csv" -Database "guestbook" "SELECT * FROM entries"
        $afterObjects = gsutil ls gs://gcsql-instance-testing
        ($afterObjects -contains "gs://gcsql-instance-testing/testothercsv$r.csv") | Should Be true
    }

}

Describe "Import-GcSqlInstance" {
    # For these tests, test-db2 was used because an instance must have a database for it to work.
    # A specific nondescript bucket with nondescript files was also used 
    # because the permissions have to be set correctly.
    $instance = "test-db2"

    # Ordinarily for these tests to work, you would do something similar to the following:
   
    # gcloud sql instances create $instance --quiet 2>$null
    # $bucket = New-GcsBucket -Name "gcps-bucket-creation" -Project $project
    # Get an SQL or CSV file onto your computer
    # New-GcsObject $bucket $objectName -File $filename
    # Make sure the permissions for both the uploaded file and the bucket are set for import to work
    # (You can do this by running Import, getting the error message, and then adding that user to the permissions
    # on the storage website.)
    # Create the database/tables for your instance using a MySQL client.
    # The tests could now be run with the applicable files/instances.
    # Afterwards,
    # gcloud sql instances delete $instance --quiet 2>$null
    # Remove-GcsBucket $bucketName -Force

    # Because importing data into a database can take varying amounts of time, the only way to be sure the operation was
    # successful is to make sure that the Import cmdlet does not error while waiting for the operation to finish.
    # If the operation errors, like in test 3, then the Import operation failed.

    
    It "should be able to import a regular SQL file" {
        { Import-GcSqlInstance "test-db2" "gs://gcsql-csharp-import-testing/testsqlS" "newguestbook" } | Should not Throw
    }

    It "should be able to import a regular CSV file" {
        { Import-GcSqlInstance "test-db2" "gs://gcsql-csharp-import-testing/testsql.csv" "newguestbook" "entries" } | Should not Throw
    }

    It "should throw an error if something's wrong" {
        { Import-GcSqlInstance "test-db2" "gs://gcsql-csharp-import-testing/testsql.gz" "newguestbook" } | Should Throw `
        "ERROR 1840 (HY000) at line 24: @@GLOBAL.GTID_PURGED can only be set when @@GLOBAL.GTID_EXECUTED is empty."
    }
}

Describe "Restart-GcSqlInstance" {
    It "should work and restart a test instance" {
        # A random number is used to avoid collisions with the speed of creating and deleting instances.
        $r = Get-Random
        $instance = "test-inst$r"
        gcloud sql instances create $instance --quiet 2>$null
        Restart-GcSqlInstance -Instance $instance

        $operations = Get-GcSqlOperation -Instance $instance
        $operations.Count | Should Be 2
        $operations[0].OperationType | Should Match "RESTART"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
        $operations[1].OperationType | Should Match "CREATE"

        gcloud sql instances delete $instance --quiet 2>$null
    }

     It "should work and restart a pipelined instance (instance and default projects same)" {
         # A random number is used to avoid collisions with the speed of creating and deleting instances.
        $r = Get-Random
        $instance = "test-inst$r"
        gcloud sql instances create $instance --quiet 2>$null
        Get-GcSqlInstance -Name $instance | Restart-GcSqlInstance

        $operations = Get-GcSqlOperation -Instance $instance
        $operations.Count | Should Be 2
        $operations[0].OperationType | Should Match "RESTART"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
        $operations[1].OperationType | Should Match "CREATE"

        gcloud sql instances delete $instance --quiet 2>$null
     }

    It "should work and restart a pipelined instance (instance and default projects differ)" {
        $nonDefaultProject = "asdf"
        $defaultProject = "gcloud-powershell-testing"

        # Set gcloud config to a non-default project (not gcloud-powershell-testing)
        gcloud config set project $nonDefaultProject

         # A random number is used to avoid collisions with the speed of creating and deleting instances.
        $r = Get-Random
        $instance = "test-inst$r"
        gcloud sql instances create $instance --project $defaultProject --quiet 2>$null
        Get-GcSqlInstance -Project $defaultProject -Name $instance | Restart-GcSqlInstance

        $operations = Get-GcSqlOperation -Project $defaultProject -Instance $instance
        $operations.Count | Should Be 2
        $operations[0].OperationType | Should Match "RESTART"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
        $operations[1].OperationType | Should Match "CREATE"

        gcloud sql instances delete $instance --project $defaultProject --quiet 2>$null

        # Reset gcloud config back to default project (gcloud-powershell-testing)
        gcloud config set project $defaultProject
     }
}

Describe "Start-GcSqlReplica" {
    # For these tests, test-db2 was used because an instance must have a database and a binarylog for it to be 
    # replicated. This kind of instance cannot be easily/quickly instantiated like those in other tests.
    $masterInstance = "test-db2"
    $2ndGenTier = "db-n1-standard-1"

    It "should work and start a test replica" {
        # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --quiet 2>$null
        Start-GcSqlReplica -Replica $replica

        $operations = Get-GcSqlOperation -Instance $replica
        $operations.Count | Should Be 2
        $operations[0].OperationType | Should Match "START_REPLICA"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
        $operations[1].OperationType | Should Match "CREATE_REPLICA"

        gcloud sql instances delete $replica --quiet 2>$null
    }

     It "should work and start a pipelined replica (replica and default projects same)" {
         # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --quiet 2>$null
        Get-GcSqlInstance -Name $replica | Start-GcSqlReplica

        $operations = Get-GcSqlOperation -Instance $replica
        $operations.Count | Should Be 2
        $operations[0].OperationType | Should Match "START_REPLICA"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
        $operations[1].OperationType | Should Match "CREATE_REPLICA"

        gcloud sql instances delete $replica --quiet 2>$null
     }

    It "should work and start a pipelined replica (replica and default projects differ)" {
        $nonDefaultProject = "asdf"
        $defaultProject = "gcloud-powershell-testing"

        # Set gcloud config to a non-default project (not gcloud-powershell-testing)
        gcloud config set project $nonDefaultProject

         # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --project $defaultProject --quiet 2>$null
        Get-GcSqlInstance -Project $defaultProject -Name $replica | Start-GcSqlReplica

        $operations = Get-GcSqlOperation -Project $defaultProject -Instance $replica
        $operations.Count | Should Be 2
        $operations[0].OperationType | Should Match "START_REPLICA"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
        $operations[1].OperationType | Should Match "CREATE_REPLICA"

        gcloud sql instances delete $replica --project $defaultProject --quiet 2>$null

        # Reset gcloud config back to default project (gcloud-powershell-testing)
        gcloud config set project $defaultProject
     }
}

Describe "Stop-GcSqlReplica" {
    # For these tests, test-db2 was used because an instance must have a database and a binarylog for it to be 
    # replicated. This kind of instance cannot be easily/quickly instantiated like those in other tests.
    $masterInstance = "test-db2"
    $2ndGenTier = "db-n1-standard-1"

    It "should work and stop a test replica" {
        # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --quiet 2>$null
        Stop-GcSqlReplica -Replica $replica

        $operations = Get-GcSqlOperation -Instance $replica
        $operations.Count | Should Be 2
        $operations[0].OperationType | Should Match "STOP_REPLICA"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
        $operations[1].OperationType | Should Match "CREATE_REPLICA"

        gcloud sql instances delete $replica --quiet 2>$null
    }

     It "should work and stop a pipelined replica (replica and default projects same)" {
         # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --quiet 2>$null
        Get-GcSqlInstance -Name $replica | Stop-GcSqlReplica

        $operations = Get-GcSqlOperation -Instance $replica
        $operations.Count | Should Be 2
        $operations[0].OperationType | Should Match "STOP_REPLICA"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
        $operations[1].OperationType | Should Match "CREATE_REPLICA"

        gcloud sql instances delete $replica --quiet 2>$null
     }

    It "should work and stop a pipelined replica (replica and default projects differ)" {
        $nonDefaultProject = "asdf"
        $defaultProject = "gcloud-powershell-testing"

        # Set gcloud config to a non-default project (not gcloud-powershell-testing)
        gcloud config set project $nonDefaultProject

         # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --project $defaultProject --quiet 2>$null
        Get-GcSqlInstance -Project $defaultProject -Name $replica | Stop-GcSqlReplica

        $operations = Get-GcSqlOperation -Project $defaultProject -Instance $replica
        $operations.Count | Should Be 2
        $operations[0].OperationType | Should Match "STOP_REPLICA"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
        $operations[1].OperationType | Should Match "CREATE_REPLICA"

        gcloud sql instances delete $replica --project $defaultProject --quiet 2>$null

        # Reset gcloud config back to default project (gcloud-powershell-testing)
        gcloud config set project $defaultProject
     }
}

Describe "Promote-GcSqlReplica" {
    # For these tests, test-db2 was used because an instance must have a database and a binarylog for it to be 
    # replicated. This kind of instance cannot be easily/quickly instantiated like those in other tests.
    $masterInstance = "test-db2"
    $2ndGenTier = "db-n1-standard-1"

    It "should work and promote a test replica (replica name as positional param)" {
        # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --quiet 2>$null
        Promote-GcSqlReplica $replica

        $operations = Get-GcSqlOperation -Instance $replica
        $operations.Count | Should Be 2
        $operations[0].OperationType | Should Match "PROMOTE_REPLICA"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
        $operations[1].OperationType | Should Match "CREATE_REPLICA"

        gcloud sql instances delete $replica --quiet 2>$null
    }

     It "should work and promote a pipelined replica (replica and default projects same)" {
         # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --quiet 2>$null
        Get-GcSqlInstance -Name $replica | Promote-GcSqlReplica

        $operations = Get-GcSqlOperation -Instance $replica
        $operations.Count | Should Be 2
        $operations[0].OperationType | Should Match "PROMOTE_REPLICA"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
        $operations[1].OperationType | Should Match "CREATE_REPLICA"

        gcloud sql instances delete $replica --quiet 2>$null
     }

    It "should work and promote a pipelined replica (replica and default projects differ)" {
        $nonDefaultProject = "asdf"
        $defaultProject = "gcloud-powershell-testing"

        # Set gcloud config to a non-default project (not gcloud-powershell-testing)
        gcloud config set project $nonDefaultProject

         # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --project $defaultProject --quiet 2>$null
        Get-GcSqlInstance -Project $defaultProject -Name $replica | Promote-GcSqlReplica

        $operations = Get-GcSqlOperation -Project $defaultProject -Instance $replica
        $operations.Count | Should Be 2
        $operations[0].OperationType | Should Match "PROMOTE_REPLICA"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
        $operations[1].OperationType | Should Match "CREATE_REPLICA"

        gcloud sql instances delete $replica --project $defaultProject --quiet 2>$null

        # Reset gcloud config back to default project (gcloud-powershell-testing)
        gcloud config set project $defaultProject
     }
}

Describe "Restore-GcSqlInstanceBackup" {
    # For these tests, test-db2 and mynewinstance were used because an instance must have backups enabled and a 
    # binarylog to be backed up. This kind of instance cannot be easily/quickly instantiated like those in other tests.
    $backupInstance1 = "test-db2"
    $backupInstance2 = "mynewinstance"

    $backupRunIds1 = (Get-GcSqlBackupRun -Instance $backupInstance1).Id
    $backupRunIds2 = (Get-GcSqlBackupRun -Instance $backupInstance2).Id

    $numRestoreOps1 = (Get-GcSqlOperation -Instance $backupInstance1 | where { $_.OperationType -eq "RESTORE_VOLUME" }).Count
    $numRestoreOps2 = (Get-GcSqlOperation -Instance $backupInstance2 | where { $_.OperationType -eq "RESTORE_VOLUME" }).Count

    It "should backup test-db2 to its own backup" {
        $backupRunId = $backupRunIds1[0]

        Restore-GcSqlInstanceBackup $backupRunId $backupInstance1 

        $operations = Get-GcSqlOperation -Instance $backupInstance1
        ($operations | where { $_.OperationType -eq "RESTORE_VOLUME" }).Count | Should Be ($numRestoreOps1 + 1)
        $operations[0].OperationType | Should Match "RESTORE_VOLUME"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
    }

     It "should backup pipelined test-db2 to its own backup (test-db2 and default projects same)" {
         $backupRunId = $backupRunIds1[1]

        Get-GcSqlInstance -Name $backupInstance1 | Restore-GcSqlInstanceBackup $backupRunId

        $operations = Get-GcSqlOperation -Instance $backupInstance1
        ($operations | where { $_.OperationType -eq "RESTORE_VOLUME" }).Count | Should Be ($numRestoreOps1 + 2)
        $operations[0].OperationType | Should Match "RESTORE_VOLUME"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
     }

    It "should backup pipelined test-db2 to its own backup (test-db2 and default projects differ)" {
        $nonDefaultProject = "asdf"
        $defaultProject = "gcloud-powershell-testing"

        # Set gcloud config to a non-default project (not gcloud-powershell-testing)
        gcloud config set project $nonDefaultProject

        $backupRunId = $backupRunIds1[2]

        Get-GcSqlInstance -Project $defaultProject -Name $backupInstance1 | Restore-GcSqlInstanceBackup $backupRunId

        $operations = Get-GcSqlOperation -Project $defaultProject -Instance $backupInstance1
        ($operations | where { $_.OperationType -eq "RESTORE_VOLUME" }).Count | Should Be ($numRestoreOps1 + 3)
        $operations[0].OperationType | Should Match "RESTORE_VOLUME"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""

        # Reset gcloud config back to default project (gcloud-powershell-testing)
        gcloud config set project $defaultProject
     }

    It "should backup pipelined mynewinstance to test-db2's backup" {
        $backupRunId = $backupRunIds1[0]

        Get-GcSqlInstance -Name $backupInstance2 | Restore-GcSqlInstanceBackup $backupRunId -BackupInstance $backupInstance1

        $operations = Get-GcSqlOperation -Instance $backupInstance2
        ($operations | where { $_.OperationType -eq "RESTORE_VOLUME" }).Count | Should Be ($numRestoreOps2 + 1)
        $operations[0].OperationType | Should Match "RESTORE_VOLUME"
        $operations[0].Status | Should Match "DONE"
        $operations[0].Error | Should Match ""
     }

    # Reset both instances to their own last backups
    Restore-GcSqlInstanceBackup $backupRunIds1[0] $backupInstance1
    Restore-GcSqlInstanceBackup $backupRunIds2[0] $backupInstance2
}

Describe "Update-GcSqlInstance" {
    # A random number is used to avoid collisions with the speed of creating and deleting instances.
    $r = Get-Random
    $instance = "test-inst$r"
    gcloud sql instances create $instance --tier "db-n1-standard-1" --activation-policy "ALWAYS" --quiet 2>$null
  
    It "should patch even if nothing changes" {
        $before = Get-GcSqlInstance -Name $instance
        $settingVer = $before.Settings.SettingsVersion
        $after = Update-GcSqlInstance $instance $settingVer
        ($after.Settings.SettingsVersion) | Should BeGreaterThan $settingVer
        ($after.SelfLink) | Should Be $before.SelfLink
    }

    It "should patch maintenance windows" {
        $day = Get-Random -Minimum 1 -Maximum 10
        $hour = Get-Random -Minimum 1 -Maximum 10
        $before = Get-GcSqlInstance -Name $instance
        $settingVer = $before.Settings.SettingsVersion
        $after = Update-GcSqlInstance $instance $settingVer -MaintenanceWindowDay $day -MaintenanceWindowHour $hour
        ($after.Settings.SettingsVersion) | Should BeGreaterThan $settingVer
        ($after.Settings.MaintenanceWindow.Day) | Should Be $day
        ($after.Settings.MaintenanceWindow.Hour) | Should Be $hour
    }

    It "should patch backup configurations" {
        $before = Get-GcSqlInstance -Name $instance
        $settingVer = $before.Settings.SettingsVersion
        $after = Update-GcSqlInstance $instance $settingVer -BackupBinaryLogEnabled $true -BackupEnabled $true  -BackupStartTime "22:00"
        ($after.Settings.SettingsVersion) | Should BeGreaterThan $settingVer
        ($after.Settings.BackupConfiguration.BinaryLogEnabled) | Should Be true
        ($after.Settings.BackupConfiguration.Enabled) | Should Be true
        ($after.Settings.BackupConfiguration.StartTime) | Should Be "22:00"
    }

    It "should patch IP configuations" {
        $before = Get-GcSqlInstance -Name $instance
        $settingVer = $before.Settings.SettingsVersion
        $after = Update-GcSqlInstance $instance $settingVer -IpConfigRequireSsl $False
        ($after.Settings.SettingsVersion) | Should BeGreaterThan $settingVer
        ($after.Settings.IpConfiguration.RequireSsl) | Should Be false
    }

    It "should patch Location Preferences" {
        $before = Get-GcSqlInstance -Name $instance
        $settingVer = $before.Settings.SettingsVersion
        $after = Update-GcSqlInstance $instance $settingVer -LocationPreferenceZone "us-central1-a"
        ($after.Settings.SettingsVersion) | Should BeGreaterThan $settingVer
        ($after.Settings.LocationPreference.Zone) | Should be "us-central1-a"
    }

    It "should be able to take in an instance" {
        $before = Get-GcSqlInstance -Name $instance
        $settingVer = $before.Settings.SettingsVersion
        $after = Update-GcSqlInstance $settingVer -InstanceObject $before
        ($after.Settings.SettingsVersion) | Should BeGreaterThan $settingVer
    }

    It "should update correctly" {
        $before = Get-GcSqlInstance -Name $instance
        $settingVer = $before.Settings.SettingsVersion
        $after = Update-GcSqlInstance $instance $settingVer -Update
        ($after.Settings.SettingsVersion) | Should BeGreaterThan $settingVer
        ($after.Settings.MaintenanceWindow.Day) | Should Be 0
    }

    gcloud sql instances delete $instance --quiet 2>$null
}

Reset-GCloudConfig $oldActiveConfig $configName
