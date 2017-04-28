. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$script:powerShellProjectId = "gcloud-powershell-testing"
$script:powerShellProjectTwoId = "gcloud-powershell-testing-2"
$script:powerShellProjectName = "Google Powershell Testing"
$script:powerShellProjectTwoName = "GCloud PowerShell Testing 2"

Describe "Get-GcpProject" {
    It "should work" {
        $projects = Get-GcpProject
        $project = $projects | Where ProjectId -eq $powerShellProjectId
        $projectTwo = $projects | Where ProjectId -eq $powerShellProjectTwoId

        $project.Name | Should BeExactly $powerShellProjectName
        $project.LifecycleState | Should Be ACTIVE
        $project.Labels | Should Not BeNullOrEmpty
        $projectTwo.Name | Should BeExactly $powerShellProjectTwoName
        $projectTwo.LifecycleState | Should Be ACTIVE
        $projectTwo.Labels | Should Not BeNullOrEmpty
    }

    It "should work with -Name" {
        $project = Get-GcpProject -Name $powerShellProjectName
        $project.Name | Should BeExactly $powerShellProjectName
        $project.ProjectId | Should BeExactly $powerShellProjectId
    }

    It "should work with -ProjectId" {
        $project = Get-GcpProject -ProjectId $powerShellProjectTwoId
        $project.Name | Should BeExactly $powerShellProjectTwoName
        $project.ProjectId | Should BeExactly $powerShellProjectTwoId
    }

    It "should work with -Label" {
        # Both projects have this label.
        $commonLabel = @{"gcloud-powershell-testing" = "testing"}
        $projects = Get-GcpProject -Label $commonLabel
        $project = $projects | Where ProjectId -eq $powerShellProjectId
        $projectTwo = $projects | Where ProjectId -eq $powerShellProjectTwoId

        $project.Name | Should BeExactly $powerShellProjectName
        $projectTwo.Name | Should BeExactly $powerShellProjectTwoName
    }

    It "should work with multiple labels" {
        # Only the second project has both of these labels.
        $labels = @{"gcloud-powershell-testing" = "testing"; "powershell" = "gcloud"}
        $project = Get-GcpProject -Label $labels
        $project.Name | Should BeExactly $powerShellProjectTwoName
        $project.ProjectId | Should BeExactly $powerShellProjectTwoId
    }

    It "should work with multiple parameters" {
        $commonLabel = @{"gcloud-powershell-testing" = "testing"}
        $project = Get-GcpProject -Name $powerShellProjectName -Label $commonLabel
        $project.Name | Should BeExactly $powerShellProjectName
        $project.ProjectId | Should BeExactly $powerShellProjectId

        $projectTwo = Get-GcpProject -ProjectId $powerShellProjectId -Label $commonLabel
        $project.Name | Should BeExactly $powerShellProjectName
        $project.ProjectId | Should BeExactly $powerShellProjectId
    }
}
