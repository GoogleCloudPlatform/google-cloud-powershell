# TODO(chrsmith): Provide a "initialize unit tests" method, which also sets common properties like $project.

# Install the GCP cmdlets module into the current PowerShell session.
function Install-GcloudCmdlets() {
    $dll = Get-ChildItem $PSScriptRoot\..\Google.PowerShell\bin -Recurse -Include Google.PowerShell.dll |
        sort -Property CreationTime | select -First 1
    Import-Module $dll -Verbose
}

# Creates a GCS bucket owned associated with the project, deleting any existing
# buckets with that name and all of their contents.
function Create-TestBucket($project, $bucket) {
    gsutil -m rm -r "gs://${bucket}/*"
    gsutil rb "gs://${bucket}"
    gsutil mb -p $project "gs://${bucket}"
}

# Copies a 0-byte file from the local machine to Google Cloud Storage.
function Add-TestFile($bucket, $objName) {
    $filename = [System.IO.Path]::GetTempFileName()
    gsutil ls "gs://${bucket}"
    gsutil cp $filename "gs://${bucket}/${objName}"
    Remove-Item -Force $filename
}
