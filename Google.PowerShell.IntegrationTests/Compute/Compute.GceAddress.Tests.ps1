. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

$r = Get-Random
Get-GceAddress | Remove-GceAddress
Get-GceAddress -Global | Remove-GceAddress

Describe "Add-GceAddress" {
    $addressName = "test-add-address-$r"

    It "should fail with wrong project" {
        { Add-GceAddress $addressName -Project "asdf" } | Should Throw 403
    }

    Context "make region specific addresses" {
        AfterEach {
            Get-GceAddress | Remove-GceAddress
        }

        It "should make region address by name" {
            Add-GceAddress $addressName -Description "for testing"
            $address = Get-GceAddress $addressName
            $address.Name | Should Be $addressName
            $address.AddressValue | Should Not BeNullOrEmpty
            $address.Region | Should Match "us-central1"
            $address.Description | Should Be "for testing"
        }

        It "should make non-default region address by name" {
            Add-GceAddress $addressName -Region "us-east1"
            $address = Get-GceAddress $addressName -Region "us-east1"
            $address.Name | Should Be $addressName
            $address.AddressValue | Should Not BeNullOrEmpty
            $address.Region | Should Match "us-east1"
        }

        It "should make region address by name with pipeline" {
            $addressName | Add-GceAddress
            $address = Get-GceAddress $addressName
            $address.Name | Should Be $addressName
            $address.AddressValue | Should Not BeNullOrEmpty
            $address.Region | Should Match "us-central1"
        }

        It "should make region address with object" {
            $obj = New-Object Google.Apis.Compute.v1.Data.Address
            $obj.Name = $addressName
            $obj.Description = "for testing"
            Add-GceAddress $obj
            $address = Get-GceAddress $addressName
            $address.Name | Should Be $addressName
            $address.AddressValue | Should Not BeNullOrEmpty
            $address.Region | Should Match "us-central1"
            $address.Description | Should Be "for testing"
        }

        It "should make region address with object using pipeline" {
            $obj = New-Object Google.Apis.Compute.v1.Data.Address
            $obj.Name = $addressName
            $obj | Add-GceAddress 
            $address = Get-GceAddress $addressName
            $address.Name | Should Be $addressName
            $address.AddressValue | Should Not BeNullOrEmpty
            $address.Region | Should Match "us-central1"
        }
    }

    Context "make global addresses" {
        AfterEach {
            Remove-GceAddress $addressName -Global
        }

        It "should make global address by name" {
            Add-GceAddress $addressName -Description "for testing" -Global
            $address = Get-GceAddress $addressName -Global
            $address.Name | Should Be $addressName
            $address.AddressValue | Should Not BeNullOrEmpty
            $address.Region | Should BeNullOrEmpty
            $address.Description | Should Be "for testing"
        }

        It "should make global address by name with pipeline" {
            $addressName | Add-GceAddress -Global
            $address = Get-GceAddress $addressName -Global
            $address.Name | Should Be $addressName
            $address.AddressValue | Should Not BeNullOrEmpty
            $address.Region | Should BeNullOrEmpty
        }

        It "should make global address with object" {
            $obj = New-Object Google.Apis.Compute.v1.Data.Address
            $obj.Name = $addressName
            $obj.Description = "for testing"
            Add-GceAddress $obj -Global
            $address = Get-GceAddress $addressName -Global
            $address.Name | Should Be $addressName
            $address.AddressValue | Should Not BeNullOrEmpty
            $address.Region | Should BeNullOrEmpty
            $address.Description | Should Be "for testing"
        }

        It "should make global address with object using pipeline" {
            $obj = New-Object Google.Apis.Compute.v1.Data.Address
            $obj.Name = $addressName
            $obj | Add-GceAddress -Global
            $address = Get-GceAddress $addressName -Global
            $address.Name | Should Be $addressName
            $address.AddressValue | Should Not BeNullOrEmpty
            $address.Region | Should BeNullOrEmpty
        }
    }
}

