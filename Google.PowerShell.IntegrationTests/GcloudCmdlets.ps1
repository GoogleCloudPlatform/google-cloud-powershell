# Install the GCP cmdlets module into the current PowerShell session.
function Install-GcloudCmdlets() {
	# TODO(chrsmith): Check both the Debug and Release, use most recent.
	$pathToCmdlets = "$PSScriptRoot\..\Google.PowerShell\bin\Debug\Google.PowerShell.dll"
	Import-Module $pathToCmdlets -Verbose
}
# TODO(chrsmith): Provide a "initialize unit tests" method, which also sets common properties like $project.