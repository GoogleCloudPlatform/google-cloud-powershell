. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "New-GcsObject" {

    $bucket = "gcps-object-testing"
    Create-TestBucket $project $bucket

    It "should work" {
        $filename = [System.IO.Path]::GetTempFileName()
        "Hello, World" | Out-File $filename -Encoding utf8

        $objectName = "43b75bdd-8869-496e-8c0d-3c12b49dcb18.txt"

        $newObj = New-GcsObject $bucket $objectName -File $filename
        Remove-Item $filename

        $newObj.Name | Should Be $objectName
        $newObj.Size | Should Be 17

        # Double check it is stored in GCS.
        $obj = Get-GcsObject $bucket $objectName
        $obj.Name | Should Be $objectName
        $obj.Size | Should Be 17
    }

    It "should fail if the file does not exist" {
        { New-GcsObject $bucket "file-not-found.txt" -File "C:\file-404" } `
            | Should Throw "File not found"
    }

    It "accepts pipeline input as object contents" {
        "test string" | New-GcsObject $bucket "pipeline-test"
        Read-GcsObject $bucket "pipeline-test" | Should Be "test string"
        Remove-GcsObject $bucket "pipeline-test"
    }

    # Confirm the object can have slashes. Regression test for .NET Client Libs
    # issue: https://github.com/google/google-api-dotnet-client/issues/643
    It "should work for object names with slashes" {
        $filename = [System.IO.Path]::GetTempFileName()
        "Huzzah!" | Out-File $filename -Encoding ascii -NoNewline

        $objectName = "C:\both-kinds/country\western"
        $newObj = New-GcsObject $bucket $objectName -File $filename
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
        Push-Location $env:TEMP
        $fileName = "local-file.txt"
        "file-contents" | Out-File "$env:TEMP\$fileName" -Encoding ascii -NoNewline

        $tempFolderName = Split-Path $env:TEMP -Leaf
        $tests = @(
                "$fileName",
                ".\$fileName",
                "./$fileName",
                (Join-Path $env:TEMP "$fileName"),
                "..\$tempFolderName\$fileName")

        $i = 0
        foreach ($test in $tests) {
            New-GcsObject $bucket "on-gcs/relative-path/scenario-$i" -File $fileName

            $obj = Get-GcsObject $bucket "on-gcs/relative-path/scenario-$i"
            $obj.Size | Should Be 13  # "file-contents"

            $i = $i + 1
        }

        Remove-Item "$env:TEMP\$fileName"
        Pop-Location
    }

    It "should set predefined ACLs if instructed to" {
        $filename = [System.IO.Path]::GetTempFileName()
        "predefined ACL test" | Out-File $filename -Encoding ascii -NoNewline

        $objectName = "predefined-acl-test"
        $newObj = New-GcsObject $bucket $objectName -File $filename -PredefinedAcl "publicRead"
        # ACL[0] is from the user who created the object.
        # ACL[1]'s Id is like "gcps-object-testing/predefined-acl-test/1459867429211000/allUsers"
        $newObj.Acl[1].Id | Should Match "$bucket/$objectName/"
        $newObj.Acl[1].Id | Should Match "/allUsers"

        $existingObj = Get-GcsObject $bucket $objectName
        $newObj.Acl[1].Id | Should Match "$bucket/$objectName/"
        $newObj.Acl[1].Id | Should Match "/allUsers"

        Remove-GcsObject $bucket $objectName
    }

    It "will not overwrite existing objects without -Force" {
        $objectName = "existing-object"

        $tempFile = [System.IO.Path]::GetTempFileName()
        "existing-gcs-object" | Out-File $tempFile -Encoding ascii -NoNewline

        # Create
        New-GcsObject $bucket $objectName -File $tempFile

        # Confirm we won't clobber
        { New-GcsObject $bucket $objectName -File $tempFile } `
            | Should Throw "Storage object 'existing-object' already exists"

        # Confirm -Force works
        "updated-object-contents" | Out-File $tempFile -Encoding ascii -NoNewline
        New-GcsObject $bucket $objectName -File $tempFile -Force
        Remove-Item $tempFile

        # Confirm the contents are expected
        $tempFile2 = [System.IO.Path]::GetTempFileName()  # New temp file to download the updated object.
        Read-GcsObject $bucket $objectName $tempFile2 -Force

        $fileContents = Get-Content $tempFile2
        $fileContents | Should BeExactly "updated-object-contents"

        Remove-Item $tempFile2
    }

    It "will accept object contents from the pipeline" {
        $objectName = "new-object-from-pipeline"
        $objectContents = "Object contents from the PowerShell pipeline"
        $objectContents | New-GcsObject $bucket $objectName -PredefinedAcl "publicRead"

        Read-GcsObject $bucket $objectName | Should BeExactly $objectContents
    }

    It "will have default MIME types for files and text" {
        # Text file
        "text" | New-GcsObject $bucket "text-file" | Out-Null
        $textObj = Get-GcsObject $bucket "text-file"
        $textObj.ContentType | Should Be "text/plain; charset=utf-8"
        Remove-GcsObject $textObj

        # Binary file
        $tempFile = [System.IO.Path]::GetTempFileName()
        # TODO(chrsmith): Support creating 0-byte files on GCS.
        [System.IO.File]::WriteAllText($tempFile, "<binary-data>")
        New-GcsObject $bucket "binary-file" -File $tempFile | Out-Null
        $binObj = Get-GcsObject $bucket "binary-file"
        $binObj.ContentType | Should Be "application/octet-stream"
        Remove-GcsObject $binObj
        Remove-Item $tempFile
    }

    It "will infer mime type based on file extension" {
        $tempFile = [System.IO.Path]::GetTempFileName() + ".png"
        # TODO(chrsmith): Support creating 0-byte files on GCS.
        [System.IO.File]::WriteAllText($tempFile,"<binary-data>")
        New-GcsObject $bucket "png-file" -File $tempFile | Out-Null
        $binObj = Get-GcsObject $bucket "png-file"
        $binObj.ContentType | Should Be "image/png"
        Remove-GcsObject $binObj
        Remove-Item $tempFile
    }

    It "should write zero byte files" {
        $emptyObj = New-GcsObject $bucket "zero-byte-test"
        $emptyObj.Size | Shoudl Be 0
        Remove-GcsObject $emptyObj

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

    It "should support getting the bucket via the pipeline (and via Bucket object)" {
        $bucketObj = Get-GcsBucket $bucket
        $objs = $bucketObj | Find-GcsObject
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

    It "should work" {
        Add-TestFile $bucket "testfile.txt"
        $obj = Remove-GcsObject $bucket "testfile.txt"
        { Get-GcsObject $bucket "testfile.txt" } | Should Throw "404"
    }

    It "should accept objects from the pipeline" {
        @("alpha", "beta", "gamma") | ForEach { New-GcsObject $bucket $_ $_ }
        $objs = Find-GcsObject $bucket
        $objs.Length | Should Be 3

        $objs | Remove-GcsObject

        $objs = Find-GcsObject $bucket
        $objs.Length | Should Be 0
    }

    It "should fail for non existing objects" {
        { Remove-GcsObject -Bucket $bucket -ObjectName "file-404.txt" } | Should Throw "404"
    }
}

Describe "Read-GcsObject" {

    $bucket = "gcps-read-object-testing"
    Create-TestBucket $project $bucket

    $testObjectName = "alpha/beta/testfile.txt"
    $testFileContents = "Hello, World"

    BeforeEach {
        # Before each test, upload a new file to the GCS bucket.
        $filename = [System.IO.Path]::GetTempFileName()
        $testFileContents | Out-File $filename -Encoding ascii -NoNewline
        New-GcsObject $bucket $testObjectName -File $filename -Force
        Remove-Item -Force $filename
    }

    It "should work" {
        # GetTempFileName creates a 0-byte file, which will cause problems
        # because the cmdlet won't overwrite it without -Force.
        $tempFileName = [System.IO.Path]::Combine(
                 [System.IO.Path]::GetTempPath(),
                 [System.IO.Path]::GetRandomFileName())
        Read-GcsObject $bucket $testObjectName $tempFileName

        Get-Content $tempFileName | Should BeExactly $testFileContents

        Remove-Item $tempFileName
    }

    It "won't overwrite existing files" {
        # Creates a 0-byte file, which we won't clobber.
        $tempFileName = [System.IO.Path]::GetTempFileName()
        # File should point to somewhere in Users\AppData\Local\Temp.
        { Read-GcsObject $bucket $testObjectName $tempFileName } `
            | Should Throw "already exists"

        Remove-Item $tempFileName
    }

    It "will clobber files if -Force is present" {
        # Creates a 0-byte file in the way.
        $tempFileName = [System.IO.Path]::GetTempFileName()
        Read-GcsObject $bucket $testObjectName $tempFileName -Force

        # Confirm the file has non-zero size.
        Get-Content $tempFileName | Should Be $testFileContents
        Remove-Item $tempFileName
    }

    It "raise an error if the Storage Object does not exist" {
        $tempFileName = [System.IO.Path]::Combine(
             [System.IO.Path]::GetTempPath(),
             [System.IO.Path]::GetRandomFileName())
        { Read-GcsObject $bucket "random-file" $tempFileName } `
            | Should Throw "Not Found"
    }

    It "fails if it doesn't have write access" {
        { Read-GcsObject $bucket $testObjectName "C:\windows\helloworld.txt" } `
            | Should Throw "is denied" 
    }

    It "will write contents to pipeline if no -OutFile is set" {
        $result = Read-GcsObject $bucket $testObjectName
        $result | Should BeExactly $testFileContents
        # TODO(chrsmith): Find out how to get Pester to confirm a cmdlet did not have any
        # output, and confirm that -Outfile doesn't put anything in the pipeline.
    }

    It "will work in conjunction with the Out-File cmdlet" {
        $tempFileName = [System.IO.Path]::GetTempFileName()
        # Read contents from GCS, pipe them to a file.
        Read-GcsObject $bucket $testObjectName `
            | Out-File $tempFileName -Force -NoNewline
        Get-Content $tempFileName | Should Be $testFileContents
        Remove-Item $tempFileName
    }

    # TODO(chrsmith): Confirm it throws a 403 if you don't have GCS access.
    # TODO(chrsmith): Confirm it fails if you don't have write access to disk.
}

Describe "Write-GcsObject" {

    $bucket = "gcps-write-object-testing"
    Create-TestBucket $project $bucket

    It "should work" {
        $objectName = "folder/file.txt"
        $originalContents = "This is the ORIGINAL file contents."

        # Create the original file.
        $tempFile = [System.IO.Path]::GetTempFileName()
        $originalContents | Out-File $tempFile -Encoding ascii -NoNewline
        New-GcsObject $bucket $objectName -File $tempFile
        Remove-Item $tempFile

        # Rewrite its contents
        $tempFile = [System.IO.Path]::GetTempFileName()
        $newContents = "This is the NEW content."
        $newContents | Out-File $tempFile -Encoding ascii -NoNewline
        Write-GcsObject $bucket $objectName -File $tempFile
        Remove-Item $tempFile

        # Confirm the contents have changed.
        $tempFile = [System.IO.Path]::GetTempFileName()
        Read-GcsObject $bucket $objectName $tempFile -Force

        Get-Content $tempFile | Should BeExactly $newContents
        Remove-Item $tempFile
    }

    It "requires the -File or -Contents parameter be named (or from pipeline)" {
        { Write-GcsObject "bucket-name" "object-name" "contents-or-file?" } `
            | Should Throw "Parameter set cannot be resolved using the specified named parameters"
    }

    It "will accept contents from the pipeline" {
        # Note that we aren't specifying the -Contents or -File parameter. Instead
        # that is set by the pipeline.
        $objectName = "write-gcsobject-from-pipeline"
        $objectContents = "This is some text from the PowerShell pipeline"
        # This step fails because Write assumes the Object already exists (unless -Force) is used.
        # For general objct creation, use New-GcsObject.
        { $objectContents | Write-GcsObject $bucket $objectName } `
            | Should Throw "Storage object 'write-gcsobject-from-pipeline' does not exist"

        # Adding -Force does the trick. Confirm it worked.
        $objectContents | Write-GcsObject $bucket $objectName -Force
        Read-GcsObject $bucket $objectName | Should BeExactly $objectContents

        # Exercise the explicit -Content parameter too.
        Write-GcsObject $bucket ($objectName + "2") -Content $objectContents -Force
        Read-GcsObject $bucket ($objectName + "2") | Should BeExactly $objectContents
    }

    It "should accept relative file paths" {
        $objectName = "relative-file-test"

        # Create the original GCS object.
        "contents" | New-GcsObject $bucket $objectName

        # Rewrite its contents, reading from a relative file.
        $localFileName = "write-gcs-object-file-in-temp-dir.txt"
        "updated contents" | Out-File "$env:TEMP\$localFileName" -Encoding ascii -NoNewline
        Push-Location $env:TEMP
        Write-GcsObject $bucket $objectName -File ".\$localFileName"
        Remove-Item $localFileName

        # Confirm the contents have changed, writing to a relative file path.
        $downloadedFileName = "file-in-temp-dir-from-gcs.txt"
        Read-GcsObject $bucket $objectName -OutFile $downloadedFileName -Force
        Get-Content "$env:TEMP\$downloadedFileName" | Should BeExactly "updated contents"

        # Cleanup.
        Remove-Item $downloadedFileName
        Remove-GcsObject $bucket $objectName
        Pop-Location
    }

    # TODO(chrsmith): Confirm it works for 0-byte files (currently it doesn't).
    # TODO(chrsmith): Confirm Write-GcsObject doesn't remove object metadata, such
    # as its existing ACLs. (Since we are uploading a new object in-place.)
}

Reset-GCloudConfig $oldActiveConfig $configName