Describe "Get-GceAddress" {
    $addressName1 = "test-add-address1-$r"
    $addressName2 = "test-add-address2-$r"
    $addressName3 = "test-add-address3-$r"
    $globalAddressName1 = "test-add-address-global1-$r"
    $globalAddressName2 = "test-add-address-global2-$r"

    $addressName1, $addressName2 | Add-GceAddress

    Add-GceAddress $addressName3 -Region "us-east1"

    $globalAddressName1, $globalAddressName2 | Add-GceAddress -Global
    
    It "should fail for wrong project." {
        { Get-GceAddress -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant address" {
        { Get-GceAddress "not-exist-address" } | Should Throw 404
    }

    It "should get all address of project, including both global and region specific" {
        $addresses = Get-GceAddress
        ($addresses | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Address }
        $addresses.Count | Should Be 5
        ($addresses.Name -eq $addressName1).Count | Should Be 1
        ($addresses.Name -eq $globalAddressName1).Count | Should Be 1
        $addresses.AddressValue | Should Not BeNullOrEmpty
    }

    It "should get all region addresses" {
        $addresses = Get-GceAddress -Region "us-central1"
        ($addresses | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Address }
        $addresses.Count | Should Be 2
        ($addresses.Name -eq $addressName1).Count | Should Be 1
        $addresses.Name | Should Not Be $addressName3
        $addresses.AddressValue | ForEach-Object { $_ | Should Not BeNullOrEmpty }
        $addresses.Region | Should Match "us-central1"

        # Try with non-default region.
        $address = Get-GceAddress -Region "us-east1"
        ($address | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Address }
        $address.Count | Should Be 1
        $address.Name | Should Be $addressName3
        $address.AddressValue | Should Not BeNullOrEmpty
        $address.Region | Should Match "us-east1"
    }

    It "should get region address by name" {
        $address = Get-GceAddress $addressName1
        ($address | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Address }
        $address.Count | Should Be 1
        $address.Name | Should Be $addressName1
        $address.AddressValue | Should Not BeNullOrEmpty
        $address.Region | Should Match "us-central1"
    }

    It "should get all global addresses of project" {
        $addresses = Get-GceAddress -Global
        ($addresses | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Address }
        $addresses.Count | Should Be 2
        ($addresses.Name -eq $globalAddressName1).Count | Should Be 1
        $addresses.AddressValue | ForEach-Object { $_ | Should Not BeNullOrEmpty }
        $addresses.Region | ForEach-Object { $_ | Should BeNullOrEmpty }
    }

    It "should get global address by name" {
        $address = Get-GceAddress $globalAddressName2 -Global
        ($address | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.Address }
        $address.Count | Should Be 1
        $address.Name | Should Be $globalAddressName2
        $address.AddressValue | Should Not BeNullOrEmpty
        $address.Region | Should BeNullOrEmpty
    }

    Get-GceAddress | Remove-GceAddress
    Get-GceAddress -Global | Remove-GceAddress
}

Describe "Remove-GceAddress" {
    
    It "should fail for wrong project." {
        { Remove-GceAddress "not-exist-address" -Project "asdf" } | Should Throw 403
    }

    It "should fail for non-existant address" {
        { Remove-GceAddress "not-exist-address" } | Should Throw 404
    }
    
    Context "remove region addresses" {
        $addressName = "test-remove-address-$r"

        BeforeEach {
            Add-GceAddress $addressName
        }

        It "should work" {
            Remove-GceAddress $addressName
            { Get-GceAddress $addressName } | Should Throw 404
        }

        It "should work with pipeline" {
            $addressName | Remove-GceAddress
            { Get-GceAddress $addressName } | Should Throw 404
        }

        It "should work with object" {
            $address = Get-GceAddress $addressName
            Remove-GceAddress $address
            { Get-GceAddress $addressName } | Should Throw 404
        }

        It "should work with object pipeline" {
            Get-GceAddress $addressName | Remove-GceAddress
            { Get-GceAddress $addressName } | Should Throw 404
        }
    }

    Context "remove global addresses" {
        $addressName = "test-remove-global-address-$r"

        BeforeEach {
            Add-GceAddress $addressName -Global
        }

        It "should work" {
            Remove-GceAddress $addressName -Global
            { Get-GceAddress $addressName -Global } | Should Throw 404
        }

        It "should work with pipeline" {
            $addressName | Remove-GceAddress -Global
            { Get-GceAddress $addressName -Global } | Should Throw 404
        }

        It "should work with object" {
            $address = Get-GceAddress $addressName -Global
            Remove-GceAddress $address
            { Get-GceAddress $addressName -Global } | Should Throw 404
        }

        It "should work with object pipeline" {
            Get-GceAddress $addressName -Global |
                Remove-GceAddress
            { Get-GceAddress $addressName -Global } | Should Throw 404
        }
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
