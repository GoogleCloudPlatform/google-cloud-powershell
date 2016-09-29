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

        # Confirm it doesn't have any metadata by default.
        $obj.Metadata | Should Be $null
    }

    It "should fail if the file does not exist" {
        { New-GcsObject $bucket "file-not-found.txt" -File "C:\file-404" } `
            | Should Throw "File not found"
    }

    It "should fail if the folder does not exist" {
        { New-GcsObject $bucket -Folder "C:\I should not exist" } `
            | Should Throw "Directory C:\I should not exist cannot be found"
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
        $emptyObj.Size | Should Be 0
        Remove-GcsObject $emptyObj
    }

    It "should write metadata" {
        $obj = New-GcsObject $bucket "metadata-test" `
            -Metadata @{ "alpha" = 1; "beta" = "two"; "Content-Type" = "image/png" }
        $obj.Metadata.Count = 3
        $obj.Metadata["alpha"] | Should Be 1
        $obj.Metadata["beta"] | Should Be "two"
        $obj.Metadata["Content-Type"] | Should Be "image/png"
        # Content-Type can be set from metadata.
        $obj.ContentType | Should Be "image/png"
        Remove-GcsObject $obj
    }

    # Regression for a bug found while unit testing other scenarios.
    It "should write metadata when accepting content from pipeline" {
        $obj = "XXX" | New-GcsObject $bucket "metadata-test-2" `
            -Metadata @{ "alpha" = 1; "beta" = 2; "gamma" = 3}
        $obj.Metadata.Count | Should Be 3

        Remove-GcsObject $obj
    }

    It "will prefer the -ContentType parameter to -Metadata" {
        $obj = New-GcsObject $bucket "metadata-test" `
            -ContentType "image/jpeg" `
            -Metadata @{ "Content-Type" = "image/png" }
        $obj.ContentType | Should Be "image/jpeg"
        # It will also apply to the Metadata too.
        $obj.Metadata["Content-Type"] | Should Be "image/jpeg"
        Remove-GcsObject $obj
    }

    It "should upload an empty folder" {
        $TestFolder = Join-Path $TestDrive "TestFolder"

        try {
            if (-not (Test-Path $TestFolder))
            {
                New-Item -ItemType Directory -Path $TestFolder | Out-Null
            }

            $folder = New-GcsObject -Bucket $bucket -Folder $TestFolder -Force
            $folderName = "TestFolder/"

            $folder.Name | should be $folderName
            $folder.Size | should be 0

            $folderOnline = Get-GcsObject -Bucket $bucket -ObjectName $folderName
            $folderOnline.Name | should be $folderName
            $folderOnline.Size | should be 0
        }
        finally
        {
            Remove-Item $TestFolder -Recurse -Force -ErrorAction Ignore
        }
    }

    It "should upload a folder with files and subfolders" {
        $TestFolder = Join-Path $TestDrive "TestFolder"

        try {
            if (-not (Test-Path $TestFolder))
            {
                New-Item -ItemType Directory -Path $TestFolder | Out-Null
            }

            "Hello, world" | Out-File (Join-Path $TestFolder "world.txt")
            "Hello, mars" | Out-File (Join-Path $TestFolder "mars.txt")
            "Hello, jupiter" | Out-File (Join-Path $TestFolder "jupiter.txt")

            $TestSubfolder = Join-Path $TestFolder "TestSubfolder"
            New-Item -ItemType Directory -Path $TestSubfolder | Out-Null

            "Hello, saturn" | Out-File (Join-Path $TestSubfolder "saturn.txt")
            "Hello, pluto" | Out-File (Join-Path $TestSubfolder "pluto.txt")

            $result = New-GcsObject -Bucket $bucket -Folder $TestFolder -Force

            $result.Count | should be 7
            $result.Name -contains "TestFolder/jupiter.txt" | should be $true
            $result.Name -contains "TestFolder/mars.txt" | should be $true
            $result.Name -contains "TestFolder/TestSubfolder/pluto.txt" | should be $true

            $saturn = Get-GcsObject -Bucket $bucket -ObjectName "TestFolder/TestSubfolder/saturn.txt"
            $saturn.ContentType | should be "text/plain"
        }
        finally
        {
            Remove-Item $TestFolder -Recurse -Force -ErrorAction Ignore
        }
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

Describe "Set-GcsObject" {

    $bucket = "gcps-get-object-testing"
    Create-TestBucket $project $bucket
    Add-TestFile $bucket "testfile1.txt"

    It "should work" {
        # Default ACLs set on the object from the bucket.
        $obj = Get-GcsObject $bucket "testfile1.txt"
        $obj.Acl.Count | Should Be 4
        $obj.Acl[0].ID.Contains("/project-owners-") | Should Be $true
        $obj.Acl[1].ID.Contains("/project-editors-") | Should Be $true
        $obj.Acl[2].ID.Contains("/project-viewers-") | Should Be $true
        $obj.Acl[3].ID.Contains("/user-") | Should Be $true

        # Set new value for ACLs using a predefined set.
        $obj = $obj | Set-GcsObject -PredefinedAcl PublicRead
        $obj.Acl.Count | Should Be 2
        $obj.Acl[0].ID.Contains("/user-") | Should Be $true
        $obj.Acl[1].ID.Contains("/allUsers") | Should Be $true

        # Confirm the change took place.
        $obj = Get-GcsObject $bucket "testfile1.txt"
        $obj.Acl.Count | Should Be 2
        $obj.Acl[0].ID.Contains("/user-") | Should Be $true
        $obj.Acl[1].ID.Contains("/allUsers") | Should Be $true
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

    It "should take pipeline input" {
        # GetTempFileName creates a 0-byte file, which will cause problems
        # because the cmdlet won't overwrite it without -Force.
        $tempFileName = [System.IO.Path]::Combine(
                 [System.IO.Path]::GetTempPath(),
                 [System.IO.Path]::GetRandomFileName())
        Get-GcsObject $bucket $testObjectName |
            Read-GcsObject -OutFile $tempFileName

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
        Write-GcsObject $bucket ($objectName + "2") -Contents $objectContents -Force
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

    It "should write zero bytes" {
        $objectName = "write-zero-bytes-test"

        # Create the original GCS object.
        "contents" | New-GcsObject $bucket $objectName

        Write-GcsObject $bucket $objectName
        $emptyObj = Get-GcsObject $bucket $objectName
        $emptyObj.Size | Should Be 0
        Remove-GcsObject $emptyObj
    }

    It "should not rewrite ACLs" {
        $orgObj = "original contents" | New-GcsObject $bucket "acl-test" `
            -PredefinedAcl bucketOwnerRead

        # The exact project or user IDs don't matter. As long as the ACL kind is correct.
        $orgObj.Acl.Id -like "*/project-owners-*" | Should Be $true
        $orgObj.Acl.Id -like "*/user-*"           | Should Be $true

        # Updating the object leaves the existing ACLs in place.
        $updatedObj = "new contents" | Write-GcsObject $bucket "acl-test"
        $updatedObj.Acl.Id -like "*/project-owners-*" | Should Be $true
        $updatedObj.Acl.Id -like "*/user-*"           | Should Be $true

        Remove-GcsObject $bucket "acl-test"
    }

    It "should not clobber existing metadata" {
        $orgObj = "original contents" | New-GcsObject $bucket "metadata-test" `
            -Metadata @{ "one" = 1; "two" = 2}
        $orgObj.Metadata.Count | Should Be 2
        
        $updatedObj = "new contents" | Write-GcsObject $bucket "metadata-test"
        $updatedObj.Metadata.Count | Should Be 2

        Remove-GcsObject $bucket "metadata-test"
    }

    It "should merge Metadata updates" {
        $step1 = "XXX" | New-GcsObject $bucket "metadata-test2" `
            -Metadata @{ "alpha" = 1; "beta" = 2; "gamma" = 3 }
        $step1.Metadata.Count | Should Be 3
        $step1.Metadata["alpha"] | Should Be 1
        $step1.Metadata["beta"]  | Should Be 2
        $step1.Metadata["gamma"] | Should Be 3

        # Remove a value ("beta"), update a value ("gamma"), add a new value ("delta").
        $step2 = "XXX" | Write-GcsObject $bucket "metadata-test2" `
            -Metadata @{ "beta" = $null; "gamma" = 33; "delta" = 4 }

        $step2.Metadata.Count | Should Be 3
        $step2.Metadata["alpha"] | Should Be 1
        $step2.Metadata.ContainsKey("beta") | Should Be $false
        $step2.Metadata["gamma"] | Should Be 33
        $step2.Metadata["delta"] | Should Be 4

        Remove-GcsObject $bucket "metadata-test2"
    }

    It "should give precidence to the ContentType parameter" {
        # Where Write-Gcs object creates a new object (-Force)
        $newObjectCase = "XXX" | Write-GcsObject $bucket "content-type-test" `
            -ContentType "image/png" -Metadata @{ "Content-Type" = "image/jpeg" } `
            -Force
        $newObjectCase.ContentType | Should Be "image/png"

        # Where Write-Gcs has both ContentType and a Metadata value.
        $both = "XXX" | Write-GcsObject $bucket "content-type-test" `
            -ContentType "test/alpha" -Metadata @{ "Content-Type" = "test/beta" }
        $both.ContentType | Should Be "test/alpha"

        Remove-GcsObject $bucket "content-type-test"
    }
}

Describe "Test-GcsObject" {
    $bucket = "gcps-test-object-testing"
    Create-TestBucket $project $bucket

    It "should work" {
        Test-GcsObject $bucket "test-obj" | Should Be $false
        $obj = "can you hear me now?" | New-GcsObject $bucket "test-obj"
        Test-GcsObject $bucket "test-obj" | Should Be $true
        $obj | Remove-GcsObject
    }

    It "should return false if the Bucket does not exist" {
        Test-GcsObject "bucket-aad2fjadkdmgzadfhj4" "obj.txt"| Should Be $false
    }

    It "should fail if the bucket is not accessible" {
        { Test-GcsObject "asdf" "gcs-object.txt" } | Should Throw "has been disabled"
    }
}

Describe "Copy-GcsObject" {
    $bucket = "gcps-copy-object-testing"
    $r = Get-Random

    It "Should fail to read non-existant bucket" {
        { Copy-GcsObject -SourceBucket $bucket -SourceObject "test-source" $bucket "test-dest" } |
            Should Throw 404
    }

    Context "With bucket" {
        BeforeAll {
            New-GcsBucket $bucket
        }
        
        It "Should fail to read non-existant source object" {
            { Copy-GcsObject -SourceBucket $bucket -SourceObject "test-source" $bucket "test-dest" } |
                Should Throw 404
        }
        
        It "Should fail to read from unaccessable source bucket" {
            { Copy-GcsObject -SourceBucket "asdf" -SourceObject "test-source" $bucket "test-dest" } |
                Should Throw 403
        }
        
        It "Should fail to write to unaccessable source bucket" {
            New-GcsObject $bucket "test-source0" -Contents "test0-$r"
            { Copy-GcsObject -SourceBucket $bucket -SourceObject "test-source0" "asdf" "test-dest" } |
                Should Throw 403
        }

        It "Should work by name" {
            New-GcsObject $bucket "test-source" -Contents "test1-$r"
            Copy-GcsObject -SourceBucket $bucket -SourceObject "test-source" $bucket "test-dest"
            Read-GcsObject $bucket "test-dest" | Should Be "test1-$r"
        }

        It "Should work by object" {
            $sourceObj = New-GcsObject $bucket "test-source2" -Contents "test2-$r"
            $sourceObj | Copy-GcsObject $bucket "test-dest2"
            Read-GcsObject $bucket "test-dest2" | Should Be "test2-$r"
        }

        It "Should overwrite existing object" {
            $sourceObj = Get-GcsObject $bucket "test-source"
            $sourceObj | Copy-GcsObject $bucket "test-dest2" -Force
            Read-GcsObject $bucket "test-dest2" | Should Be "test1-$r"
        }

        AfterAll {
            Remove-GcsBucket $bucket -Force
        }
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
