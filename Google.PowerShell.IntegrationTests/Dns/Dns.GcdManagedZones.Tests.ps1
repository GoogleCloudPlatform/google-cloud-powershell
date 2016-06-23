. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

Describe "Get-GcdManagedZones" {

    It "should fail to return managed zones of non-existent project" {
        { Get-GcdManagedZones -Project "project-no-exist" } | Should Throw "400"
    }

	It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdManagedZones -Project "asdf" } | Should Throw "403"
    }

	# Delete all existing zones
	$preExistingZones = gcloud dns managed-zones list --project $project

	if ($preExistingZones.Count -gt 0) {
		$preExistingZones = $preExistingZones[1..($preExistingZones.length-1)]

		ForEach ($zoneDescrip in $preExistingZones) {
			$zoneName = $zoneDescrip.Split(" ")[0]
			gcloud dns managed-zones delete $zoneName
		}
	}

	It "should list all (0) managed zones in a project" {
        (Get-GcdManagedZones -Project $project).Count -eq 0 | Should Be $true
    }

	#Create zone for testing 
	gcloud dns managed-zones create --dns-name="gcloudexample.com." --description="testing zone, 1" "test1"

	It "should work and list the managed zone just created" {
        $zones = Get-GcdManagedZones -Project $project
		$zones[0].GetType().FullName | Should Match "Google.Apis.Dns.v1.Data.ManagedZone"
		$zones[0].Description | Should Match "testing zone, 1"
		$zones[0].DnsName | Should Match "gcloudexample.com"
		$zones[0].Kind | Should Match "dns#managedZone"
		$zones[0].Name | Should Match "test1"
	}

	It "should list all (1) managed zones in a project" {
        (Get-GcdManagedZones -Project $project).Count -eq 1 | Should Be $true
    }

	#Delete test zone
	gcloud dns managed-zones delete "test1"
}
