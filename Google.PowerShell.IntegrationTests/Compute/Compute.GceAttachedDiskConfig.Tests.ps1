$path = $PSScriptRoot

while($child -eq $null) {
    $child = Get-ChildItem GcloudCmdlets.ps1 -Recurse -Path $path 
    $path = Split-Path $path
}

. $child.FullName
Install-GcloudCmdlets

$r = Get-Random
Describe "New-GceAttachedDiskConfig" {
    $image = Get-GceImage "debian-cloud" -Family "debian-8"
    $source = New-GceDisk "test-attached-disk-config-$r" $image
    It "should set defaults for persistant disks" {
        $disk = New-GceAttachedDiskConfig $source
        $disk.AutoDelete | Should Be $false
        $disk.Boot | Should Be $false
        $disk.DeviceName | Should BeNullOrEmpty
        $disk.Index | Should BeNullOrEmpty
        $disk.Initializeparams | Should BeNullOrEmpty
        $disk.Interface__ | Should Be SCSI
        $disk.Mode | Should Be READ_WRITE
        $disk.Source | Should Be $source.SelfLink
    }

    It "should set defaults for new disk" {
        $disk = New-GceAttachedDiskConfig $image
        $disk.AutoDelete | Should Be $false
        $disk.Boot | Should Be $false
        $disk.DeviceName | Should BeNullOrEmpty
        $disk.Index | Should BeNullOrEmpty
        $disk.Interface__ | Should Be SCSI
        $disk.Mode | Should Be READ_WRITE
        $disk.Source | Should BeNullOrEmpty
        $params = $disk.InitializeParams
        $params.SourceImage | Should Be $image.SelfLink
        $params.DiskName | Should BeNullOrEmpty
        $params.DiskSizeGb | Should BeNullOrEmpty
        $params.DiskType | Should BeNullOrEmpty
    }

    It "should fail with both source and sourceImage" {
        { New-GceAttachedDiskConfig -SourceImage $image -Source $source} |
            Should Throw "Parameter set cannot be resolved"
    }

    It "should set values for persistant disks" {
        $disk = New-GceAttachedDiskConfig -Source $source -AutoDelete -Boot -Nvme `
            -DeviceName "nameOnDevice" -ReadOnly
        $disk.AutoDelete | Should Be $true
        $disk.Boot | Should Be $true
        $disk.DeviceName | Should Be nameOnDevice
        $disk.Index | Should BeNullOrEmpty
        $disk.Initializeparams | Should BeNullOrEmpty
        $disk.Interface__ | Should Be NVME
        $disk.Mode | Should Be READ_ONLY
        $disk.Source | Should Be $source.SelfLink
    }

    It "should set values for new disk" {
        $disk = New-GceAttachedDiskConfig -SourceImage $image -AutoDelete -Boot -Name "diskname" `
            -Nvme -DeviceName "nameOnDevice" -DiskType "someDiskType" -ReadOnly -Size 30
        $disk.AutoDelete | Should Be $true
        $disk.Boot | Should Be $true
        $disk.DeviceName | Should Be "nameOnDevice"
        $disk.Index | Should BeNullOrEmpty
        $disk.Interface__ | Should Be NVME
        $disk.Mode | Should Be READ_ONLY
        $disk.Source | Should BeNullOrEmpty
        $params = $disk.InitializeParams
        $params.SourceImage | Should Be $image.SelfLink
        $params.DiskName | Should Be diskname
        $params.DiskSizeGb | Should Be 30
        $params.DiskType | Should Be someDiskType
    }

    It "should build list" {
        $diskList = (New-GceAttachedDiskConfig -SourceImage $image),
            (New-GceAttachedDiskConfig -Source $source)
        $diskList.Count | Should Be 2
        $diskList[0].InitializeParams | Should Not BeNullOrEmpty
        $diskList[1].InitializeParams | Should BeNullOrEmpty
    }
}
