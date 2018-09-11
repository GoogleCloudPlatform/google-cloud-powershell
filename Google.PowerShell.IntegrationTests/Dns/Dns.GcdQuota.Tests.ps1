. $PSScriptRoot\..\Dns\GcdCmdlets.ps1
Install-GcloudCmdlets
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcdQuota" {

    It "should fail to return DNS quota of non-existent project" {
        { Get-GcdQuota -Project $nonExistProject } | Should Throw "403"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdQuota -Project $accessErrProject } | Should Throw "403"
    }

    It "should work and retrieve valid DNS quota information for a named project" {
        $quotaInfo = Get-GcdQuota -Project $project

        $quotaInfo.GetType().FullName | Should Match $quotaType

        $quotaInfo.ManagedZones -ge 50 | Should Be $true
        $quotaInfo.ResourceRecordsPerRrset -ge 50 | Should Be $true
        $quotaInfo.RrsetAdditionsPerChange -ge 50 | Should Be $true
        $quotaInfo.RrsetDeletionsPerChange -ge 50 | Should Be $true
        $quotaInfo.RrsetsPerManagedZone -ge 9000 | Should Be $true
        $quotaInfo.TotalRrdataSizePerChange -ge 9000 | Should Be $true
    }

    It "should work and retrieve valid DNS quota information for the current config's project" {
        $quotaInfo = Get-GcdQuota

        $quotaInfo.GetType().FullName | Should Match $quotaType

        $quotaInfo.ManagedZones -ge 50 | Should Be $true
        $quotaInfo.ResourceRecordsPerRrset -ge 50 | Should Be $true
        $quotaInfo.RrsetAdditionsPerChange -ge 50 | Should Be $true
        $quotaInfo.RrsetDeletionsPerChange -ge 50 | Should Be $true
        $quotaInfo.RrsetsPerManagedZone -ge 9000 | Should Be $true
        $quotaInfo.TotalRrdataSizePerChange -ge 9000 | Should Be $true
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
