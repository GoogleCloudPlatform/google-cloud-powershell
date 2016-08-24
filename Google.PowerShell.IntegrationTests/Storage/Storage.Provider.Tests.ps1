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

    It "Should have drive initalized." {
        Test-Path gs:\ | Should Be $true
        cd gs:\ 2>&1 | Should BeNullOrEmpty
    }

    It "Should make bucket" {
        Test-Path $bucketName | Should Be $false
        mkdir $bucketName
        Test-Path $bucketName | Should Be $true
        $bucket = Get-Item $bucketName
        ($bucket | Get-Member).TypeName | Should Be Google.Apis.Storage.v1.Data.Bucket
        $bucket.Name | Should Be $bucketName
    }
    
    It "Should move current directory to bucket" {
        cd $bucketName 2>&1 | Should BeNullOrEmpty
    }

    It "Should create folder" {
        mkdir $folderName
        Test-Path $folderName | Should Be $true
        cd $folderName
    }
    
    It "Should navigate forward and back" {
        cd ..\..\$bucketName\$folderName\..\.. 2>&1 | Should BeNullOrEmpty
        Get-Location | Should Be "gs:\"
        cd gs:\$bucketName\$folderName 2>&1 | Should BeNullOrEmpty
    }

    It "Should write file" {
        New-Item $fileName -File "$PSScriptRoot\..\README.md"
        Test-Path $fileName | Should Be $true
        Compare-Object (Get-Content gs:\$bucketName\$folderName\$fileName) (Get-Content $PSScriptRoot\..\README.md) | Should BeNullOrEmpty
    }

    It "Should clear file" {
        Clear-Content $fileName
        Test-Path $fileName | Should Be $true
        Get-Content $fileName | Should BeNullOrEmpty
    }

    It "Should set content" {
        Set-Content $fileName -Value $content
        sleep -Seconds 2
        cat $fileName | Should Be $content
    }

    It "Should copy file" {
        cp $fileName $cpFileName
        Test-Path $cpFileName | Should Be $true
        cat $cpFileName | Should Be $content
    }

    It "Should list files" {
        $files = ls
        $files.Count | Should Be 2
        ($files.Name -match $fileName).Count | Should Be 1
        ($files.Name -match $cpFileName).Count | Should Be 1
    }

    It "Should copy folder" {
        cd ..
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
        Test-Path $fileName | Should Be $true
        rm $fileName
        Test-Path $fileName | Should Be $false
    }

    It "Should list all files" {
        cd gs:\$bucketName
        $files = ls -Recurse
        $files.Count | Should Be 5
    }

    It "Should remove folder" {
        cd gs:\$bucketName
        rm $cpFolderName -Recurse
        Test-Path $cpFolderName | Should Be $false
    }

    It "Should remove bucket" {
        cd gs:\
        {rm $bucketName -Recurse} | Should Not Throw
    }
}

Pop-Location
Reset-GCloudConfig $oldActiveConfig $configName
