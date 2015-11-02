# Install the GCP cmdlets module into the current PowerShell session.
function Install-GcloudCmdlets() {
	# TODO(chrsmith): Check both the Debug and Release, use most recent.
	$pathToCmdlets = "$PSSCriptRoot\..\Google.PowerShell\bin\Debug\Google.PowerShell.dll"
	Import-Module $pathToCmdlets -Verbose
}

