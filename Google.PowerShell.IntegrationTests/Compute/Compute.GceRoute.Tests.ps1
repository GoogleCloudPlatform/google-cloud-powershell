. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$r = Get-Random

Describe "Add-GceRoute" {
    $routeName = "test-route-$r"
    $allIps = "0.0.0.0/0"
    $network = Get-GceNetwork default

    It "should fail for wrong project" {
        { Add-GceRoute $routeName $allIps $network -Project "asdf" } | Should Throw 403
    }

    Context "add success" {
        AfterEach {
            Remove-GceRoute $routeName
        }

        It "should work" {
            $addRoute = Add-GceRoute $routeName $allIps $network -Priority 9001 -Description "for testing $r"`
                -Tag "alpha", "beta" -NextHopInternetGateway
            $addRoute.Name | Should Be $routeName
            $addRoute.DestRange | Should Be $allIps
            $addRoute.Network | Should Be $network.SelfLink
            $addRoute.Priority | Should Be 9001
            $addRoute.Description | Should Be "for testing $r"
            $addRoute.Tags.Count | Should Be 2
            $addRoute.Tags-contains "alpha" | Should Be $true
            $addRoute.Tags -contains "beta" | Should Be $true
            $addRoute.NextHopGateway | Should Match "global/gateways/default-internet-gateway"
            $getRoute = Get-GceRoute $routeName
            Compare-Object $getRoute $addRoute | Should BeNullOrEmpty
        }

        It "should work with object" {
            # Get the existing default internet gateway route
            $rootRoute = Get-GceRoute | Where { $_.NextHopGateway -ne $null } | Select -First 1
            $rootRoute.Name = $routeName
            $rootRoute.DestRange = $allIps
            $rootRoute.Priority = 9001
            $rootRoute.Id = $null
            $rootRoute.Description ="for testing $r"

            $addRoute = Add-GceRoute $rootRoute

            $addRoute.Name | Should Be $routeName
            $addRoute.DestRange | Should Be $allIps
            $addRoute.Network | Should Be $network.SelfLink
            $addRoute.Priority | Should Be 9001
            $addRoute.Description | Should Be "for testing $r"
            $getRoute = Get-GceRoute $routeName
            Compare-Object $getRoute $addRoute | Should BeNullOrEmpty
        }

        It "should work with object on pipeline" {
            # Get the existing default internet gateway route
            $rootRoute = Get-GceRoute | Where { $_.NextHopGateway -ne $null } | Select -First 1
            $rootRoute.Name = $routeName
            $rootRoute.DestRange = $allIps
            $rootRoute.Priority = 9001
            $rootRoute.Network = $network.SelfLink
            $rootRoute.Id = $null
            $rootRoute.Description ="for testing $r"
            
            $addRoute = $rootRoute | Add-GceRoute

            $addRoute.Name | Should Be $routeName
            $addRoute.DestRange | Should Be $allIps
            $addRoute.Network | Should Be $network.SelfLink
            $addRoute.Priority | Should Be 9001
            $addRoute.Description | Should Be "for testing $r"
            $getRoute = Get-GceRoute $routeName
            Compare-Object $getRoute $addRoute | Should BeNullOrEmpty
        }
        
        $image = "projects/debian-cloud/global/images/debian-8-jessie-v20160511"
        $instanceName = "test-route-instance-$r"
        New-GceInstanceConfig $instanceName -DiskImage $image -MachineType "f1-micro" -CanIpForward $true |
            Add-GceInstance -Project $project -Zone $zone
        $instance = Get-GceInstance $instanceName

        It "should route to an instance" {
            $addRoute = Add-GceRoute $routeName $allIps $network -Priority 9001 -NextHopInstance $instance
            $addRoute.NextHopInstance | Should Be $instance.SelfLink
        }

        It "should route to an ip" {
            $ip = $instance.NetworkInterfaces.NetworkIp
            $addRoute = Add-GceRoute $routeName $allIps $network -Priority 9001 -NextHopIp $ip
            $addRoute.NextHopIP | Should Be $ip
        }

        Remove-GceInstance $instance

        # TODO: Add a vpn tunnel and test for it.
    }
}

Describe "Get-GceRoute" {
    $routeName = "test-route-$r"
    $allIps = "0.0.0.0/0"
    $network = Get-GceNetwork default

    It "should fail for wrong project" {
        { Get-GceRoute $routeName -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant route" {
        { Get-GceRoute $routeName } | Should Throw 404
    }

    Add-GceRoute $routeName $allIps $network -Priority 9001 -NextHopInternetGateway

    It "should list for project" {
        $routes = Get-GceRoute
        $routes.Count | Should Be 6 # the 1 we created, plus the default internet gateway route plus 4 subnetwork routes.
        ($routes | Get-Member).TypeName | Should Be Google.Apis.Compute.v1.Data.Route
    }

    It "should get by name" {
        $route = Get-GceRoute $routeName
        $route.Count | Should Be 1
        ($route | Get-Member).TypeName | Should Be Google.Apis.Compute.v1.Data.Route
        $route.Priority | Should Be 9001
        $route.DestRange | Should Be $allIps
        $route.NextHopGateway | Should Match "global/gateways/default-internet-gateway"
    }

    It "should get by name with pipeline" {
        $route = $routeName | Get-GceRoute
        ($route | Get-Member).TypeName | Should Be Google.Apis.Compute.v1.Data.Route
        $route.Count | Should Be 1
        $route.Priority | Should Be 9001
        $route.DestRange | Should Be $allIps
        $route.NextHopGateway | Should Match "global/gateways/default-internet-gateway"
    }

    Remove-GceRoute $routeName
}

Describe "Remove-GceRoute" {
    $routeName = "test-route-$r"
    $allIps = "0.0.0.0/0"
    $network = Get-GceNetwork default

    It "should fail for wrong project" {
        { Remove-GceRoute $routeName -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant route" {
        { Remove-GceRoute $routeName } | Should Throw 404
    }

    Context "remove sucess" {
        BeforeEach {
            Add-GceRoute $routeName $allIps $network -Priority 9001 -NextHopInternetGateway
        }

        It "should work with name" {
            Remove-GceRoute $routeName
            { Get-GceRoute $routeName } | Should Throw 404
        }

        It "should work with object" {
            $route = Get-GceRoute $routeName
            Remove-GceRoute $route
            { Get-GceRoute $routeName } | Should Throw 404
        }

        It "should work with object on pipeline" {
            $route = Get-GceRoute $routeName
            $route | Remove-GceRoute 
            { Get-GceRoute $routeName } | Should Throw 404
        }
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
