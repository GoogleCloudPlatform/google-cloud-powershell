. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$script:powerShellProjectId = "gcloud-powershell-testing"
$script:powerShellProjectTwoId = "gcloud-powershell-testing-2"
$script:powerShellProjectName = "Google Powershell Testing"
$script:powerShellProjectTwoName = "GCloud PowerShell Testing 2"

Describe "Get-GcIamPolicyBinding" {
    It "should work" {
        $projects = Get-GcProject
        $project = $projects | Where-Object {$_.ProjectId -eq $powerShellProjectId}
        $projectTwo = $projects | Where-Object {$_.ProjectId -eq $powerShellProjectTwoId}

        $project.Name | Should BeExactly $powerShellProjectName
        $project.LifecycleState | Should Be ACTIVE
        $project.Labels | Should Not BeNullOrEmpty
        $projectTwo.Name | Should BeExactly $powerShellProjectTwoName
        $projectTwo.LifecycleState | Should Be ACTIVE
        $projectTwo.Labels | Should Not BeNullOrEmpty
    }

    It "should work with -Name" {
        $project = Get-GcProject -Name $powerShellProjectName
        $project.Name | Should BeExactly $powerShellProjectName
        $project.ProjectId | Should BeExactly $powerShellProjectId
    }

    It "should work with -ProjectId" {
        $project = Get-GcProject -ProjectId $powerShellProjectTwoId
        $project.Name | Should BeExactly $powerShellProjectTwoName
        $project.ProjectId | Should BeExactly $powerShellProjectTwoId
    }

    It "should work with -Label" {
        # Both projects have this label.
        $commonLabel = @{"gcloud-powershell-testing" = "testing"}
        $projects = Get-GcProject -Label $commonLabel
        $project = $projects | Where-Object {$_.ProjectId -eq $powerShellProjectId}
        $projectTwo = $projects | Where-Object {$_.ProjectId -eq $powerShellProjectTwoId}

        $projects.Count | Should Be 2
        $project.Name | Should BeExactly $powerShellProjectName
        $projectTwo.Name | Should BeExactly $powerShellProjectTwoName
    }

    It "should work with multiple labels" {
        # Only the second project has both of these labels.
        $labels = @{"gcloud-powershell-testing" = "testing"; "powershell" = "gcloud"}
        $project = Get-GcProject -Label $labels
        $project.Name | Should BeExactly $powerShellProjectTwoName
        $project.ProjectId | Should BeExactly $powerShellProjectTwoId
    }

    It "should work with multiple parameters" {
        $commonLabel = @{"gcloud-powershell-testing" = "testing"}
        $project = Get-GcProject -Name $powerShellProjectName -Label $commonLabel
        $project.Name | Should BeExactly $powerShellProjectName
        $project.ProjectId | Should BeExactly $powerShellProjectId

        $projectTwo = Get-GcProject -ProjectId $powerShellProjectId -Label $commonLabel
        $project.Name | Should BeExactly $powerShellProjectName
        $project.ProjectId | Should BeExactly $powerShellProjectId
    }
}
