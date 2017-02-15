. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$script:appveyorServiceAccEmail = "serviceAccount:appveyorci-testing@gcloud-powershell-testing.iam.gserviceaccount.com"

Describe "Get-GcIamPolicyBinding" {
    It "should work" {
        $policies = Get-GcIamPolicyBinding
        $policies.Count -gt 0 | Should Be $true

        $editors = $policies | Where-Object {$_.Role -eq "roles/editor"}
        $editors.Members.Count -gt 0 | Should Be $true
        $editors.Members -contains $script:appveyorServiceAccEmail | Should Be $true
    }

    It "should throw if we don't have permission" {
        { Get-GcIamPolicyBinding -Project "asdf" } | Should Throw "403"
    }
}