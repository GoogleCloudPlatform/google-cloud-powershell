. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

Describe "Get-GceDisk" {
    # Cleanup from a previous run if necessary.
    gcloud compute disks delete --project $project "test-disk-1" --zone "us-central1-a" --quiet
    gcloud compute disks delete --project $project "test-disk-2" --zone "us-central1-a" --quiet
    gcloud compute disks delete --project $project "test-disk-1" --zone "us-central1-b" --quiet
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
        $disks = Get-GceDisk -Project $project -DiskName "xxxxx"
        $disks | Should BeNullOrEmpty
    }

    # Cleanup of test disks.
    gcloud compute disks delete --project $project "test-disk-1" --zone "us-central1-a" --quiet
    gcloud compute disks delete --project $project "test-disk-2" --zone "us-central1-a" --quiet
    gcloud compute disks delete --project $project "test-disk-1" --zone "us-central1-b" --quiet
}
