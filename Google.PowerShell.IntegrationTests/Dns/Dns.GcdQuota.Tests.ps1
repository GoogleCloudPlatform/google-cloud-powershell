. $PSScriptRoot\..\Dns\GcdCmdlets.ps1

Describe "Get-GcdQuota" {

    It "should fail to return DNS quota of non-existent project" {
        { Get-GcdQuota -DnsProject $nonExistProject } | Should Throw "400"
    }

    It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdQuota -DnsProject $accessErrProject } | Should Throw "403"
    }

    It "should work and retrieve valid DNS quota information" {
        $quotaInfo = Get-GcdQuota -DnsProject $project

        $quotaInfo.GetType().FullName | Should Match $quotaType

        $quotaInfo.ManagedZones -ge 50 | Should Be $true
        $quotaInfo.ResourceRecordsPerRrset -ge 50 | Should Be $true
        $quotaInfo.RrsetAdditionsPerChange -ge 50 | Should Be $true
        $quotaInfo.RrsetDeletionsPerChange -ge 50 | Should Be $true
        $quotaInfo.RrsetsPerManagedZone -ge 9000 | Should Be $true
        $quotaInfo.TotalRrdataSizePerChange -ge 9000 | Should Be $true
    }
}
