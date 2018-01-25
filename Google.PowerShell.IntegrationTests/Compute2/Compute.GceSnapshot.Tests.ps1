. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

$r = Get-Random

$diskName = "test-image-disk-$r"
Describe "Add-GceSnapshot" {
    $disk = New-GceDisk -DiskName $diskName -SizeGb 1

    $snapshotName = "test-add-snapshot-$r"

    It "should fail on wrong project" {
        { Add-GceSnapshot $diskName -Project "asdf" } | Should Throw 403
    }

    It "should fail on non-existant disk" {
        { Add-GceSnapshot "not-a-real-disk" } | Should Throw 404
    }

    It "should work with disk name" {
        $snapshot = Add-GceSnapshot $diskName -Name $snapshotName -Description "for testing $r"
        ($snapshot | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Snapshot }
        $snapshot.Name | Should Be $snapshotName
        $snapshot.Description | Should Be "for testing $r"
        { Get-GceSnapshot $snapshot.Name } | Should Not Throw
    }

    It "should work with disk object" {
        $snapshot = Add-GceSnapshot $disk
        $snapshot | Should Not BeNullOrEmpty
        { Get-GceSnapshot $snapshot.Name } | Should Not Throw
    }

    It "should work with disk object on pipeline" {
        $snapshot = $disk | Add-GceSnapshot
        $snapshot | Should Not BeNullOrEmpty
        { Get-GceSnapshot $snapshot.Name } | Should Not Throw
    }

    It "should work with -GuestFlush" {
        $snapshot = Add-GceSnapShot $diskName -GuestFlush
        $snapshot | Should Not BeNullOrEmpty
        { Get-GceSnapshot $snapshot.Name } | Should Not Throw
    }
    
    Remove-GceDisk $disk
    Get-GceSnapshot | Remove-GceSnapshot
}

Describe "Get-GceSnapshot" {
    $disk = New-GceDisk -DiskName $diskName -SizeGb 1

    $snapshotName = "test-get-snapshot-$r"
    $snapshotName2 = "test-get-snapshot2-$r"

    It "should fail on wrong project" {
        { Get-GceSnapshot $snapshotName -Project "asdf" } | Should Throw 403
    }

    It "should fail on non-existant snapshot" {
        { Get-GceSnapshot "not-a-real-snapshot" } | Should Throw 404
    }

    It "should work with no snapshots" {
        $snapshots = Get-GceSnapshot
        $snapshots | Should BeNullOrEmpty
    }

    $disk | Add-GceSnapshot -Name $snapshotName
    $disk | Add-GceSnapshot -Name $snapshotName2

    It "should get all project snapshots" {
        $snapshots = Get-GceSnapshot
        ($snapshots | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Snapshot }
        $snapshots.Count | Should Be 2
        ($snapshosts.Name -eq $snapshotName).Count | Should Be 1
        ($snapshosts.Name -eq $snapshotName2).Count | Should Be 1
    }

    It "should get snapshot by name" {
        $snapshot = Get-GceSnapshot $snapshotName
        $snapshot.Count | Should Be 1
        $snapshot.Name | Should Be $snapshotName
    }
    
    Remove-GceDisk $disk
    Get-GceSnapshot | Remove-GceSnapshot
}

Describe "Remove-GceSnapshot" {
    $disk = New-GceDisk -DiskName $diskName -SizeGb 1
    $snapshotName = "test-get-snapshot-$r"

    It "should fail on wrong project" {
        { Remove-GceSnapshot $snapshotName -Project "asdf" } | Should Throw 403
    }

    It "should fail on non-existant snapshot" {
        { Remove-GceSnapshot "not-a-real-snapshot" } | Should Throw 404
    }

    Context "remove successes" {

        BeforeEach {
            $disk | Add-GceSnapshot -Name $snapshotName
        }

        It "should work by name" {
            Remove-GceSnapshot $snapshotName
            { Get-GceSnapshot $snapshotName } | Should Throw 404
        }

        It "should work by object" {
            $snapshot = Get-GceSnapshot $snapshotName
            Remove-GceSnapshot $snapshot
            { Get-GceSnapshot $snapshotName } | Should Throw 404
        }

        It "should work by object with pipeline" {
            Get-GceSnapshot $snapshotName | Remove-GceSnapshot
            { Get-GceSnapshot $snapshotName } | Should Throw 404
        }
    }

    Remove-GceDisk $disk
}

Reset-GCloudConfig $oldActiveConfig $configName
