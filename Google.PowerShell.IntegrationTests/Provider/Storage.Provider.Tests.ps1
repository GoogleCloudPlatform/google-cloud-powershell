. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
Push-Location .
$r = Get-Random

Describe "Storage Provider"{
    $bucketName = "gcs-provider-test-$r"
    $folderName = "gcs-folder-$r"
    $fileName = "gcs-file-$r"
    $cpFileName = "gcs-cp-file-$r"
    $cpFolderName = "gcs-cp-folder-$r"
    $content = "file content $r"

    It "Should have drive initalized" {
        Test-Path gs:\ | Should Be $true
        cd gs:\ 2>&1 | Should BeNullOrEmpty
    }

    It "Should make bucket" {
        cd gs:\
        Test-Path $bucketName | Should Be $false
        mkdir $bucketName
        Test-Path $bucketName | Should Be $true
        $bucket = Get-Item $bucketName
        ($bucket | Get-Member).TypeName | Should Be Google.Apis.Storage.v1.Data.Bucket
        $bucket.Name | Should Be $bucketName
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
        New-Item $fileName -File "$PSScriptRoot\TestFile.txt"
        Test-Path $fileName | Should Be $true
        $gcsContents = Get-Content gs:\$bucketName\$folderName\$fileName
        $localContents = Get-Content $PSScriptRoot\TestFile.txt
        Compare-Object $gcsContents $localContents | Should BeNullOrEmpty
    }

    It "Should clear file" {
        cd gs:\$bucketName\$folderName
        Clear-Content $fileName
        Test-Path $fileName | Should Be $true
        Get-Content $fileName | Should BeNullOrEmpty
        $object = Get-GcsObject $bucketName "$folderName/$fileName"
        $object.Size | Should Be 0
    }

    It "Should set content" {
        cd gs:\$bucketName\$folderName
        Set-Content $fileName -Value $content
        sleep -Seconds 2
        cat $fileName | Should Be $content
    }

    It "Should copy file" {
        cd gs:\$bucketName\$folderName
        cp $fileName $cpFileName
        Test-Path $cpFileName | Should Be $true
        cat $cpFileName | Should Be $content
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
        cat $fileName, $cpFileName | Should Be $content
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
        Test-Path $cpFolderName | Should Be $false
        Test-GcsObject $bucketName "$cpFolderName/" | Should Be $false
    }

    It "Should remove bucket" {
        cd gs:\
        {rm $bucketName -Recurse} | Should Not Throw
    }

    It "Should fail to enter bucket without permissions" {
        (cd gs:\asdf 2>&1).Count | Should BeGreaterThan 0
    }

    # TODO(jimwp): Add test to ensure we are not bypassing access controls.
}

Pop-Location
Reset-GCloudConfig $oldActiveConfig $configName
