. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
$project, $_, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcSqlInstance" {
    BeforeAll {
        $r = Get-Random
        # A random number is used to avoid collisions with the speed of creating
        # and deleting instances.
        $instance = "test-inst$r"
        Add-GcSqlInstance $instance
    }

    AfterAll {
        Remove-GcSqlInstance $instance
    }

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
}

Describe "Add-GcSqlInstance" {

    It "should work" {
        $r = Get-Random
        # A random number is used to avoid collisions with the speed of creating
        # and deleting instances.
        $instance = "test-inst$r"
        try {
            $instances = Get-GcSqlInstance
            $setting = New-GcSqlSettingConfig "db-n1-standard-1"
            $config = New-GcSqlInstanceConfig $instance -SettingConfig $setting
            Add-GcSqlInstance $config
            $newInstances = Get-GcSqlInstance
            ($instances.Name -contains $instance) | Should Be false
            ($newInstances.Name -contains $instance) | Should Be true
        }
        finally {
            Remove-GcSqlInstance $instance
        }
    }
    
    It "should be able to make a default with just a name" -Skip {
        $r = Get-Random
        # A random number is used to avoid collisions with the speed of creating
        # and deleting instances.
        $instance = "test-inst$r"
        try {
            $instances = Get-GcSqlInstance
            Add-GcSqlInstance $instance
            $newInstances = Get-GcSqlInstance
            ($instances.Name -contains $instance) | Should Be false
            ($newInstances.Name -contains $instance) | Should Be true
        }
        finally {
            Remove-GcSqlInstance $instance
        }
    }

    It "should be able to reflect custom settings" -Skip {
        $r = Get-Random
        # A random number is used to avoid collisions with the speed of creating
        # and deleting instances.
        $instance = "test-inst$r"
        try {
            $setting = New-GcSqlSettingConfig "db-n1-standard-1" -MaintenanceWindowDay 1 -MaintenanceWindowHour 2
            $config = New-GcSqlInstanceConfig $instance -SettingConfig $setting
            Add-GcSqlInstance $config
            $myInstance = Get-GcSqlInstance $instance
            $myInstance.Settings.MaintenanceWindow.Day | Should Be 1
            $myInstance.Settings.MaintenanceWindow.Hour | Should Be 2
        }
        finally {
            Remove-GcSqlInstance $instance
        }
    }

    It "should be able to create a read-replica instance" -Pending {
        $r = Get-Random
        # A random number is used to avoid collisions with the speed of creating
        # and deleting instances.
        $instance = "test-inst$r"
        try {
            $setting = New-GcSqlSettingConfig "db-n1-standard-1" 
            $replicaConfig = New-GcSqlInstanceReplicaConfig
            $config = New-GcSqlInstanceConfig $instance -SettingConfig $setting `
                -ReplicaConfig $replicaConfig -MasterInstanceName "test-db4"
            Add-GcSqlInstance $config

            $newInstances = Get-GcSqlInstance
            ($newInstances.Name -contains $instance) | Should Be true
            $myInstance = Get-GcSqlInstance $instance
            $myInstance.MasterInstanceName | Should Be "gcloud-powershell-testing:test-db4"
            $myInstance.InstanceType | Should Be "READ_REPLICA_INSTANCE"
        }
        finally {
            Remove-GcSqlInstance $instance
        }
    }
}

Describe "Remove-GcSqlInstance" {
    It "should work" {
        $r = Get-Random
        # A random number is used to avoid collisions with the speed of creating
        # and deleting instances.
        $instance = "test-inst$r"
        try {
            Add-GcSqlInstance $instance
            $instances = Get-GcSqlInstance
            ($instances.Name -contains $instance) | Should Be true
        }
        finally {
            Remove-GcSqlInstance $instance
        }
        $instances = Get-GcSqlInstance
        ($instances.Name -contains $instance) | Should Be false
    }

    It "should be able to take a pipelined Instance" -Skip {
        $r = Get-Random
        # A random number is used to avoid collisions with the speed of creating
        # and deleting instances.
        $instance = "test-inst$r"
        try {
            Add-GcSqlInstance $instance
            $instances = Get-GcSqlInstance
            ($instances.Name -contains $instance) | Should Be true
        }
        finally {
            Get-GcSqlInstance $instance | Remove-GcSqlInstance
        }
        $instances = Get-GcSqlInstance
        ($instances.Name -contains $instance) | Should Be false
    }

    It "shouldn't delete anything that doesn't exist" {
        { Remove-GcSqlInstance "should-fail" } | Should Throw "The client is not authorized to make this request. [403]"
    }
}

Describe "Export-GcSqlInstance" {
    AfterAll {
        gsutil -q rm gs://gcsql-instance-testing/*
    }

    # For these tests, test-db4 was used because an instance must have a populated database for it to work.
    # A specific nondescript bucket was also used because the permissions have to be set correctly
    $r = Get-Random
    # A random number is used to avoid collisions with the speed of creating
    # and deleting instances.
    $instance = "test-db4"


    It "should export an applicable SQL file" {
        $beforeObjects = gsutil ls gs://gcsql-instance-testing
        ($beforeObjects -contains "gs://gcsql-instance-testing/testsql$r.gz") | Should Be false
        Export-GcSqlInstance $instance "gs://gcsql-instance-testing/testsql$r.gz"
        $afterObjects = gsutil ls gs://gcsql-instance-testing
        ($afterObjects -contains "gs://gcsql-instance-testing/testsql$r.gz") | Should Be true
    }

    It "should export an applicable CSV file" {
        $beforeObjects = gsutil ls gs://gcsql-instance-testing
        ($beforeObjects -contains "gs://gcsql-instance-testing/testcsv$r.csv") | Should Be false
        Export-GcSqlInstance $instance "gs://gcsql-instance-testing/testcsv$r.csv" "SELECT * FROM guestbook.entries"
        $afterObjects = gsutil ls gs://gcsql-instance-testing
        ($afterObjects -contains "gs://gcsql-instance-testing/testcsv$r.csv") | Should Be true
    }

    It "should be able to export a specific SQL file" {
        $beforeObjects = gsutil ls gs://gcsql-instance-testing
        ($beforeObjects -contains "gs://gcsql-instance-testing/testothersql$r.gz") | Should Be false
        Export-GcSqlInstance $instance "gs://gcsql-instance-testing/testothersql$r.gz" -Database "guestbook","guestbook2" 
        $afterObjects = gsutil ls gs://gcsql-instance-testing
        ($afterObjects -contains "gs://gcsql-instance-testing/testothersql$r.gz") | Should Be true
    }

    It "should be able to export a specific CSV file" {
        $beforeObjects = gsutil ls gs://gcsql-instance-testing
        ($beforeObjects -contains "gs://gcsql-instance-testing/testothercsv$r.csv") | Should Be false
        Export-GcSqlInstance $instance "gs://gcsql-instance-testing/testothercsv$r.csv" -Database "guestbook" "SELECT * FROM entries"
        $afterObjects = gsutil ls gs://gcsql-instance-testing
        ($afterObjects -contains "gs://gcsql-instance-testing/testothercsv$r.csv") | Should Be true
    }

}

Describe "Import-GcSqlInstance" {
    # For these tests, test-db4 was used because an instance must have a database for it to work.
    # A specific nondescript bucket with nondescript files was also used 
    # because the permissions have to be set correctly.
    $instance = "test-db4"

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
        { Import-GcSqlInstance $instance "gs://gcsql-csharp-import-testing/testsqlS3" "newguestbook" } | Should not Throw
    }

    It "should be able to import a regular CSV file" {
        { Import-GcSqlInstance $instance "gs://gcsql-csharp-import-testing/testsql.csv" "newguestbook" "entries" } | Should not Throw
    }

    It "should throw an error if something's wrong" -Skip {
        { Import-GcSqlInstance $instance "gs://gcsql-csharp-import-testing/testsqlS" "newguestbook" } | Should Throw `
        "ERROR 1227 (42000) at line 18: Access denied; you need (at least one of) the SUPER privilege(s) for this operation"
    }

    It "should import a local file by uploading it to GCS for a local file upon completion" -Skip {
        $oldBuckets = Get-GcsBucket
        { Import-GcSqlInstance $instance "$PSScriptRoot\sample-table.csv" "newguestbook" "entries" } |
            Should not Throw
        # The cmdlet creates a new Google Cloud Storage bucket so that the data can be imported. 
        # We want to make sure this bucket is deleted after.
        $newBuckets = Get-GcsBucket
        $oldBuckets.Count | Should Be $newBuckets.Count
    }

    It "should delete the bucket for a local file upon a file error" -Skip {
        $oldBuckets = Get-GcsBucket
        { Import-GcSqlInstance $instance "$PSScriptRoot\filenotexist" "newguestbook" "entries" } | 
        Should Throw "Could not find file '$PSScriptRoot\filenotexist'"
        $newBuckets = Get-GcsBucket
        $oldBuckets.Count | Should Be $newBuckets.Count
    }

    It "should delete the bucket for a local file upon a instance error" -Skip {
        $oldBuckets = Get-GcsBucket
        { Import-GcSqlInstance $instance "$PSScriptRoot\sample-table.csv" "newguestbook" "tablenotexist" } | 
        Should Throw "Error 1146: Table 'newguestbook.tablenotexist' doesn't exist"
        $newBuckets = Get-GcsBucket
        $oldBuckets.Count | Should Be $newBuckets.Count
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
