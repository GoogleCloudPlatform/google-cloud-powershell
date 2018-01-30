. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
$project, $_, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Start-GcSqlReplica" {
    # For these tests, test-db4 was used because an instance must have a database and a binarylog for it to be
    # replicated. This kind of instance cannot be easily/quickly instantiated like those in other tests.
    $masterInstance = "test-db4"
    $2ndGenTier = "db-n1-standard-1"

    It "should work and start a test replica" {
        # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --quiet 2>$null
        try {
            Start-GcSqlReplica -Replica $replica

            $operations = Get-GcSqlOperation -Instance $replica
            $operations.Count | Should Be 2
            $operations[0].OperationType | Should Match "START_REPLICA"
            $operations[0].Status | Should Match "DONE"
            $operations[0].Error | Should Match ""
            $operations[1].OperationType | Should Match "CREATE_REPLICA"
        }
        finally {
            gcloud sql instances delete $replica --quiet 2>$null
        }
    }

     It "should work and start a pipelined replica (replica and default projects same)" -Skip {
         # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --quiet 2>$null
        try {
            Get-GcSqlInstance -Name $replica | Start-GcSqlReplica

            $operations = Get-GcSqlOperation -Instance $replica
            $operations.Count | Should Be 2
            $operations[0].OperationType | Should Match "START_REPLICA"
            $operations[0].Status | Should Match "DONE"
            $operations[0].Error | Should Match ""
            $operations[1].OperationType | Should Match "CREATE_REPLICA"
        }
        finally {
            gcloud sql instances delete $replica --quiet 2>$null
        }
     }

    It "should work and start a pipelined replica (replica and default projects differ)" -Skip {
        $nonDefaultProject = "asdf"
        $defaultProject = "gcloud-powershell-testing"

        # Set gcloud config to a non-default project (not gcloud-powershell-testing)
        gcloud config set project $nonDefaultProject 2>$null

         # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --project $defaultProject --quiet 2>$null
        try {
            Get-GcSqlInstance -Project $defaultProject -Name $replica | Start-GcSqlReplica

            $operations = Get-GcSqlOperation -Project $defaultProject -Instance $replica
            $operations.Count | Should Be 2
            $operations[0].OperationType | Should Match "START_REPLICA"
            $operations[0].Status | Should Match "DONE"
            $operations[0].Error | Should Match ""
            $operations[1].OperationType | Should Match "CREATE_REPLICA"
        }
        finally {
            gcloud sql instances delete $replica --project $defaultProject --quiet 2>$null

            # Reset gcloud config back to default project (gcloud-powershell-testing)
            gcloud config set project $defaultProject 2>$null
        }
     }
}

Describe "Stop-GcSqlReplica" {
    # For these tests, test-db4 was used because an instance must have a database and a binarylog for it to be
    # replicated. This kind of instance cannot be easily/quickly instantiated like those in other tests.
    $masterInstance = "test-db4"
    $2ndGenTier = "db-n1-standard-1"

    It "should work and stop a test replica" {
        # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --quiet 2>$null
        try {
            Stop-GcSqlReplica -Replica $replica

            $operations = Get-GcSqlOperation -Instance $replica
            $operations.Count | Should Be 2
            $operations[0].OperationType | Should Match "STOP_REPLICA"
            $operations[0].Status | Should Match "DONE"
            $operations[0].Error | Should Match ""
            $operations[1].OperationType | Should Match "CREATE_REPLICA"
        }
        finally {
            gcloud sql instances delete $replica --quiet 2>$null
        }
    }

     It "should work and stop a pipelined replica (replica and default projects same)" -Skip {
         # A random number is used to avoid collisions with the speed of creating and deleting instances/replicas.
        $r = Get-Random
        $replica = "test-repl$r"
        gcloud sql instances create $replica --master-instance-name $masterInstance --tier $2ndGenTier --replication SYNCHRONOUS --quiet 2>$null
        try {
            Get-GcSqlInstance -Name $replica | Stop-GcSqlReplica

            $operations = Get-GcSqlOperation -Instance $replica
            $operations.Count | Should Be 2
            $operations[0].OperationType | Should Match "STOP_REPLICA"
            $operations[0].Status | Should Match "DONE"
            $operations[0].Error | Should Match ""
            $operations[1].OperationType | Should Match "CREATE_REPLICA"
        }
        finally {
            gcloud sql instances delete $replica --quiet 2>$null
        }
     }

    It "should work and stop a pipelined replica (replica and default projects differ)" -Skip {
        $nonDefaultProject = "asdf"
        $defaultProject = "gcloud-powershell-testing"

        # Set gcloud config to a non-default project (not gcloud-powershell-testing)
        gcloud config set project $nonDefaultProject 2>$null

        try {
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
        }
        finally {
            gcloud sql instances delete $replica --project $defaultProject --quiet 2>$null

            # Reset gcloud config back to default project (gcloud-powershell-testing)
            gcloud config set project $defaultProject 2>$null
        }
     }
}

Reset-GCloudConfig $oldActiveConfig $configName