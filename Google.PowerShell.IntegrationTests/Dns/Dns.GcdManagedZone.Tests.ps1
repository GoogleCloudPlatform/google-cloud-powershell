. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project = "gcloud-powershell-testing"

Describe "Get-GcdManagedZone" {

	It "should fail to return managed zones of non-existent project" {
        { Get-GcdManagedZone -Project "project-no-exist" } | Should Throw "400"
    }

	It "should give access errors as appropriate" {
        # Don't know who created the "asdf" project.
        { Get-GcdManagedZone -Project "asdf" } | Should Throw "403"
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

	It "should fail to return non-existent managed zones of existing project" {
        { Get-GcdManagedZone -Project $project -ManagedZone "managedZone-no-exist" } | Should Throw "404"
    }

	It "should list exactly 0 managed zones in project" {
        (Get-GcdManagedZone -Project $project).Count -eq 0 | Should Be $true
    }

	# Create zone for testing 
	gcloud dns managed-zones create --dns-name="gcloudexample.com." --description="testing zone, 1" "test1"

	It "should list exactly 1 managed zone in project" {
        (Get-GcdManagedZone -Project $project).Count -eq 1 | Should Be $true
    }

	It "should work and list the 1 managed zone just created" {
        $zones = Get-GcdManagedZone -Project $project
		$zones.GetType().FullName | Should Match "Google.Apis.Dns.v1.Data.ManagedZone"
		$zones.Description | Should Match "testing zone, 1"
		$zones.DnsName | Should Match "gcloudexample.com"
		$zones.Kind | Should Match "dns#managedZone"
		$zones.Name | Should Match "test1"
	}

	# Create second zone for testing
	gcloud dns managed-zones create --dns-name="gcloudexample2.com." --description="testing zone, 2" "test2"

	It "should list exactly 2 managed zones in project" {
        (Get-GcdManagedZone -Project $project).Count -eq 2 | Should Be $true
    }

	It "should work and list the 2 managed zones just created" {
        $zones = Get-GcdManagedZone -Project $project

		# The type and Kind should be the same for all managed zones
		$zones[0].GetType().FullName | Should Match "Google.Apis.Dns.v1.Data.ManagedZone"
		$zones[1].GetType().FullName | Should Match "Google.Apis.Dns.v1.Data.ManagedZone"
		($zones.Kind -match "dns#managedZone").Count | Should Be 2

		($zones.Description -match "testing zone, 1").Count | Should Be 1
		($zones.DnsName -match "gcloudexample.com").Count | Should Be 1
		($zones.Name -match "test1").Count | Should Be 1

		($zones.Description -match "testing zone, 2").Count | Should Be 1
		($zones.DnsName -match "gcloudexample2.com").Count | Should Be 1
		($zones.Name -match "test2").Count | Should Be 1
	}

	It "should work and retrieve managed zone test1" {
        $zones = Get-GcdManagedZone -Project $project -ManagedZone "test1"
		$zones.GetType().FullName | Should Match "Google.Apis.Dns.v1.Data.ManagedZone"
		$zones.Description | Should Match "testing zone, 1"
		$zones.DnsName | Should Match "gcloudexample.com"
		$zones.Kind | Should Match "dns#managedZone"
		$zones.Name | Should Match "test1"
	}

	It "should work and retrieve managed zone test2" {
        $zones = Get-GcdManagedZone -Project $project -ManagedZone "test2"
		$zones.GetType().FullName | Should Match "Google.Apis.Dns.v1.Data.ManagedZone"
		$zones.Description | Should Match "testing zone, 2"
		$zones.DnsName | Should Match "gcloudexample2.com"
		$zones.Kind | Should Match "dns#managedZone"
		$zones.Name | Should Match "test2"
	}

	# Delete test zones
	gcloud dns managed-zones delete "test1"
	gcloud dns managed-zones delete "test2"
}
