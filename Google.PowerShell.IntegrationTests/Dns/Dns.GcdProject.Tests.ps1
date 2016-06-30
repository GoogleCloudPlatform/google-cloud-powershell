. $PSScriptRoot\..\Dns\GcdCmdlets.ps1

Describe "Get-GcdProject" {
    BeforeAll {
        Remove-AllManagedZone($project)
    }

    It "should fail to return representation of non-existent project" {
        { Get-GcdProject -Project $nonExistProject } | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdProject -Project $accessErrProject } | Should Throw "403"
    }

    It "should work and retrieve valid project information" {
        $projectInfo = Get-GcdProject -Project $project

        $projectInfo.GetType().FullName | Should Match $projectType
        $projectInfo.Id | Should Match $project
        $projectInfo.Kind | Should Match $projectKind

        $projectInfo.Quota.ManagedZones -ge 50 | Should Be $true
        $projectInfo.Quota.ResourceRecordsPerRrset -ge 50 | Should Be $true
        $projectInfo.Quota.RrsetAdditionsPerChange -ge 50 | Should Be $true
        $projectInfo.Quota.RrsetDeletionsPerChange -ge 50 | Should Be $true
        $projectInfo.Quota.RrsetsPerManagedZone -ge 9000 | Should Be $true
        $projectInfo.Quota.TotalRrdataSizePerChange -ge 9000 | Should Be $true
    }
}
