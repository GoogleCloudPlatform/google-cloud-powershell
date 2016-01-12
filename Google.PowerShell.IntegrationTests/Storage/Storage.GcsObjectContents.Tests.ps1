. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

Describe "Get-GcsObjectContents" {

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
             [System.DateTime]::Now.Ticks.ToString())
        Get-GcsObjectContents $bucket $testObjectName $tempFileName

        $fileContents = [System.IO.File]::ReadAllText($tempFileName)
        $fileContents | Should BeExactly $testFileContents

        Remove-Item -Force $tempFileName
    }

    It "won't overwrite existing files" {
        # Creates a 0-byte file, which we won't clobber.
        $tempFileName = [System.IO.Path]::GetTempFileName()
        # Pester automatically confirms the 
        { Get-GcsObjectContents $bucket $testObjectName $tempFileName } `
            | Should Throw "File Already Exists"

        Remove-Item -Force $tempFileName
    }

    It "will cobber files if -Overwrite is present" {
        # Creates a 0-byte file in the way.
        $tempFileName = [System.IO.Path]::GetTempFileName()
        Get-GcsObjectContents $bucket $testObjectName $tempFileName -Overwrite

        # Confirm the file has non-zero size.
        [System.IO.File]::ReadAllText($tempFileName) | Should Be $testFileContents
    }

    It "throws a 404 if the Storage Object does not exist" {
        $tempFileName = [System.IO.Path]::Combine(
             [System.IO.Path]::GetTempPath(),
             [System.DateTime]::Now.Ticks.ToString())
        { Get-GcsObjectContents $bucket "random-file" $tempFileName } `
            | Should Throw "404" 
    }

    It "failes if it doesn't have write access" {
        { Get-GcsObjectContents $bucket $testObjectName "C:\windows\helloworld.txt" } `
            | Should Throw "is denied" 
    }
    # TODO(chrsmith): Confirm it throws a 403 if you don't have GCS access.
    # TODO(chrsmith): Confirm it fails if you don't have write access to disk.
}
