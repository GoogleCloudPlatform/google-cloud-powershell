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

Reset-GCloudConfig $oldActiveConfig $configName
