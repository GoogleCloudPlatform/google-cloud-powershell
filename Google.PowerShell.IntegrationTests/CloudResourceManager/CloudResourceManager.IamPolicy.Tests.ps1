. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GceInstance" {
    It "should work" {
        $policies = Get-GcIamPolicy
        $policies.Count -gt 0 | Should Be $true

        $editors = $policies | Where-Object {$_.Role -eq "roles/editor"}
        $editors.Members.Count -gt 0 | Should Be $true
        $editors.Members -contains "serviceAccount:appveyorci-testing@gcloud-powershell-testing.iam.gserviceaccount.com" |
            Should Be $true
    }

    It "should throw if we don't have permission" {
        { Get-GcIamPolicy -Project "asdf" } | Should Throw "403"
    }
}