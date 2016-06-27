. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

Describe "Get-GcdProject" {

    It "should fail to return representation of non-existent project" {
        { Get-GcdProject -Project "project-no-exist" } | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdProject -Project "asdf" } | Should Throw "403"
    }

    It "should work and retrieve valid project information" {
        $projectInfo = Get-GcdProject -Project $project

        $projectInfo.GetType().FullName | Should Match "Google.Apis.Dns.v1.Data.Project"
        $projectInfo.Id | Should Match $project
        $projectInfo.Kind | Should Match "dns#project"

        $projectInfo.Quota.ManagedZones -ge 50 | Should Be $true
        $projectInfo.Quota.ResourceRecordsPerRrset -ge 50 | Should Be $true
        $projectInfo.Quota.RrsetAdditionsPerChange -ge 50 | Should Be $true
        $projectInfo.Quota.RrsetDeletionsPerChange -ge 50 | Should Be $true
        $projectInfo.Quota.RrsetsPerManagedZone -ge 9000 | Should Be $true
        $projectInfo.Quota.TotalRrdataSizePerChange -ge 9000 | Should Be $true
    }
}
