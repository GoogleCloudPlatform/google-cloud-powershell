﻿. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

Describe "New-GcsObject" {

    $bucket = "gcps-object-testing"
    Create-TestBucket $project $bucket

    It "should work" {
        $filename = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($filename, "Hello, World")

        $objectName = "43b75bdd-8869-496e-8c0d-3c12b49dcb18.txt"

        $newObj = New-GcsObject $bucket $objectName $filename
        Remove-Item -Force $filename

        $newObj.Name | Should Be $objectName
        $newObj.Size | Should Be 12

        # Double check it is stored in GCS.
        $obj = Get-GcsObject $bucket $objectName
        $obj.Name | Should Be $objectName
        $obj.Size | Should Be 12
    }

    It "should fail if the file does not exist" {
        { New-GcsObject $bucket "file-not-found.txt" "C:\file-404" } `
            | Should Throw "File Not Found"
    }

    # Confirm the object can have slashes. Regression test for .NET Client Libs
    # issue: https://github.com/google/google-api-dotnet-client/issues/643
    It "should work for object names with slashes" {
        $filename = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($filename, "Huzzah!")

        $objectName = "C:\both-kinds/country\western"
        $newObj = New-GcsObject $bucket $objectName $filename
        Remove-Item -Force $filename

        $newObj.Name | Should Be $objectName
        $newObj.Size | Should Be 7

        # Double check it is stored in GCS.
        $obj = Get-GcsObject $bucket $objectName
        $obj.Name | Should Be $objectName
        $obj.Size | Should Be 7

        Remove-GcsObject $bucket $objectName
    }

    It "should support relative file paths" {
        # TODO(chrsmith): Fix underlying bugs and use Push-Location and Pop-Location.
        $orgWorkingDir = [System.Environment]::CurrentDirectory
        [System.Environment]::CurrentDirectory = [System.IO.Path]::GetTempPath()

        $filePath = [System.IO.Path]::Combine(
            [System.Environment]::CurrentDirectory,
            "local-file.txt")
        [System.IO.File]::WriteAllText($filePath, "file-contents")        
            
        $newObj = New-GcsObject $bucket "on-gcs/file.txt" "local-file.txt"

        $obj = Get-GcsObject $bucket "on-gcs/file.txt"
        $obj.Name | Should Be "on-gcs/file.txt"

        [System.Environment]::CurrentDirectory = $orgWorkingDir
    }

    It "should set predefined ACLs if instructed to" {
        $filename = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($filename, "predefined ACL test")

        $objectName = "predefined-acl-test"
        $newObj = New-GcsObject $bucket $objectName $filename -PredefinedAcl "publicRead"
        # ACL[0] is from the user who created the object.
        # ACL[1]'s Id is like "gcps-object-testing/predefined-acl-test/1459867429211000/allUsers"
        $newObj.Acl[1].Id | Should Match "$bucket/$objectName/"
        $newObj.Acl[1].Id | Should Match "/allUsers"

        $existingObj = Get-GcsObject $bucket $objectName
        $newObj.Acl[1].Id | Should Match "$bucket/$objectName/"
        $newObj.Acl[1].Id | Should Match "/allUsers"

        Remove-GcsObject $bucket $objectName
    }
}

Describe "Get-GcsObject" {

    $bucket = "gcps-get-object-testing"
    Create-TestBucket $project $bucket
    Add-TestFile $bucket "testfile1.txt"
    Add-TestFile $bucket "testfile2.txt"

    It "should work" {
        $obj = Get-GcsObject $bucket "testfile1.txt"
        $obj.Name | Should Be "testfile1.txt"
        $obj.Size | Should Be 0
    }

    It "should fail for non existing objects" {
        { Get-GcsObject -Bucket $bucket -ObjectName "file-404.txt" } | Should Throw "404"
    }
}

Describe "Find-GcsObject" {

    $bucket = "gcps-get-object-testing"
    Create-TestBucket $project $bucket
    Add-TestFile $bucket "file1.txt"
    Add-TestFile $bucket "A/file2.txt"
    Add-TestFile $bucket "A/B/file3.txt"
    Add-TestFile $bucket "A/B/file4.txt"
    Add-TestFile $bucket "B/A/A/file5.txt"
    Add-TestFile $bucket "B/A/A/A/A/file6.txt"
    Add-TestFile $bucket "B/A/A/A/A/file7.txt"
    Add-TestFile $bucket "B/B/A/A/A/file8.txt"
    Add-TestFile $bucket "C/file9.txt"
    Add-TestFile $bucket "C/fileA.txt"

    It "should support getting all objects in a bucket" {
        $objs = Find-GcsObject $bucket
        $objs.Length | Should Be 10
    }

    It "should support prefix matching" {
        $objs = Find-GcsObject $bucket -Prefix "A/"
        $objs.Length | Should Be 3

        $objs = Find-GcsObject $bucket -Prefix "B/"
        $objs.Length | Should Be 4

        $objs = Find-GcsObject $bucket -Prefix "B/B"
        $objs.Length | Should Be 1
    }

    It "should support delimiting results" {
        $objs = Find-GcsObject $bucket -Delimiter "/"
        $objs.Length | Should Be 1

        $objs = Find-GcsObject $bucket -Prefix "A/" -Delimiter "/"
        $objs.Length | Should Be 1
        
        $objs = Find-GcsObject $bucket -Prefix "A/B" -Delimiter "/"
        $objs.Length | Should Be 0

        $objs = Find-GcsObject $bucket -Prefix "A/B/" -Delimiter "/"
        $objs.Length | Should Be 2
    }
}

Describe "Remove-GcsObject" {

    $bucket = "gcps-get-object-testing"
    Create-TestBucket $project $bucket
    Add-TestFile $bucket "testfile1.txt"
    Add-TestFile $bucket "testfile2.txt"

    It "should work" {
        $obj = Remove-GcsObject $bucket "testfile1.txt"
        { Get-GcsObject $bucket "testfile1.txt" } | Should Throw "404"
    }

    It "should fail for non existing objects" {
        { Remove-GcsObject -Bucket $bucket -ObjectName "file-404.txt" } | Should Throw "404"
    }
}

Describe "Read-GcsObjectContents" {

    $bucket = "gcps-objectcontents-testing"
    Create-TestBucket $project $bucket

    $testObjectName = "alpha/beta/testfile.txt"
    $testFileContents = "Hello, World"

    BeforeEach {
        # Before each test, upload a new file named "helloworld.txt" to the GCS bucket.
        $filename = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($filename, $testFileContents)
        New-GcsObject $bucket $testObjectName $filename
        Remove-Item -Force $filename
    }

    It "should work" {
        $tempFileName = [System.IO.Path]::Combine(
             [System.IO.Path]::GetTempPath(),
             [System.IO.Path]::GetRandomFileName())
        Read-GcsObject $bucket $testObjectName $tempFileName

        $fileContents = [System.IO.File]::ReadAllText($tempFileName)
        $fileContents | Should BeExactly $testFileContents

        Remove-Item -Force $tempFileName
    }

    It "won't overwrite existing files" {
        # Creates a 0-byte file, which we won't clobber.
        $tempFileName = [System.IO.Path]::GetTempFileName()
        # Pester automatically confirms the 
        { Read-GcsObject $bucket $testObjectName $tempFileName } `
            | Should Throw "File Already Exists"

        Remove-Item -Force $tempFileName
    }

    It "will cobber files if -Force is present" {
        # Creates a 0-byte file in the way.
        $tempFileName = [System.IO.Path]::GetTempFileName()
        Read-GcsObject $bucket $testObjectName $tempFileName -Force

        # Confirm the file has non-zero size.
        [System.IO.File]::ReadAllText($tempFileName) | Should Be $testFileContents
    }

    It "throws a 404 if the Storage Object does not exist" {
        $tempFileName = [System.IO.Path]::Combine(
             [System.IO.Path]::GetTempPath(),
             [System.IO.Path]::GetRandomFileName())
        { Read-GcsObject $bucket "random-file" $tempFileName } `
            | Should Throw "404" 
    }

    It "fails if it doesn't have write access" {
        { Read-GcsObject $bucket $testObjectName "C:\windows\helloworld.txt" } `
            | Should Throw "is denied" 
    }
    # TODO(chrsmith): Confirm it throws a 403 if you don't have GCS access.
    # TODO(chrsmith): Confirm it fails if you don't have write access to disk.
}
