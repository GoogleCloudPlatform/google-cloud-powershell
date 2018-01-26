. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
Push-Location .
$r = Get-Random

Describe "Storage Provider"{
    # The bucket which we create and in which we run all of our tests.
    $bucketName = "gcs-provider-test-$r"
    # The first folder object we make.
    $folderName = "gcs-folder-$r"
    # The name of a folder that is only implied as a prefix.
    $fakeFolderName = "fake-folder-$r"
    # The first file object we make.
    $fileName = "gcs-file-$r"
    # The file object we copy to.
    $cpFileName = "gcs-cp-file-$r"
    # The folder object we copy to.
    $cpFolderName = "gcs-cp-folder-$r"
    # The string we set as the content of a file.
    $content1 = "file content 1 $r"
    # The string we set as the content of a file.
    $content2 = "file content 2 $r"

    It "Should have drive initialized" {
        Test-Path gs:\ | Should Be $true
        cd gs:\ 2>&1 | Should BeNullOrEmpty
    }

    It "Should change directory from function" {
        cd gs:\
        cd c:
        gs:
        $PWD.Path | Should Be "gs:\"
    }

    It "Should make bucket" {
        cd gs:\
        Test-Path $bucketName | Should Be $false
        mkdir $bucketName
        Test-Path $bucketName | Should Be $true
        $buckets = Get-Item $bucketName
        ($buckets | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Storage.v1.Data.Bucket }
        $buckets.Name | ForEach-Object { $_ | Should Be $bucketName }
    }

    It "Should move current directory to bucket" {
        cd gs:\
        cd $bucketName 2>&1 | Should BeNullOrEmpty
    }

    It "Should create folder" {
        cd gs:\$bucketName
        mkdir $folderName
        Test-Path $folderName | Should Be $true
        $folder = Get-Item $folderName
        $folder.Name | Should Be "$folderName/"
        cd $folderName
    }
    
    It "Should navigate forward and back" {
        cd gs:\$bucketName\$folderName
        cd ..\..\$bucketName\$folderName\..\.. 2>&1 | Should BeNullOrEmpty
        Get-Location | Should Be "gs:\"
        cd gs:\$bucketName\$folderName 2>&1 | Should BeNullOrEmpty
        Get-Location | Should Be "gs:\$bucketName\$folderName"
    }

    It "Should write file" {
        cd gs:\$bucketName\$folderName
        $tempFile = New-TemporaryFile
        Set-Content $tempFile -Value $content1
        New-Item $fileName -File $tempFile
        Test-Path $fileName | Should Be $true
        $gcsContents = Get-Content gs:\$bucketName\$folderName\$fileName
        $localContents = Get-Content $tempFile
        Compare-Object $gcsContents $localContents | Should BeNullOrEmpty
    }

    It "Should create file using Set-Content" {
        cd gs:\$bucketName\$folderName
        rm $fileName
        Set-Content $fileName -Value $content1
        Sleep 2
        Test-Path $fileName | Should Be $true
        Get-Content $fileName | Should Be $content1
    }

    It "Should clear file" {
        cd gs:\$bucketName\$folderName
        Clear-Content $fileName
        Test-Path $fileName | Should Be $true
        Get-Content $fileName | Should BeNullOrEmpty
        $object = Get-GcsObject -ObjectName $fileName
        $object.Size | Should Be 0
    }

    It "Should set content" {
        cd gs:\$bucketName\$folderName
        Set-Content $fileName -Value $content2
        sleep -Seconds 2
        cat $fileName | Should Be $content2
    }

    It "Should copy file" {
        cd gs:\$bucketName\$folderName
        cp $fileName $cpFileName
        Test-Path $cpFileName | Should Be $true
        cat $cpFileName | Should Be $content2
    }

    It "Should list files" {
        cd gs:\$bucketName\$folderName
        $files = ls
        $files.Count | Should Be 2
        ($files.Name -match $fileName).Count | Should Be 1
        ($files.Name -match $cpFileName).Count | Should Be 1
    }

    It "Should copy folder" {
        cd gs:\$bucketName
        cp $folderName $cpFolderName -Recurse
        Test-Path $cpFolderName | Should Be $true
        cd $cpFolderName
        $files = ls
        $files.Count | Should Be 2
        ($files.Name -match $fileName).Count | Should Be 1
        ($files.Name -match $cpFileName).Count | Should Be 1
        cat $fileName, $cpFileName | ForEach-Object { $_ | Should Be $content2 }
    }

    It "Should delete file" {
        cd gs:\$bucketName\$cpFolderName
        Test-Path $fileName | Should Be $true
        rm $fileName
        Test-Path $fileName | Should Be $false
    }

    It "Should list all files" {
        cd gs:\$bucketName
        $files = ls -Recurse
        # Should get folder, cpFolder, folder/file, folder/cpFile, and cpFolder/cpFile.
        # cpFolder/file was deleted.
        $files.Count | Should Be 5
    }

    It "Should remove folder" {
        cd gs:\$bucketName
        rm $cpFolderName -Recurse
        Sleep 2
        Test-Path $cpFolderName | Should Be $false
        Test-GcsObject $bucketName "$cpFolderName/" | Should Be $false
    }

    It "Should create tiered item" {
        cd gs:\$bucketName
        $newFile = New-Item $fakeFolderName\$fileName -Value $content1
        Test-Path $fakeFolderName | Should Be True
        Test-Path $fakeFolderName\ | Should Be True
        { cd $fakeFolderName } | Should Not Throw
        cd ..
        { cd $fakeFolderName\ } | Should Not Throw
        cat $fileName | Should Be $content1
        $file = ls
        $file.Count | Should Be 1
        Compare-Object $newFile $file | Should Be $null
    }

    It "Should remove folder that is not at the root" {
        cd gs:\$bucketName\$folderName
        mkdir $fakeFolderName
        Test-Path $fakeFolderName | Should Be $true
        Remove-Item ".\$fakeFolderName\"
        Test-Path $fakeFolderName | Should Be $false
    }

    It "Should not remove with -WhatIf" {
        cd gs:\
        {rm $bucketName -Recurse -WhatIf} | Should Not Throw
        Test-Path $bucketName | Should Be $true
    }

    It "Should remove bucket" {
        cd gs:\
        {rm $bucketName -Recurse} | Should Not Throw
        Test-Path $bucketName | Should Be $false
    }

    It "Should fail to enter bucket without permissions" {
        # Confirm we get an error when attempting to cd into an illegal bucket.
        $cdErrors = (cd gs:\asdf 2>&1)
        $cdErrors.Count | Should Be 1
        ($cdErrors | gm).TypeName | ForEach-Object { $_ | Should Be "System.Management.Automation.ErrorRecord" }
    }

    # TODO(jimwp): Add test to ensure we are not bypassing access controls.
}

Pop-Location
Reset-GCloudConfig $oldActiveConfig $configName
