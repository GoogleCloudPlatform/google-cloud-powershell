. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"
# Delete all disks associated with the project.
$disks = Get-GceDisk $project
Write-Host "Deleting any existing GCE disks..."
foreach ($disk in $disks) {
    gcloud compute disks delete --project $project $disk.Name --zone $disk.Zone --quiet
}

Describe "Get-GceDisk" {
    # Create test disks.
    gcloud compute disks create --project $project "test-disk-1" --zone "us-central1-a" --size 20 --quiet
    gcloud compute disks create --project $project "test-disk-2" --zone "us-central1-a" --size 20 --quiet
    gcloud compute disks create --project $project "test-disk-1" --zone "us-central1-b" --size 20 --quiet

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
        { Get-GceDisk $project "us-central1-a" "xxxxx" } `
            | Should Throw "404"
    }

    # Cleanup of test disks.
    gcloud compute disks delete --project $project "test-disk-1" --zone "us-central1-a" --quiet
    gcloud compute disks delete --project $project "test-disk-2" --zone "us-central1-a" --quiet
    gcloud compute disks delete --project $project "test-disk-1" --zone "us-central1-b" --quiet
}

Describe "New-GceDisk" {
    It "should work" {
        $r = Get-Random
        $disk = New-GceDisk `
            -Project $project `
            -DiskName "test-disk-$r" `
            -Description "$r" `
            -SizeGb 215 `
            -DiskType "pd-ssd" `
            -SourceImage "projects/windows-cloud/global/images/family/windows-2012-r2" `
            -Zone "us-central1-c"

        # Confirmed the return object sets all the expected fields.
        $disk.Name | Should MatchExactly "test-disk-$r"
        $disk.Description | Should MatchExactly 
        $disk.SizeGb | Should BeExactly 215
        $disk.Type | Should Match "pd-ssd"
        $disk.Zone | Should Match "us-central1-c"

        # Confirm the values were actually set, too.
        $disk = Get-GceDisk -Project $project -DiskName "test-disk-$r"

        $disk.Name | Should MatchExactly "test-disk-$r"
        $disk.Description | Should MatchExactly 
        $disk.SizeGb | Should BeExactly 215
        $disk.Type | Should Match "pd-ssd"
        $disk.Zone | Should Match "us-central1-c"

        # TODO(chrsmith): $disk.Zone is a URI, which will fail when the request is
        # made. This is a wart on the API, since it should accept the URI form of zones.
        Remove-GceDisk -Project $project -Zone "us-central1-c" -DiskName $disk.Name
    }

    It "should fail with invalid disk names" {
        { New-GceDisk -Project $project -Zone "us-central1-d" -DiskName "totally invalid!" } `
            | Should Throw "Invalid value for field 'resource.name'"
    }
}

Describe "Resize-GceDisk" {
    $diskName = "resize-test"
    $zone = "us-central1-b"
    gcloud compute disks delete --project $project $diskName --zone $zone --quiet
    gcloud compute disks create --project $project $diskName --zone $zone --size 20 --quiet
 
    It "should fail with invalid disk names" {
        $disk = Get-GceDisk $project $zone $diskName
        $disk.SizeGb | Should BeExactly 20
        
        $disk = Resize-GceDisk $project $zone $diskName 1337
        $disk.SizeGb | Should BeExactly 1337

        $disk = Get-GceDisk $project $zone $diskName
        $disk.SizeGb | Should BeExactly 1337
    }

    It "should fail if the disk size is smaller" {
        { Resize-GceDisk $project $zone $diskName -NewSizeGb 10 } `
            | Should Throw "must be larger than existing size '1337'. [400]"
    }

    Remove-GceDisk $project $zone $diskName
}

Describe "Remove-GceDisk" {
    $zone = "us-central1-a"

    It "should work" {
        $diskName = "remove-disk-test"
        New-GceDisk -Project $project -Zone $zone -DiskName $diskName

        $disk = Get-GceDisk $project $zone $diskName
        Remove-GceDisk $project $zone $diskName
        
        { Get-GceDisk $project $zone $diskName } `
            | Should Throw "404"
    }

    It "should fail to delete non-existant disks" {
        { Remove-GceDisk $project $zone "does-not-exist" } `
            | Should Throw "was not found [404]"
    }

    # TODO(chrsmith): Confirm the error case if you try to delete a GCE disk and
    # the disk is in-use by a VM.
}