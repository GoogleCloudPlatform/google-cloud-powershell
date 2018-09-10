. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GCloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "New-GcsObject" {

    $bucket = "gcps-object-testing"

    BeforeEach {
        if (-not (Test-GcsBucket $bucket)) {
            Create-TestBucket $project $bucket
        }
    }

    AfterEach {
        if (Test-GcsBucket $bucket) {
            Remove-GcsBucket -Name $bucket -Force -ErrorAction SilentlyContinue
        }
    }

    function Test-NewGcsObjectSimple([switch]$gcsProvider) {
        $filename = [System.IO.Path]::GetTempFileName()
        "Hello, World" | Out-File $filename -Encoding utf8

        $objectName = Get-Random

        $currentLocation = Resolve-Path .\
        try {
            if ($gcsProvider) {
                cd "gs:\$bucket"
                $newObj = New-GcsObject -ObjectName $objectName -File $filename
                $obj = Get-GcsObject -ObjectName $objectName
            }
            else {
                $newObj = New-GcsObject $bucket $objectName -File $filename
                $obj = Get-GcsObject $bucket $objectName
            }
            Remove-Item $filename

            $newObj.Name | Should Be $objectName
            $newObj.Size | Should Be 17

            # Double check it is stored in GCS.
            $obj.Name | Should Be $objectName
            $obj.Size | Should Be 17

            # Confirm it doesn't have any metadata by default.
            $obj.Metadata | Should Be $null
        }
        finally {
            cd $currentLocation
        }
    }

    It "should work" {
        Test-NewGcsObjectSimple
    }

    It "should upload a file content with GCS Provider" {
        Test-NewGcsObjectSimple -gcsProvider
    }

    It "should respect -Bucket parameter even in GCS Provider" {
        $r = Get-Random
        $objectName = "gcps-new-gcsobject-testing-$r"
        $anotherBucket = "gcps-new-gcsobject-testing-$r"

        $currentLocation = Resolve-Path .\
        try {
            Create-TestBucket $project $anotherBucket
            cd "gs:\$bucket"
            $newObj = New-GcsObject -ObjectName $objectName -Value "Testing" -Bucket $anotherBucket

            $newObj.Name | Should Be $objectName
            $newObj.Bucket | Should Be $anotherBucket

            # Double check it is stored in GCS.
            $obj = Get-GcsObject $anotherBucket $objectName
            $obj.Name | Should Be $objectName
        }
        finally {
            cd $currentLocation
            Remove-GcsBucket -Name $anotherBucket -Force -ErrorAction SilentlyContinue
        }
    }

    It "should work and preserve folder structure in a GCS folder" {
        $filename = [System.IO.Path]::GetTempFileName()
        "Hello, World" | Out-File $filename -Encoding utf8

        $objectName = Get-Random

        $currentLocation = Resolve-Path .\
        $folderName = "gcps-testing"
        try {
            cd "gs:\$bucket"
            mkdir $folderName
            cd $folderName
            $newObj = New-GcsObject -ObjectName $objectName -File $filename
            Remove-Item $filename

            $newObj.Name | Should Be "$folderName/$objectName"
            $newObj.Size | Should Be 17

            # Double check it is stored in GCS.
            $obj = Get-GcsObject -ObjectName $objectName
            $obj.Name | Should Be "$folderName/$objectName"
            $obj.Size | Should Be 17

            # Confirm it doesn't have any metadata by default.
            $obj.Metadata | Should Be $null

            # Try object name with slash to see whether the folder structure is preserved in this case.
            $newObj = New-GcsObject -ObjectName "Testing/$objectName" -Value "Testing value"
            $newObj.Name | Should Be "$folderName/Testing/$objectName"

            # Double check it is stored in GCS.
            $obj = Get-GcsObject -ObjectName "Testing/$objectName"
            $obj.Name | Should Be "$folderName/Testing/$objectName"
        }
        finally {
            cd $currentLocation
        }
    }

    It "should fail if the -Bucket is not provided outside GCS Provider" {
        { New-GcsObject -ObjectName "file-not-found.txt" -File "C:\file-404" } `
            | Should Throw "Bucket name cannot be determined."
    }

    It "should fail if the file does not exist" {
        { New-GcsObject $bucket "file-not-found.txt" -File "C:\file-404" } `
            | Should Throw "File not found"
    }

    It "should fail if the folder does not exist" {
        { New-GcsObject $bucket -Folder "C:\I should not exist" } `
            | Should Throw "Directory 'C:\I should not exist' cannot be found"
    }

    It "accepts pipeline input as object contents" {
        "test string" | New-GcsObject "pipeline-test" -Bucket $bucket
        Read-GcsObject $bucket "pipeline-test" | Should Be "test string"
        Remove-GcsObject $bucket "pipeline-test"

        $currentLocation = Resolve-Path .\
        try {
            cd "gs:\$bucket"
            "test string" | New-GcsObject -ObjectName "pipeline-test"
            Read-GcsObject -ObjectName "pipeline-test" | Should Be "test string"
            Remove-GcsObject -ObjectName "pipeline-test"
        }
        finally {
            cd $currentLocation
        }
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
                ".\$fileremoveName",
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

    It "will not overwrite existing objects without -Force" -Skip {
        $tempFile = [System.IO.Path]::GetTempFileName()
        "existing-gcs-object" | Out-File $tempFile -Encoding ascii -NoNewline

        # Create
        $newObj = New-GcsObject $bucket -File $tempFile

        # Verify that the -ObjectName will default to the file name if not specified (only for file upload case).
        $objectName = Split-Path -Leaf $tempFile
        $newObj.Name | Should BeExactly $objectName

        # Confirm we won't clobber
        { New-GcsObject $bucket $objectName -File $tempFile -ErrorAction Stop } `
            | Should Throw "Storage object '$objectName' already exists"

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

    It "should work with fixed-type metadata" {
        $obj = New-GcsObject $bucket "metadata-test" `
            -ContentEncoding "gzip" -ContentType "image/png"
        $obj.Metadata | Should BeNullOrEmpty
        $obj.ContentType | Should Be "image/png"
        $obj.ContentEncoding | Should Be "gzip"
        Remove-GcsObject $obj
    }

    It "should work with fixed-type metadata through -Metadata parameter" {
        $obj = New-GcsObject $bucket "metadata-test" `
            -Metadata @{ "Content-Encoding" = "gzip"; "Content-Type" = "image/png" }
        $obj.Metadata | Should BeNullOrEmpty
        $obj.ContentType | Should Be "image/png"
        $obj.ContentEncoding | Should Be "gzip"
        Remove-GcsObject $obj
    }

    It "should write metadata" {
        $obj = New-GcsObject $bucket "metadata-test" `
            -Metadata @{ "alpha" = 1; "beta" = "two"; "Content-Type" = "image/png" }
        $obj.Metadata.Count = 2
        $obj.Metadata["alpha"] | Should Be 1
        $obj.Metadata["beta"] | Should Be "two"
        # Content-Type can be set from metadata.
        $obj.ContentType | Should Be "image/png"
        Remove-GcsObject $obj
    }

    It "should work with both fixed-type and custom metadata" {
        $obj = New-GcsObject $bucket "metadata-test" `
            -Metadata @{ "Content-Encoding" = "gzip"; "Content-Type" = "image/png";
                         "alpha" = 1; "beta" = "two" }
        $obj.Metadata.Count = 2
        $obj.Metadata["alpha"] | Should Be 1
        $obj.Metadata["beta"] | Should Be "two"
        $obj.ContentType | Should Be "image/png"
        $obj.ContentEncoding | Should Be "gzip"
        Remove-GcsObject $obj
    }

    # Regression for a bug found while unit testing other scenarios.
    It "should write metadata when accepting content from pipeline" {
        $obj = "XXX" | New-GcsObject $bucket "metadata-test-2" `
            -Metadata @{ "alpha" = 1; "beta" = 2; "gamma" = 3}
        $obj.Metadata.Count | Should Be 3

        Remove-GcsObject $obj
    }

    It "will prefer the fixed-type metadata parameter to -Metadata" {
        $obj = New-GcsObject $bucket "metadata-test" `
            -ContentLanguage "aa" `
            -ContentType "image/jpeg" `
            -Metadata @{ "Content-Type" = "image/png"; "Content-Language" = "en" }
        $obj.ContentType | Should Be "image/jpeg"
        $obj.ContentLanguage | Should Be "aa"
        # It should not apply to the Metadata too.
        $obj.Metadata | Should BeNullOrEmpty
        Remove-GcsObject $obj
    }

    function Test-NewGcsObjectEmptyFolder([switch]$gcsProvider) {
        $folderName = [System.IO.Path]::GetRandomFileName()
        $testFolder = "$env:TEMP/$folderName"

        $currentLocation = Resolve-Path .\
        try {
            New-Item -ItemType Directory -Path $testFolder | Out-Null
            if ($gcsProvider) {
                cd "gs:\$bucket"
                $folder = New-GcsObject -Folder $testFolder -Force
                $folderOnline = Get-GcsObject -ObjectName "$folderName/"
            }
            else {
                $folder = New-GcsObject -Bucket $bucket -Folder $testFolder -Force
                $folderOnline = Get-GcsObject -Bucket $bucket -ObjectName "$folderName/"
            }

            $folder.Name | Should Be "$folderName/"
            $folder.Size | Should Be 0

            $folderOnline.Name | Should Be "$folderName/"
            $folderOnline.Size | Should Be 0
        }
        finally {
            cd $currentLocation
            Remove-Item $testFolder -Recurse -Force -ErrorAction Ignore
        }
    }

    It "should upload an empty folder" {
        Test-NewGcsObjectEmptyFolder
    }

    It "should upload an empty folder in GCS Provider location" {
        Test-NewGcsObjectEmptyFolder -gcsProvider
    }

    function Test-SlashHelper($result, $folderName) {
        $result.Count | Should Be 2
        $result.Name -contains "$folderName/" | should be $true
        $result.Name -contains "$folderName/world.txt" | should be $true
    }

    function Test-NewGcsObjectSlashes([switch]$gcsProvider) {
        $folderName = [System.IO.Path]::GetRandomFileName()
        $testFolder = "$env:TEMP/$folderName"

        $currentLocation = Resolve-Path .\
        try {
            New-Item -ItemType Directory -Path $testFolder | Out-Null
            "Hello, world" | Out-File "$testFolder/world.txt"

            if ($gcsProvider) {
                cd "gs:\$bucket"
                # Add a backslash to the end and make sure it is uploaded.
                $result = New-GcsObject -Folder "$testFolder\" -Force
            }
            else {
                # Add a backslash to the end and make sure it is uploaded.
                $result = New-GcsObject -Bucket $bucket -Folder "$testFolder\" -Force
            }

            Test-SlashHelper $result $folderName

            if ($gcsProvider) {
                # Add a forwards slash to the end and make sure it is uploaded.
                $result = New-GcsObject -Folder "$testFolder/" -Force
            }
            else {
                # Add a forwards slash to the end and make sure it is uploaded.
                $result = New-GcsObject -Bucket $bucket -Folder "$testFolder/" -Force
            }

            Test-SlashHelper $result $folderName
        }
        finally {
            cd $currentLocation
            Remove-Item $testFolder -Recurse -Force -ErrorAction Ignore
        }
    }

    It "should work for both forwards slashes and backslashes in the path" {
        Test-NewGcsObjectSlashes
    }

    It "should work for both forwards slashes and backslashes in the path in GCS Provider location" {
        Test-NewGcsObjectSlashes -gcsProvider
    }

    function Test-NewGcsObjectSubfolder([switch]$gcsProvider) {
        $folderName = [System.IO.Path]::GetRandomFileName()
        $testFolder = "$env:TEMP/$folderName"

        $currentLocation = Resolve-Path .\
        try {
            New-Item -ItemType Directory -Path $testFolder | Out-Null

            "Hello, world" | Out-File "$testFolder/world.txt"
            "Hello, mars" | Out-File "$testFolder/mars.txt"
            "Hello, jupiter" | Out-File "$testFolder/jupiter.txt"

            $testSubfolder = "$testFolder/TestSubfolder"
            New-Item -ItemType Directory -Path $testSubfolder | Out-Null

            "Hello, saturn" | Out-File "$testSubfolder/saturn.txt"
            "Hello, pluto" | Out-File "$testSubfolder/pluto.txt"

            if ($gcsProvider) {
                cd "gs:\$bucket"
                $result = New-GcsObject -Folder $testFolder -Force
            }
            else {
                $result = New-GcsObject -Bucket $bucket -Folder $testFolder -Force
            }

            # The query returns 7 even though we create 5 because 2 of them are folders.
            $result.Count | Should Be 7
            # Confirm the files and folders were created.
            $result.Name -contains "$folderName/" | should be $true
            $result.Name -contains "$folderName/world.txt" | Should Be $true
            $result.Name -contains "$folderName/jupiter.txt" | Should Be $true
            $result.Name -contains "$folderName/mars.txt" | Should Be $true
            $result.Name -contains "$folderName/TestSubFolder/" | should be $true
            $result.Name -contains "$folderName/TestSubfolder/pluto.txt" | Should Be $true
            $result.Name -contains "$folderName/TestSubfolder/saturn.txt" | Should Be $true

            $saturn = Get-GcsObject -Bucket $bucket -ObjectName "$folderName/TestSubfolder/saturn.txt"
            $saturn.ContentType | Should Be "text/plain"

            # This should contain everything except the TestSubFolder and its files.
            $objs = Get-GcsObject -Delimiter "/" -Bucket $bucket -Prefix "$folderName/"
            $objs.Count | Should Be 4
            $objs.Name -contains "$folderName/TestSubfolder/pluto.txt" | Should Be $false
            $objs.Name -contains "$folderName/TestSubfolder/saturn.txt" | Should Be $false
            $objs.Name -contains "$folderName/TestSubfolder/" | Should Be $false

            # Everything should be returned!
            $objs = Get-GcsObject -Bucket $bucket -Prefix "$folderName/"
            $objs.Count | Should Be 7

            $objs = Get-GcsObject -Bucket $bucket -Prefix "$folderName/TestSubfolder/"
            $objs.Count | Should Be 3
            $objs.Name -contains "$folderName/TestSubfolder/pluto.txt" | Should Be $true
            $objs.Name -contains "$folderName/TestSubfolder/saturn.txt" | Should Be $true
            $objs.Name -contains "$folderName/TestSubfolder/" | Should Be $true
        }
        finally {
            cd $currentLocation
            Remove-Item $testFolder -Recurse -Force -ErrorAction Ignore
        }
    }

    It "should upload a folder with files and subfolders" {
        Test-NewGcsObjectSubfolder
    }

    It "should upload a folder with files and subfolders in GCS Provider" {
        Test-NewGcsObjectSubfolder -gcsProvider
    }

    It "should upload a folder with the correct prefix" {
        $folderName = [System.IO.Path]::GetRandomFileName()
        $testFolder = "$env:TEMP/$folderName"

        try {
            New-Item -ItemType Directory -Path $testFolder | Out-Null
            "Hello, world" | Out-File "$testFolder/world.txt"
            $prefix = "Planet"
            $result = New-GcsObject -Bucket $bucket -Folder "$testFolder/" -Force -ObjectNamePrefix $prefix

            $result.Count | Should Be 2
            $result.Name -contains "$prefix/$folderName/" | should be $true
            $result.Name -contains "$prefix/$folderName/world.txt" | should be $true
        }
        finally {
            Remove-Item $testFolder -Recurse -Force -ErrorAction Ignore
        }
    }

    It "should upload a folder with the correct prefix in GCS Provider" {
        $folderName = [System.IO.Path]::GetRandomFileName()
        $testFolder = "$env:TEMP/$folderName"

        $currentLocation = Resolve-Path .\
        try {
            New-Item -ItemType Directory -Path $testFolder | Out-Null
            "Hello, world" | Out-File "$testFolder/world.txt"
            $prefix = "Planet"

            cd "gs:\$bucket"

            $result = New-GcsObject -Folder "$testFolder/" -Force -ObjectNamePrefix $prefix

            $result.Count | Should Be 2
            $result.Name -contains "$prefix/$folderName/" | should be $true
            $result.Name -contains "$prefix/$folderName/world.txt" | should be $true

            $anotherDir = "another-directory"
            mkdir $anotherDir
            cd $anotherDir

            $result = New-GcsObject -Folder "$testFolder/" -Force -ObjectNamePrefix $prefix

            $result.Count | Should Be 2
            $result.Name -contains "$anotherDir/$prefix/$folderName/" | should be $true
            $result.Name -contains "$anotherDir/$prefix/$folderName/world.txt" | should be $true
        }
        finally {
            cd $currentLocation
            Remove-Item $testFolder -Recurse -Force -ErrorAction Ignore
        }
    }
}

Describe "Get-GcsObject" {
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

    It "should work" {
        $obj = Get-GcsObject $bucket "file1.txt"
        $obj.Name | Should Be "file1.txt"
        $obj.Size | Should Be 0
    }

    It "should fail for non existing objects" {
        { Get-GcsObject -Bucket $bucket -ObjectName "file-404.txt" -ErrorAction Stop } | Should Throw "'file-404.txt' does not exist"
    }

    It "should support getting all objects in a bucket" {
        $objs = Get-GcsObject $bucket
        $objs.Length | Should Be 10
    }

    It "should support getting all objects in a bucket in GCS Provider location" {
        $currentLocation = Resolve-Path .\
        try {
            cd "gs:\$bucket"
            $objs = Get-GcsObject
            $objs.Length | Should Be 10
        }
        finally {
            cd $currentLocation
        }
    }

    It "should support getting the bucket via the pipeline (and via Bucket object)" {
        $bucketObj = Get-GcsBucket $bucket
        $objs = $bucketObj | Get-GcsObject
        $objs.Length | Should Be 10
    }

    It "should honor -Bucket and pipelining from Bucket object even in GCS Provider" {
        $r = Get-Random
        $anotherBucket = "gcps-get-gcsobject-$r"
        Create-TestBucket $project $anotherBucket

        $currentLocation = Resolve-Path .\
        try {
            cd "gs:\$anotherBucket"
            $bucketObj = Get-GcsBucket $bucket
            $objs = $bucketObj | Get-GcsObject
            $objs.Length | Should Be 10
        }
        finally {
            cd $currentLocation
            Remove-GcsBucket $anotherBucket -Force -ErrorAction Ignore
        }
    }

    It "should support prefix matching" {
        $objs = Get-GcsObject $bucket -Prefix "A/"
        $objs.Length | Should Be 3

        $objs = Get-GcsObject $bucket -Prefix "B/"
        $objs.Length | Should Be 4

        $objs = Get-GcsObject $bucket -Prefix "B/B"
        $objs.Length | Should Be 1
    }

    It "should support prefix matching in GCS Provider location" {
        $currentLocation = Resolve-Path .\
        try {
            cd "gs:\$bucket"
            $objs = Get-GcsObject -Prefix "A/"
            $objs.Length | Should Be 3

            cd .\A
            $objs = Get-GcsObject
            $objs.Length | Should Be 3
            cd ..

            $objs = Get-GcsObject -Prefix "B/"
            $objs.Length | Should Be 4

            cd .\B
            $objs = Get-GcsObject
            $objs.Length | Should Be 4
            cd ..

            $objs = Get-GcsObject -Prefix "B/B"
            $objs.Length | Should Be 1

            cd .\B
            $objs = Get-GcsObject -Prefix "B/"
            $objs.Length | Should Be 1
            cd ..
        }
        finally {
            cd $currentLocation
        }
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

    It "should support delimiting results in GCS Provider location" {
        $currentLocation = Resolve-Path .\
        try {
            cd "gs:\$bucket"
            $objs = Get-GcsObject -Delimiter "/"
            $objs.Length | Should Be 1

            $objs = Get-GcsObject -Prefix "A/" -Delimiter "/"
            $objs.Length | Should Be 1

            cd .\A
            $objs = Get-GcsObject -Delimiter "/"
            $objs.Length | Should Be 1
            cd ..

            $objs = Get-GcsObject -Prefix "A/B" -Delimiter "/"
            $objs.Length | Should Be 0

            cd .\A\B
            $objs = Get-GcsObject -Delimiter "/"
            # Result here is 2 because the prefix is "A/B/"
            $objs.Length | Should Be 2
            cd ..\..

            $objs = Get-GcsObject -Prefix "A/B/" -Delimiter "/"
            $objs.Length | Should Be 2

            cd .\A
            $objs = Get-GcsObject -Prefix "B/" -Delimiter "/"
            $objs.Length | Should Be 2
            cd ..
        }
        finally {
            cd $currentLocation
        }
    }
}

Describe "Set-GcsObject" {
    function Test-ObjectDefaultAcl($obj) {
        $obj.Acl.Count | Should Be 4
        $obj.Acl[0].ID.Contains("/project-owners-") | Should Be $true
        $obj.Acl[1].ID.Contains("/project-editors-") | Should Be $true
        $obj.Acl[2].ID.Contains("/project-viewers-") | Should Be $true
        $obj.Acl[3].ID.Contains("/user-") | Should Be $true
    }

    function Test-ObjectPublicReadAcl($obj) {
        $obj.Acl.Count | Should Be 2
        $obj.Acl[0].ID.Contains("/user-") | Should Be $true
        $obj.Acl[1].ID.Contains("/allUsers") | Should Be $true
    }

    function Test-SetGcsObject([switch]$gcsProvider) {
        $r = Get-Random
        $fileName = "$r.txt"
        $bucket = "gcps-set-object-testing-$r"
        Create-TestBucket $project $bucket
        Add-TestFile $bucket $fileName
        $currentLocation = Resolve-Path .\
        try {
            if ($gcsProvider) {
                cd "gs:\$bucket"
                $obj = Get-GcsObject -ObjectName $fileName
            }
            else {
                $obj = Get-GcsObject $bucket $fileName
            }
            Test-ObjectDefaultAcl $obj

            # Set new value for ACLs using a predefined set.
            if ($gcsProvider) {
                $obj = Set-GcsObject -ObjectName $obj.Name -PredefinedAcl PublicRead
                $onlineObj = Get-GcsObject -ObjectName $fileName
            }
            else {
                $obj = $obj | Set-GcsObject -PredefinedAcl PublicRead
                $onlineObj = Get-GcsObject $bucket $fileName
            }

            Test-ObjectPublicReadAcl $obj
            # Confirm the change took place.
            Test-ObjectPublicReadAcl $onlineObj
        }
        finally {
            cd $currentLocation
            Remove-GcsBucket $bucket -Force -ErrorAction Ignore
        }
    }

    It "should work" {
        Test-SetGcsObject
    }

    It "should work in GCS Provider location" {
        Test-SetGcsObject -gcsProvider
    }
}

Describe "Remove-GcsObject" {
    $bucket = "gcps-get-object-testing"

    BeforeEach {
        if (-not (Test-GcsBucket $bucket)) {
            Create-TestBucket $project $bucket
        }
    }

    AfterEach {
        if (Test-GcsBucket $bucket) {
            Remove-GcsBucket -Name $bucket -Force -ErrorAction SilentlyContinue
        }
    }

    It "should work" {
        Add-TestFile $bucket "testfile.txt"
        Remove-GcsObject $bucket "testfile.txt"
        { Get-GcsObject $bucket "testfile.txt" -ErrorAction Stop } | Should Throw "'testfile.txt' does not exist"
    }

    It "should work in GCS Provider location" {
        Add-TestFile $bucket "testfile.txt"
        $currentLocation = Resolve-Path .\
        try {
            cd "gs:\$bucket"
            Remove-GcsObject -ObjectName "testfile.txt"
            { Get-GcsObject -ObjectName "testfile.txt" -ErrorAction Stop } | Should Throw "'testfile.txt' does not exist"

            $anotherFolder = "anotherFolder"
            mkdir $anotherFolder
            cd $anotherFolder
            New-GcsObject -ObjectName "Test"
            Remove-GcsObject -ObjectName "Test"
            { Get-GcsObject -ObjectName "Test" -ErrorAction Stop } | Should Throw "does not exist"
            cd ..
        }
        finally {
            cd $currentLocation
        }
    }

    It "should accept objects from the pipeline" {
        @("alpha", "beta", "gamma") | ForEach { New-GcsObject $bucket $_ $_ }
        $objs = Get-GcsObject $bucket
        $objs.Length | Should Be 3

        $objs | Remove-GcsObject

        $objs = Get-GcsObject $bucket
        $objs.Length | Should Be 0
    }

    It "should accept objects from the pipeline in GCS Provider location" {
        @("alpha", "beta", "gamma") | ForEach { New-GcsObject $bucket $_ $_ }
        $objs = Get-GcsObject $bucket
        $objs.Length | Should Be 3

        $r = Get-Random
        $anotherBucket = "gcps-get-gcsobject-$r"
        Create-TestBucket $project $anotherBucket

        $currentLocation = Resolve-Path .\
        try {
            cd "gs:\$anotherBucket"
            $objs | Remove-GcsObject

            $objs = Get-GcsObject $bucket
            $objs.Length | Should Be 0
        }
        finally {
            cd $currentLocation
            Remove-GcsBucket $anotherBucket -Force -ErrorAction Ignore
        }
    }

    It "should fail for non existing objects" {
        { Remove-GcsObject -Bucket $bucket -ObjectName "file-404.txt" } | Should Throw "'file-404.txt' does not exist"
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

    function Test-ReadGcsObjectSimple([switch]$gcsProvider) {
        # GetTempFileName creates a 0-byte file, which will cause problems
        # because the cmdlet won't overwrite it without -Force.
        $tempFileName = [System.IO.Path]::Combine(
                 [System.IO.Path]::GetTempPath(),
                 [System.IO.Path]::GetRandomFileName())
        $currentLocation = Resolve-Path .\
        try {
            if ($gcsProvider) {
                cd "gs:\$bucket"
                Read-GcsObject -ObjectName $testObjectName -OutFile $tempFileName
            }
            else {
                Read-GcsObject $bucket $testObjectName $tempFileName
            }
            Get-Content $tempFileName | Should BeExactly $testFileContents
            Remove-Item $tempFileName
        }
        finally {
            cd $currentLocation
        }
    }

    It "should work" {
        Test-ReadGcsObjectSimple
    }

    It "should work in GCS Provider location" {
        Test-ReadGcsObjectSimple -gcsProvider
    }

    function Test-ReadGcsObjectSimplePipeline([switch]$gcsProvider) {
        # GetTempFileName creates a 0-byte file, which will cause problems
        # because the cmdlet won't overwrite it without -Force.
        $tempFileName = [System.IO.Path]::Combine(
                 [System.IO.Path]::GetTempPath(),
                 [System.IO.Path]::GetRandomFileName())
        $currentLocation = Resolve-Path .\
        try {
            if ($gcsProvider) {
                cd "gs:\$bucket"
                Get-GcsObject -ObjectName $testObjectName | Read-GcsObject -OutFile $tempFileName
            }
            else {
                Get-GcsObject $bucket $testObjectName | Read-GcsObject -OutFile $tempFileName
            }
            Get-Content $tempFileName | Should BeExactly $testFileContents
            Remove-Item $tempFileName
        }
        finally {
            cd $currentLocation
        }
    }

    It "should take pipeline input" {
        Test-ReadGcsObjectSimplePipeline
    }

    It "should accept objects from the pipeline in GCS Provider location" {
        Test-ReadGcsObjectSimplePipeline -gcsProvider
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
            | Should Throw "'random-file' does not exist"
    }

    # This test can only be run in non-admin PowerShell.
    It "fails if it doesn't have write access" -Skip {
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

    BeforeEach {
        if (-not (Test-GcsBucket $bucket)) {
            Create-TestBucket $project $bucket
        }
    }

    AfterEach {
        if (Test-GcsBucket $bucket) {
            Remove-GcsBucket -Name $bucket -Force -ErrorAction SilentlyContinue
        }
    }

    function Test-WriteGcsObjectSimple([switch]$gcsProvider) {
        $objectName = "folder/file.txt"
        $originalContents = "This is the ORIGINAL file contents."

        # Create the original file.
        $tempFile = [System.IO.Path]::GetTempFileName()
        $originalContents | Out-File $tempFile -Encoding ascii -NoNewline
        $currentLocation = Resolve-Path .\
        try {
            New-GcsObject -Bucket $bucket -ObjectName $objectName -File $tempFile
            Remove-Item $tempFile

            # Rewrite its contents
            $tempFile = [System.IO.Path]::GetTempFileName()
            $newContents = "This is the NEW content."
            $newContents | Out-File $tempFile -Encoding ascii -NoNewline
            if ($gcsProvider) {
                cd "gs:\$bucket"
                Write-GcsObject -ObjectName $objectName -File $tempFile
            }
            else {
                Write-GcsObject $bucket $objectName -File $tempFile -Force
            }
            Remove-Item $tempFile

            # Confirm the contents have changed.
            $tempFile = [System.IO.Path]::GetTempFileName()
            Read-GcsObject -Bucket $bucket -ObjectName $objectName -OutFile $tempFile -Force

            Get-Content $tempFile | Should BeExactly $newContents
            Remove-Item $tempFile
        }
        finally {
            cd $currentLocation
        }
    }

    It "should work" {
        Test-WriteGcsObjectSimple
    }

    It "should work in GCS Provider location" {
        Test-WriteGcsObjectSimple -gcsProvider        
    }

    It "will accept contents from the pipeline" {
        # Note that we aren't specifying the -Value or -File parameter. Instead
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

        # Exercise the explicit -Value parameter too.
        Write-GcsObject $bucket ($objectName + "2") -Value $objectContents -Force
        Read-GcsObject $bucket ($objectName + "2") | Should BeExactly $objectContents
    }

    It "will accept GCS Object from the pipeline" {
        $objectName = "write-gcsobject-from-pipeline"
        $objectContents = "This is some text from the PowerShell pipeline"

        # Create the object
        New-GcsObject -Bucket $bucket -ObjectName $objectName -Value "Wrong Value"

        # Using piped in GCS Object with -Value.
        $objectContents = "This is some text from the PowerShell pipeline using piped in GCS Object"
        $currentLocation = Resolve-Path .\

        try {
            cd "gs:\$bucket"
            Get-GcsObject -ObjectName $objectName | Write-GcsObject -Value $objectContents
            Read-GcsObject -ObjectName $objectName | Should BeExactly $objectContents
        }
        finally {
            cd $currentLocation
        } 

        # Using piped in GCS Object with -File.
        $fileContents = "This is the file contents."
        $tempFile = [System.IO.Path]::GetTempFileName()

        try {
            cd "gs:\$bucket"
            $fileContents | Out-File $tempFile -Encoding ascii -NoNewline
            Get-GcsObject -ObjectName $objectName | Write-GcsObject -File $tempFile
        }
        finally {
            cd $currentLocation
        } 

        # Confirm the contents have changed.
        $tempFile = [System.IO.Path]::GetTempFileName()
        try {
            Read-GcsObject -Bucket $bucket -ObjectName $objectName $tempFile -Force
            Get-Content $tempFile | Should BeExactly $fileContents
        }
        finally {
            Remove-Item $tempFile
        }
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

    It "should not clobber existing custom metadata" {
        $orgObj = "original contents" | New-GcsObject $bucket "metadata-test" `
            -Metadata @{ "one" = 1; "two" = 2}
        $orgObj.Metadata.Count | Should Be 2
        
        $updatedObj = "new contents" | Write-GcsObject $bucket "metadata-test"
        $updatedObj.Metadata.Count | Should Be 2

        Remove-GcsObject $bucket "metadata-test"
    }

    It "should not clobber existing fixed-key metadata" {
        $orgObj = "original contents" | New-GcsObject $bucket "metadata-test" `
            -ContentEncoding "gzip" -ContentLanguage "aa"
        
        $updatedObj = "new contents" | Write-GcsObject $bucket "metadata-test" `
            -ContentLanguage "en"
        $updatedObj.ContentLanguage | Should Be "en"
        $updatedObj.ContentEncoding | Should Be "gzip"

        Remove-GcsObject $bucket "metadata-test"
    }

    It "should update fixed-key metadata to null" {
        $orgObj = "original contents" | New-GcsObject $bucket "metadata-test" `
            -ContentEncoding "gzip" -ContentLanguage "aa"
        
        $updatedObj = "new contents" | Write-GcsObject $bucket "metadata-test" `
            -ContentLanguage $null -ContentEncoding $null
        $updatedObj.ContentLanguage | Should BeNullOrEmpty
        $updatedObj.ContentEncoding | Should BeNullOrEmpty

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

    It "should use fixed-key parameter in -Metadata parameter" {
        # Where Write-Gcs object creates a new object (-Force)
        $newObjectCase = "XXX" | Write-GcsObject $bucket "content-type-test" `
            -ContentType "image/png" -ContentLanguage "en" `
            -Metadata @{ "Content-Type" = "image/jpeg" } -Force
        $newObjectCase.ContentType | Should Be "image/png"
        $newObjectCase.ContentLanguage | Should Be "en"

        $both = "XXX" | Write-GcsObject $bucket "content-type-test" `
            -Metadata @{ "Content-Type" = "test/beta"; "Content-Language" = "aa" }
        $both.ContentType | Should Be "test/beta"
        $both.ContentLanguage | Should Be "aa"

        Remove-GcsObject $bucket "content-type-test"
    }

    It "should give precedence to the fixed type parameter" {
        # Where Write-Gcs object creates a new object (-Force)
        $newObjectCase = "XXX" | Write-GcsObject $bucket "content-type-test" `
            -ContentType "image/png" -ContentLanguage "en" `
            -Metadata @{ "Content-Type" = "image/jpeg" } -Force
        $newObjectCase.ContentType | Should Be "image/png"
        $newObjectCase.ContentLanguage | Should Be "en"

        # Where Write-Gcs has both ContentType, ContentLanguage and a Metadata value.
        $both = "XXX" | Write-GcsObject $bucket "content-type-test" `
            -ContentType "test/alpha" -ContentLanguage "aa" `
            -Metadata @{ "Content-Type" = "test/beta"; "Content-Language" = "bb" }
        $both.ContentType | Should Be "test/alpha"
        $both.ContentLanguage | Should Be "aa"

        Remove-GcsObject $bucket "content-type-test"
    }
}

Describe "Test-GcsObject" {
    $bucket = "gcps-test-object-testing"
    Create-TestBucket $project $bucket

    function Test-TestGcsObjectSimple([switch]$gcsProvider) {
        $currentLocation = Resolve-Path .\
        try {
            if ($gcsProvider) {
                cd "gs:\$bucket"
                Test-GcsObject -ObjectName "test-obj" | Should Be $false
            }
            else {
                Test-GcsObject $bucket "test-obj" | Should Be $false
            }
            $obj = "can you hear me now?" | New-GcsObject -Bucket $bucket -ObjectName "test-obj"

            if ($gcsProvider) {
                Test-GcsObject -ObjectName "test-obj" | Should Be $true
            }
            else {
                Test-GcsObject $bucket "test-obj" | Should Be $true
            }
            $obj | Remove-GcsObject
        }
        finally {
            cd $currentLocation
        }
    }

    It "should work" {
        Test-TestGcsObjectSimple
    }

    It "should work in GCS Provider location" {
        Test-TestGcsObjectSimple -gcsProvider
    }

    It "should return false if the Bucket does not exist" {
        Test-GcsObject "bucket-aad2fjadkdmgzadfhj4" "obj.txt"| Should Be $false
    }

    It "should fail if the bucket is not accessible" {
        { Test-GcsObject "asdf" "gcs-object.txt" } | Should Throw
    }
}

Describe "Copy-GcsObject" {
    $r = Get-Random
    $bucket = "gcps-copy-object-testing-$r"

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
        
        It "Should fail to read from inaccessible source bucket" {
            { Copy-GcsObject -SourceBucket "asdf" -SourceObject "test-source" $bucket "test-dest" } |
                Should Throw 403
        }
        
        It "Should fail to write to inaccessible source bucket" {
            New-GcsObject $bucket "test-source0" -Value "test0-$r"
            { Copy-GcsObject -SourceBucket $bucket -SourceObject "test-source0" "asdf" "test-dest" } |
                Should Throw 403
        }

        It "Should work by name" {
            New-GcsObject $bucket "test-source" -Value "test1-$r"
            Copy-GcsObject -SourceBucket $bucket -SourceObject "test-source" $bucket "test-dest"
            Read-GcsObject $bucket "test-dest" | Should Be "test1-$r"
        }

        It "Should work by object" {
            $sourceObj = New-GcsObject $bucket "test-source2" -Value "test2-$r"
            $sourceObj | Copy-GcsObject $bucket "test-dest2"
            Read-GcsObject $bucket "test-dest2" | Should Be "test2-$r"
        }

        It "should work in GCS Provider location" {
            $currentLocation = Resolve-Path .\
            try {
                cd "gs:\$bucket"
                New-GcsObject -ObjectName "test-source" -Value "test1-$r" -Force
                Copy-GcsObject -SourceObjectName "test-source" -DestinationBucket $bucket -DestinationObjectName "test-dest" -Force
                Read-GcsObject -ObjectName "test-dest" | Should Be "test1-$r"

                $sourceObj = New-GcsObject -ObjectName "test-source2" -Value "test2-$r" -Force
                $sourceObj | Copy-GcsObject $bucket "test-dest2" -Force
                Read-GcsObject -ObjectName "test-dest2" | Should Be "test2-$r"
            }
            finally {
                cd $currentLocation
            }
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
