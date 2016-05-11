. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

Describe "Get-GceDisk" {

    # Create several test disks.
    New-GceDisk $project -Zone "us-central1-a" -DiskName "test-disk-1" -SizeGb 10
    New-GceDisk $project -Zone "us-central1-a" -DiskName "test-disk-2" -SizeGb 10
    # Same name, different zone.
    New-GceDisk $project -Zone "us-central1-b" -DiskName "test-disk-1" -SizeGb 10

    It "should work" {
        $disks = Get-GceDisk -Project $project
        $disks.Length | Should Be 3
    }

    It "should support filtering by Zone" {
        $disks = Get-GceDisk -Project $project -Zone "us-central1-a"
        $disks.Length | Should Be 2
    }

    It "should support filtering by Name" {
        $disks = Get-GceDisk -Project $project -DiskName "test-disk-1"
        $disks.Length | Should Be 2
    }

    It "should fail to return non-existing disks" {
        $disks = Get-GceDisk -Project $project -DiskName "xxxxx"
        $disks | Should Be {}
    }

    # Cleanup of test disks.
    Remove-GceDisk $project -Zone "us-central1-a" -DiskName "test-disk-1"
    Remove-GceDisk $project -Zone "us-central1-a" -DiskName "test-disk-2"
    Remove-GceDisk $project -Zone "us-central1-b" -DiskName "test-disk-1"
}
