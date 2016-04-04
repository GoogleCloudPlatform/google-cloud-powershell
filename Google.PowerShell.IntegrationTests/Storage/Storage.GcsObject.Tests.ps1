. $PSScriptRoot\..\GcloudCmdlets.ps1
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
        $newObj.Acl[1].Id | Should Contain "/o/predefined-acl-test/acl/allUsers"

        $existingObj = Get-GcsObject $bucket $objectName
        $existingObj.Acl[1].Id | Should Contain "/o/predefined-acl-test/acl/allUsers"

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
        $objs = Get-GcsObject $bucket
        $objs.Length | Should Be 10
    }

    It "should support prefix matching" {
        $objs = Get-GcsObject $bucket -Prefix "A/"
        $objs.Length | Should Be 3

        $objs = Get-GcsObject $bucket -Prefix "B/"
        $objs.Length | Should Be 4

        $objs = Get-GcsObject $bucket -Prefix "B/B"
        $objs.Length | Should Be 1
    }

    It "should support delimiting results" {
        $objs = Get-GcsObject $bucket -Delimiter "/"
        $objs.Length | Should Be 1

        $objs = Get-GcsObject $bucket -Prefix "A/" -Delimiter "/"
        $objs.Length | Should Be 1
        
        $objs = Get-GcsObject $bucket -Prefix "A/B" -Delimiter "/"
        $objs.Length | Should Be 0

        $objs = Get-GcsObject $bucket -Prefix "A/B/" -Delimiter "/"
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
