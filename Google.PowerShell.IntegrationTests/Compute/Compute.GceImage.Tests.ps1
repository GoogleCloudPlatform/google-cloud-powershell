. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Get-GceImage -Project $project | Remove-GceImage

Describe "Add-GceImage" {
    $r = Get-Random
    $diskName = "test-image-disk-$r"
    $disk = New-GceDisk -DiskName $diskName -SizeGb 1
    $imageName = "test-add-image-$r"

    It "should fail for wrong project" {
        { Add-GceImage $disk -Name $imageName -Project "asdf" } | Should Throw 403
    }

    It "should fail for bad disk" {
        $baseUrl = "https://www.googleapis.com/compute/v1/"
        $diskPath = "projects/gcloud-powershell-testing/zones/us-central1-a/disks/not-a-disk"
        $badDisk = @{
            "SelfLink" ="$baseUrl$diskPath";
            "Name" = $diskName
        }
        { Add-GceImage $badDisk -Name $imageName } | Should Throw 404
    }

    Context "add successes" {
        AfterEach {
            Get-GceImage -Project $project | Remove-GceImage
        }

        It "should work" {
            $image = Add-GceImage $disk -Name $imageName -Description "for testing" -Family "test-family"
            $image.Name | Should Be $imageName
            $image.Description | Should Be "for testing"
            # Make sure the image source disk url is the same as the disk's url pointing to itself.
            $image.SourceDisk | Should Be $disk.SelfLink
            $image.Family | Should Be "test-family"
        }

        It "should work with pipeline" {
            $image = $disk | Add-GceImage
            $image.Name | Should Be $diskName
            $image.SourceDisk | Should Be $disk.SelfLink
        }
    }

    Remove-GceDisk $diskName
    Get-GceImage -Project $project | Remove-GceImage
}

Describe "Get-GceImage" {
    $r = Get-Random
    $diskName = "test-image-disk-$r"
    $disk = New-GceDisk -DiskName $diskName -SizeGb 1
    $imageName1 = "test-get-image1-$r"
    $imageName2 = "test-get-image2-$r"
    $familyName = "test-family-$r"

    It "should fail for wrong project" {
        { Get-GceImage $imageName1 -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant image" {
        { Get-GceImage $imageName1 -Project $project} | Should Throw 404
    }

    It "should work when no images exist" {
        $images = Get-GceImage -Project $project
        $images | Should BeNullOrEmpty
    }

    $defaultProjects =  "centos-cloud", "coreos-cloud", "debian-cloud", "debian-cloud",
                "rhel-cloud", "suse-cloud", "ubuntu-os-cloud", "windows-cloud"

    It "should get images from default projects" {
        $images = Get-GceImage
        # Check that we get at least one image for each default project.
        $defaultProjects | %{ ($images.SelfLink -match $_).Count } | Should BeGreaterThan 0
        $images.Deprecated | ForEach-Object { $_ | Should BeNullOrEmpty }
    }

    It "should include deprecated images from project" {
        $images = Get-GceImage "windows-cloud" -IncludeDeprecated
        $images.SelfLink | Should Match "windows-cloud"
        ($images.Deprecated -ne $null).Count | Should BeGreaterThan 0
    }

    <#
    $disk | Add-GceImage -Name $imageName1
    $disk | Add-GceImage -Name $imageName2 -Family $familyName

    It "should get all project images" {
        $images = Get-GceImage -Project $project
        ($images | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Image }
        $images.Count | Should Be 2
        $images.SourceDisk | Should Match $diskName
    }

    It "should get by name" {
        $image = Get-GceImage $imageName1 -Project $project
        ($image | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Image }
        $image.Count | Should Be 1
        $image.Name | Should Be $imageName1
        $image.SourceDisk | Should Match $diskName
    }

    It "should get by name with pipeline" {
        $image = $imageName1 | Get-GceImage -Project $project
        ($image | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Image }
        $image.Count | Should Be 1
        $image.Name | Should Be $imageName1
        $image.SourceDisk | Should Match $diskName
    }

    It "should get by family" {
        $image = Get-GceImage -Family $familyName -Project $project
        ($image | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Image }
        $image.Count | Should Be 1
        $image.Name | Should Be $imageName2
        $image.SourceDisk | Should Match $diskName
    }
    #>

    Remove-GceDisk $diskName
    Get-GceImage -Project $project | Remove-GceImage
}

Describe "Disable-GceImage" {
    $r = Get-Random
    $diskName = "test-image-disk-$r"
    $disk = New-GceDisk -DiskName $diskName -SizeGb 1
    $imageName1 = "test-add-image1-$r"
    $imageName2 = "test-add-image2-$r"

    $deprecateTime = (Get-Date) + (New-TimeSpan -Minutes 4)
    $obsoleteTime = (Get-Date) + (New-TimeSpan -Hours 4)
    $deleteTime = (Get-Date) + (New-TimeSpan -Days 4)

    It "should fail for wrong project" {
        { Disable-GceImage $imageName1 -State DEPRECATED -Project "asdf" } | Should Throw 403
    }

    It "should fail for nonexist image" {
        { Disable-GceImage $imageName1 -State DEPRECATED } | Should Throw 404
    }
    
    <#
    $image1 = $disk | Add-GceImage -Name $imageName1
    $image2 = $disk | Add-GceImage -Name $imageName2

    It "should set status by name" {
        $image = Disable-GceImage $imageName1 -State DEPRECATED -Replacement $image2
        $deprecation = $image.Deprecated
        $deprecation | Should Not BeNullOrEmpty
        $deprecation.State | Should Be DEPRECATED
        $deprecation.Replacement | Should Be $image2.SelfLink
    }

    It "should set status by object" -SKip {
        $image = Disable-GceImage $image1 -State OBSOLETE -Replacement $image2
        $deprecation = $image.Deprecated
        $deprecation | Should Not BeNullOrEmpty
        $deprecation.State | Should Be OBSOLETE
        $deprecation.Replacement | Should Be $image2.SelfLink
    }
    #>

    Remove-GceDisk $diskName
    Get-GceImage -Project $project | Remove-GceImage
}

Describe "Remove-GceImage" {
    $r = Get-Random
    $diskName = "test-image-disk-$r"
    $disk = New-GceDisk -DiskName $diskName -SizeGb 1
    $imageName = "test-remove-image-$r"

    It "should fail for wrong project" {
        { Remove-GceImage $imageName -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant image" {
        { Remove-GceImage $imageName } | Should Throw 404
    }

    Context "successful removes" {
        BeforeEach {
            Add-GceImage $disk -Name $imageName
        }

        It "should work by name" {
            Remove-GceImage $imageName
            { Get-GceImage $imageName -Project $project} | Should Throw 404
        }

        It "should work by name with pipeline" {
            $imageName | Remove-GceImage
            { Get-GceImage $imageName -Project $project} | Should Throw 404
        }

        It "should work by object" {
            $image = Get-GceImage $imageName -Project $project
            Remove-GceImage $image
            { Get-GceImage $imageName -Project $project} | Should Throw 404
        }

        It "should work by object with pipeline" {
            Get-GceImage $imageName -Project $project| Remove-GceImage
            { Get-GceImage $imageName -Project $project } | Should Throw 404
        }
    }

    Remove-GceDisk $diskName
}

Reset-GCloudConfig $oldActiveConfig $configName
