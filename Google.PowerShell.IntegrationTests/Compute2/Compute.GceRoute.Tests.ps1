. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$r = Get-Random

$routeName = "test-route-$r"
$allIps = "0.0.0.0/0"
$defaultNetwork = Get-GceNetwork default
$defaultRouteCount = (Get-GceRoute).Count

Describe "Add-GceRoute" {
    It "should fail for wrong project" {
        { Add-GceRoute $routeName $allIps $defaultNetwork -Project "asdf" } | Should Throw 403
    }

    Context "add success" {
        AfterEach {
            Remove-GceRoute $routeName
        }

        It "should work" {
            $addedRoute = Add-GceRoute $routeName $allIps $defaultNetwork -Priority 9001 -Description "for testing $r"`
                -Tag "alpha", "beta" -NextHopInternetGateway
            $addedRoute.Name | Should Be $routeName
            $addedRoute.DestRange | Should Be $allIps
            $addedRoute.Network | Should Be $defaultNetwork.SelfLink
            $addedRoute.Priority | Should Be 9001
            $addedRoute.Description | Should Be "for testing $r"
            $addedRoute.Tags.Count | Should Be 2
            $addedRoute.Tags -contains "alpha" | Should Be $true
            $addedRoute.Tags -contains "beta" | Should Be $true
            $addedRoute.NextHopGateway | Should Match "global/gateways/default-internet-gateway"

            # Make sure the object we get from Add-GceRoute is the same as we get from Get-GceRoute.
            $getRoute = Get-GceRoute $routeName
            Compare-Object $getRoute $addedRoute | Should BeNullOrEmpty
        }

        It "should work with object" {
            # Get the existing default internet gateway route.
            $rootRoute = Get-GceRoute |
                Where { $_.NextHopGateway -ne $null -and $_.Network -eq $defaultNetwork.SelfLink } |
                Select -First 1

            # Change its properties so we can create a new route.
            $rootRoute.Name = $routeName
            $rootRoute.DestRange = $allIps
            $rootRoute.Priority = 9001
            $rootRoute.Id = $null
            $rootRoute.Description ="for testing $r"

            # Create a new route.
            $addedRoute = Add-GceRoute $rootRoute

            $addedRoute.Name | Should Be $routeName
            $addedRoute.DestRange | Should Be $allIps
            $addedRoute.Network | Should Be $defaultNetwork.SelfLink
            $addedRoute.Priority | Should Be 9001
            $addedRoute.Description | Should Be "for testing $r"

            # Make sure the object we get from Add-GceRoute is the same as we get from Get-GceRoute.
            $getRoute = Get-GceRoute $routeName
            Compare-Object $getRoute $addedRoute | Should BeNullOrEmpty
        }

        It "should work with object on pipeline" {
            # Get the existing default internet gateway route
            $rootRoute = Get-GceRoute | Where { $_.NextHopGateway -ne $null } | Select -First 1

            # Change its properties so we can create a new route.
            $rootRoute.Name = $routeName
            $rootRoute.DestRange = $allIps
            $rootRoute.Priority = 9001
            $rootRoute.Network = $defaultNetwork.SelfLink
            $rootRoute.Id = $null
            $rootRoute.Description ="for testing $r"

            # Create a new route.
            $addedRoute = $rootRoute | Add-GceRoute

            $addedRoute.Name | Should Be $routeName
            $addedRoute.DestRange | Should Be $allIps
            $addedRoute.Network | Should Be $defaultNetwork.SelfLink
            $addedRoute.Priority | Should Be 9001
            $addedRoute.Description | Should Be "for testing $r"

            # Make sure the object we get from Add-GceRoute is the same as we get from Get-GceRoute.
            $getRoute = Get-GceRoute $routeName
            Compare-Object $getRoute $addedRoute | Should BeNullOrEmpty
        }
        
        $image = Get-GceImage debian-cloud -Family debian-8
        $instanceName = "test-route-instance-$r"
        New-GceInstanceConfig $instanceName -DiskImage $image -MachineType "f1-micro" -CanIpForward |
            Add-GceInstance -Project $project -Zone $zone
        $instance = Get-GceInstance $instanceName

        It "should route to an instance" {
            $addedRoute = Add-GceRoute $routeName $allIps $defaultNetwork -Priority 9001 -NextHopInstance $instance
            $addedRoute.NextHopInstance | Should Be $instance.SelfLink
        }

        It "should route to an IP" {
            $ip = $instance.NetworkInterfaces.NetworkIp
            $addedRoute = Add-GceRoute $routeName $allIps $defaultNetwork -Priority 9001 -NextHopIp $ip
            $addedRoute.NextHopIP | Should Be $ip
        }

        Remove-GceInstance $instance

        # TODO(jimwp): Add a VPN tunnel and test for it.
    }
}

Describe "Get-GceRoute" {
    It "should fail for wrong project" {
        { Get-GceRoute $routeName -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant route" {
        { Get-GceRoute $routeName } | Should Throw 404
    }

    Add-GceRoute $routeName $allIps $defaultNetwork -Priority 9001 -NextHopInternetGateway

    It "should list for project" {
        $routes = Get-GceRoute
        # The one we created, plus the default internet gateway route plus subnetwork (region) routes.
        $routes.Count | Should Be ($defaultRouteCount + 1)
        ($routes | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Route }
    }

    It "should get by name" {
        $route = Get-GceRoute $routeName
        $route.Count | Should Be 1
        ($route | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Route }
        $route.Priority | Should Be 9001
        $route.DestRange | Should Be $allIps
        $route.NextHopGateway | Should Match "global/gateways/default-internet-gateway"
    }

    It "should get by name with pipeline" {
        $route = $routeName | Get-GceRoute
        ($route | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Route }
        $route.Count | Should Be 1
        $route.Priority | Should Be 9001
        $route.DestRange | Should Be $allIps
        $route.NextHopGateway | Should Match "global/gateways/default-internet-gateway"
    }

    Remove-GceRoute $routeName
}

Describe "Remove-GceRoute" {
    It "should fail for wrong project" {
        { Remove-GceRoute $routeName -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant route" {
        { Remove-GceRoute $routeName } | Should Throw 404
    }

    Context "remove sucess" {
        BeforeEach {
            Add-GceRoute $routeName $allIps $defaultNetwork -Priority 9001 -NextHopInternetGateway
        }

        It "should work with name" {
            Remove-GceRoute $routeName
            { Get-GceRoute $routeName } | Should Throw 404
            (Get-GceRoute).Count | Should Be $defaultRouteCount
        }

        It "should work with object" {
            $route = Get-GceRoute $routeName
            Remove-GceRoute $route
            { Get-GceRoute $routeName } | Should Throw 404
            (Get-GceRoute).Count | Should Be $defaultRouteCount
        }

        It "should work with object on pipeline" {
            $route = Get-GceRoute $routeName
            $route | Remove-GceRoute 
            { Get-GceRoute $routeName } | Should Throw 404
            (Get-GceRoute).Count | Should Be $defaultRouteCount
        }
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
