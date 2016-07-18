﻿. $PSScriptRoot\..\GcloudCmdlets.ps1
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
        Get-GcSqlInstance -Name $instance |  Restart-GcSqlInstance

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
        Get-GcSqlInstance -Project $defaultProject -Name $instance |  Restart-GcSqlInstance

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

Reset-GCloudConfig $oldActiveConfig $configName
