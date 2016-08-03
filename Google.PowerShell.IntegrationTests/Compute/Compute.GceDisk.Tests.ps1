. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

# Delete all disks associated with a project.
function Remove-ProjectDisks($project) {
    Write-Host "Deleting all GCE disks for $project..."
    $disks = Get-GceDisk -Project $project
    foreach ($disk in $disks) {
        gcloud compute disks delete --project $project $disk.Name --zone $disk.Zone --quiet 2>$null
    }
}

Describe "Get-GceDisk" {
    Remove-ProjectDisks($project)
    # Create test disks.
    gcloud compute disks create --project $project "test-disk-1" --zone "us-central1-a" --size 20 --quiet 2>$null
    gcloud compute disks create --project $project "test-disk-2" --zone "us-central1-a" --size 20 --quiet 2>$null
    gcloud compute disks create --project $project "test-disk-1" --zone "us-central1-b" --size 20 --quiet 2>$null

    It "should work" {
        $disks = Get-GceDisk -Project $project
        $disks.Length | Should Be 3
    }

    It "should support filtering by Zone" {
        $disks = Get-GceDisk -Project $project -Zone "us-central1-a"
        $disks.Length | Should Be 2

        # Substrings work too.
        $disks = Get-GceDisk -Project $project -Zone "us-central1"
        $disks.Length | Should Be 3
    }

    It "should support filtering by Name" {
        $disks = Get-GceDisk -Project $project -DiskName "test-disk-1"
        $disks.Length | Should Be 2
    }

    It "should support filtering by Name and Zone" {
        $disks = Get-GceDisk -Project $project -DiskName "test-disk-1" -Zone "us-central1-a"
        $disks.Length | Should Be 1
    }

    It "should fail to return non-existing disks" {
        # If querying multiple disks, may return 0 elements.
        $disks = Get-GceDisk -Project $project -DiskName "xxxxx"
        $disks | Should BeNullOrEmpty

        # If getting a specific disk (zone and disk name) fail.
        { Get-GceDisk -Project $project -Zone "us-central1-a" "xxxxx" } |
             Should Throw "404"
    }

    Remove-ProjectDisks($project)
}

Describe "New-GceDisk" {
    $r = Get-Random
    $diskName = "test-disk-$r"
    $image = Get-GceImage -Family "windows-2012-r2" "windows-cloud"

    It "should work for empty disk" {
        $disk = New-GceDisk `
            -Project $project `
            -DiskName $diskName `
            -Description "$r" `
            -SizeGb 215 `
            -DiskType "pd-ssd" `
            -Zone "us-central1-c"

        # Confirmed the return object sets all the expected fields.
        $disk.Name | Should MatchExactly $diskName
        $disk.Description | Should MatchExactly 
        $disk.SizeGb | Should BeExactly 215
        $disk.Type | Should Match "pd-ssd"
        $disk.Zone | Should Match "us-central1-c"

        # Confirm the values were actually set, too.
        $getdisk = Get-GceDisk -Project $project -DiskName $diskName
        (Compare-Object $disk $getdisk) | Should BeNullOrEmpty

        Remove-GceDisk $disk
    }

    It "should work for image" {
        $disk = New-GceDisk $diskName $image
        $disk.Name | Should MatchExactly $diskName
        $disk.SourceImage | Should Match "windows-cloud"

        Remove-GceDisk $disk
    }

    It "should work for image with pipeline" {
        $disk = $image | New-GceDisk $diskName
        $disk.Name | Should MatchExactly $diskName
        $disk.SourceImage | Should Match "windows-cloud"

        Remove-GceDisk $disk
    }

    Context "with snapshot" {
        $snapshotSource = New-GceDisk "snapshot-source-$r" -SizeGb 1
        $snapshot = $snapshotSource | Add-GceSnapshot -Name "test-snapshot-disk-source-$r"

        It "should work for snapshot" {
            $disk = New-GceDisk $diskName $snapshot
            $disk.Name | Should MatchExactly $diskName
            $disk.SourceSnapshot | Should Match $snapshot.Name

            Remove-GceDisk $disk
        }

        It "should work for snapshot on pipeline" {
            $disk = $snapshot | New-GceDisk $diskName
            $disk.Name | Should MatchExactly $diskName
            $disk.SourceSnapshot | Should Match $snapshot.Name

            Remove-GceDisk $disk
        }

        Remove-GceDisk $snapshotSource
        Remove-GceSnapshot $snapshot
    }

    It "should fail with invalid disk names" {
        { New-GceDisk -Project $project -Zone "us-central1-d" -DiskName "totally invalid!" } `
            | Should Throw "Invalid value for field 'resource.name'"
    }

    Remove-ProjectDisks($project)
}

Describe "Resize-GceDisk" {
    $diskName = "resize-test"
    $zone = "us-central1-b"
    Remove-ProjectDisks($project)
    gcloud compute disks create --project $project $diskName --zone $zone --size 20 --quiet 2>$null
 
    It "should fail with invalid disk names" {
        $disk = Get-GceDisk -Project $project -Zone $zone $diskName
        $disk.SizeGb | Should BeExactly 20
        
        $disk = Resize-GceDisk -Project $project -Zone $zone $diskName 1337
        $disk.SizeGb | Should BeExactly 1337

        $disk = Get-GceDisk -Project $project -Zone $zone $diskName
        $disk.SizeGb | Should BeExactly 1337
    }

    It "should fail if the disk size is smaller" {
        { Resize-GceDisk -Project $project -Zone $zone $diskName -NewSizeGb 10 } |
             Should Throw "existing size '1337'. [400]"
    }

    Remove-ProjectDisks($project)
}

Describe "Remove-GceDisk" {
    $zone = "us-central1-a"

    It "should work" {
        $diskName = "remove-disk-test"
        New-GceDisk -Project $project -Zone $zone -DiskName $diskName

        $disk = Get-GceDisk -Project $project -Zone $zone $diskName
        Remove-GceDisk -Project $project -Zone $zone $diskName
        
        { Get-GceDisk -Project $project -Zone $zone $diskName } | Should Throw "404"
    }

    It "should work with object" {
        $diskName = "remove-disk-test"
        $disk = New-GceDisk -Project $project -Zone $zone -DiskName $diskName

        Remove-GceDisk $disk
        
        { Get-GceDisk -Project $project -Zone $zone $diskName } | Should Throw "404"
    }
    
    It "should work with object by pipeline" {
        $diskName = "remove-disk-test"
        New-GceDisk -Project $project -Zone $zone -DiskName $diskName |
            Remove-GceDisk
        
        { Get-GceDisk -Project $project -Zone $zone $diskName } | Should Throw "404"
    }

    It "should fail to delete non-existant disks" {
        { Remove-GceDisk -Project $project -Zone $zone "does-not-exist" } | Should Throw "was not found [404]"
    }

    # TODO(chrsmith): Confirm the error case if you try to delete a GCE disk and
    # the disk is in-use by a VM.
    Remove-ProjectDisks($project)
}

Reset-GCloudConfig $oldActiveConfig $configName
