. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets


Describe "New-GceAttachedDiskConfig" {
    It "should set defaults for persistant disks" {
        $disk = New-GceAttachedDiskConfig -Source "SomePersistanatDiskUri"
        $disk.AutoDelete | Should Be $false
        $disk.Boot | Should Be $false
        $disk.DeviceName | Should BeNullOrEmpty
        $disk.Index | Should BeNullOrEmpty
        $disk.Initializeparams | Should BeNullOrEmpty
        $disk.Interface__ | Should Be SCSI
        $disk.Mode | Should Be READ_WRITE
        $disk.Source | Should Be SomePersistanatDiskUri
    }

    It "should set defaults for new disk" {
        $disk = New-GceAttachedDiskConfig -SourceImage "SomeDiskImageUri"
        $disk.AutoDelete | Should Be $false
        $disk.Boot | Should Be $false
        $disk.DeviceName | Should BeNullOrEmpty
        $disk.Index | Should BeNullOrEmpty
        $disk.Interface__ | Should Be SCSI
        $disk.Mode | Should Be READ_WRITE
        $disk.Source | Should BeNullOrEmpty
        $params = $disk.InitializeParams
        $params.SourceImage | Should Be SomeDiskImageUri
        $params.DiskName | Should BeNullOrEmpty
        $params.DiskSizeGb | Should BeNullOrEmpty
        $params.DiskType | Should BeNullOrEmpty
    }

    It "should fail with both source and sourceImage" {
        { New-GceAttachedDiskConfig -SourceImage "image" -Source "source"} |
            Should Throw "Parameter set cannot be resolved"
    }

    It "should set values for persistant disks" {
        $disk = New-GceAttachedDiskConfig -Source "SomePersistanatDiskUri" -AutoDelete -Boot -Nvme `
            -DeviceName "nameOnDevice" -ReadOnly
        $disk.AutoDelete | Should Be $true
        $disk.Boot | Should Be $true
        $disk.DeviceName | Should Be nameOnDevice
        $disk.Index | Should BeNullOrEmpty
        $disk.Initializeparams | Should BeNullOrEmpty
        $disk.Interface__ | Should Be NVME
        $disk.Mode | Should Be READ_ONLY
        $disk.Source | Should Be SomePersistanatDiskUri
    }

    It "should set values for new disk" {
        $disk = New-GceAttachedDiskConfig -SourceImage "SomeDiskImageUri" -AutoDelete -Boot -Name "diskname" `
            -Nvme -DeviceName "nameOnDevice" -DiskType "someDiskType" -ReadOnly -Size 30
        $disk.AutoDelete | Should Be $true
        $disk.Boot | Should Be $true
        $disk.DeviceName | Should Be "nameOnDevice"
        $disk.Index | Should BeNullOrEmpty
        $disk.Interface__ | Should Be NVME
        $disk.Mode | Should Be READ_ONLY
        $disk.Source | Should BeNullOrEmpty
        $params = $disk.InitializeParams
        $params.SourceImage | Should Be SomeDiskImageUri
        $params.DiskName | Should Be diskname
        $params.DiskSizeGb | Should Be 30
        $params.DiskType | Should Be someDiskType
    }

    It "should build list by pipeline" {
        $diskList = New-GceAttachedDiskConfig -SourceImage SomeImageUri |
            New-GceAttachedDiskConfig -Source "SomePersistanatDiskUri"
        $diskList.Count | Should Be 2
        $diskList[0].InitializeParams | Should Not BeNullOrEmpty
        $diskList[1].InitializeParams | Should BeNullOrEmpty
    }

    It "should pass anything through pipeline" {
        $diskList = $null | New-GceAttachedDiskConfig -Source "SomePersistanatDiskUri"
        $diskList.Count | Should Be 2
        $diskList[0] | Should BeNullOrEmpty
        $diskList[1] | Should Not BeNullOrEmpty
    }
}
