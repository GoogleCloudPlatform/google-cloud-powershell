. $PSScriptRoot\..\BigQuery\GcbqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GcbqDataset" {

    It "should list datasets" {
        try {
            New-GcbqDataset "test_id_2"
            New-GcbqDataset "test_id_3"
            $a = Get-GcbqDataset
            $a.Count | Should BeGreaterThan 1
        }
        finally {
            Get-GcbqDataset "test_id_2" |  Remove-GcbqDataset
            Get-GcbqDataset "test_id_3" |  Remove-GcbqDataset
        }
    }

    It "should get a dataset that exists and has permissions" {
        try {
            New-GcbqDataset "test_id_4"
            $a = Get-GcbqDataset "test_id_4"
            $a.DatasetReference.DatasetId | Should Be "test_id_4"
        }
        finally {
            Get-GcbqDataset "test_id_4" |  Remove-GcbqDataset
        }
    }

    It "should list datasets and then get complete dataset objects from the DatasetData" {
        try {
            New-GcbqDataset "test_id_2"
            $a = Get-GcbqDataset | Get-GcbqDataset
            $a[0].GetType() | Should Be "Google.Apis.Bigquery.v2.Data.Dataset"
        }
        finally {
            Get-GcbqDataset "test_id_2" |  Remove-GcbqDataset
        }
    }

    It "should get using a pre-existing Dataset (to refresh data from the cloud)" {
        try {
            New-GcbqDataset "test_id_2"
            $a = Get-GcbqDataset "test_id_2"
            $b = $a | Get-GcbqDataset
            $b.GetType() | Should Be "Google.Apis.Bigquery.v2.Data.Dataset"
        }
        finally {
            Get-GcbqDataset "test_id_2" |  Remove-GcbqDataset
        }
    }

    It "should get using a DatasetReference" {
        try {
            New-GcbqDataset "test_id_2"
            $a = Get-GcbqDataset "test_id_2"
            $b = $a.DatasetReference | Get-GcbqDataset
            $b.GetType() | Should Be "Google.Apis.Bigquery.v2.Data.Dataset"
        }
        finally {
            Get-GcbqDataset "test_id_2" |  Remove-GcbqDataset
        }
    }

    It "should handle when a dataset does not exist" {
        { Get-GcbqDataset "test_id_5" -ErrorAction Stop } | Should Throw 404
    }

    It "should handle projects that do not exist" {
        { Get-GcbqDataset -Project $nonExistProject "test_id_5" -ErrorAction Stop } | Should Throw 404
    }

    It "should handle projects that the user does not have permissions for" {
        { Get-GcbqDataset -Project $accessErrProject "test_id_5" } | Should Throw 400
    }
}

Describe "Set-GcbqDataset" {

    It "should update trivial metadata fields via pipeline. (not DatabaseId)" {
        try {
            New-GcbqDataset "test_dataset_id1" -Name "Testdata" -Description "Some interesting data!" -Expiration $oneDaySec
            $data = Get-GcbqDataset "test_dataset_id1"
            $data.FriendlyName = "Some Test Data"
            $data.Description = "A new description!"
            $data.DefaultTableExpirationMs = $threeDayMs

            $data = $data | Set-GcbqDataset
            $data | Should Not BeNullOrEmpty
            $data.FriendlyName | Should Be "Some Test Data"
            $data.Description | Should Be "A new description!"
            $data.DefaultTableExpirationMs | Should Be $threeDayMs
        }
        finally {
            Get-GcbqDataset "test_dataset_id1" | Remove-GcbqDataset
        }
    }

    It "should update trivial metadata fields via parameter. (not DatabaseId)" {
        try {
            New-GcbqDataset "test_dataset_id2" -Name "Testdata" -Description "Some interesting data!" -Expiration $oneDaySec
            $data = Get-GcbqDataset "test_dataset_id2"
            $data.FriendlyName = "Some Test Data"
            $data.Description = "A new description!"
            $data.DefaultTableExpirationMs = $threeDayMs

            $data = Set-GcbqDataset -InputObject $data
            $data | Should Not BeNullOrEmpty
            $data.FriendlyName | Should Be "Some Test Data"
            $data.Description | Should Be "A new description!"
            $data.DefaultTableExpirationMs | Should Be $threeDayMs
        }
        finally {
            Get-GcbqDataset "test_dataset_id2" | Remove-GcbqDataset
        }
    }

    It "should not overwrite data if a set request is malformed" {
        try {
            New-GcbqDataset "test_dataset_id3" -Name "Testdata" -Description "Some interesting data!" -Expiration $oneDaySec
            $data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
            $data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
            { Set-GcbqDataset -InputObject $data } | Should Throw "is missing"
        }
        finally {
            Get-GcbqDataset "test_dataset_id3" | Remove-GcbqDataset
        }
    } 

    It "should not update a set that does not exist" {
        $data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
        $data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
        $data.DatasetReference.DatasetId = "test_dataset_id4"
        { Set-GcbqDataset -InputObject $data } | Should Throw 404
    } 

    #TODO(ahandley): Find reason behind occasional (25%) 412 Precondition error (If-Match - header).
    
    #TODO(ahandley): Add in tests for updating Access field when cmdlet support has been built.

}

Describe "New-GcbqDataset" {

    It "should take a name, description, and time to make a dataset" {
        try {
            $data = New-GcbqDataset "test_data_id1" -Name "Testdata" -Description "Some interesting data!" -Expiration $oneDaySec
            $data | Should Not BeNullOrEmpty
            $data.DatasetReference.DatasetId | Should Be "test_data_id1"
            $data.FriendlyName | Should Be "Testdata"
            $data.Description | Should Be "Some interesting data!"
            $data.DefaultTableExpirationMs | Should Be $oneDayMs
        }
        finally {
            Get-GcbqDataset "test_data_id1" | Remove-GcbqDataset
        }
    }

    It "should accept a dataset object from pipeline" {
        try {
            $data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
            $data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
            $data.DatasetReference.DatasetId = "test_data_id2"
            $data.DatasetReference.ProjectId = $project
            $data.FriendlyName = "PipeTest"
            $data.Description = "Some cool data coming hot off the pipeline!"
            $newdata = $data | New-GcbqDataset
            $newdata | Should Not BeNullOrEmpty
            $newdata.DatasetReference.DatasetId | Should Be "test_data_id2"
            $newdata.FriendlyName | Should Be "PipeTest"
        }
        finally {
            Get-GcbqDataset "test_data_id2" | Remove-GcbqDataset
        }
    }

    It "should accept a more complex dataset object from pipeline" {
        try {
            $data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
            $data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
            $data.DatasetReference.DatasetId = "test_data_id2"
            $data.DatasetReference.ProjectId = $project
            $data.FriendlyName = "Pipe-Object Test3!"
            $data.Description = "Some cool data from the pipeline!<>?>#$%^!@&''~"
            $newdata = $data | New-GcbqDataset
            $newdata | Should Not BeNullOrEmpty
            $newdata.DatasetReference.DatasetId | Should Be "test_data_id2"
            $newdata.FriendlyName | Should Be "Pipe-Object Test3!"
            $newdata.Description | Should Be "Some cool data from the pipeline!<>?>#$%^!@&''~"
        }
        finally {
            Get-GcbqDataset "test_data_id2" | Remove-GcbqDataset
        }
    }

    It "should accept a dataset object from a command line argument" {
        try {
            $data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
            $data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
            $data.DatasetReference.DatasetId = "test_data_id3"
            $data.DatasetReference.ProjectId = $project
            $data.FriendlyName = "PipeTest"
            $data.Description = "Some cool data coming hot off the pipeline!"
            $newdata = New-GcbqDataset -InputObject $data
            $newdata | Should Not BeNullOrEmpty
            $newdata.DatasetReference.DatasetId | Should Be "test_data_id3"
            $newdata.FriendlyName | Should Be "PipeTest"
        }
        finally {
            Get-GcbqDataset "test_data_id3" | Remove-GcbqDataset
        }
    }

    It "should not work with no arguments. (meaning no datasetId)" {
        { New-GcbqDataset } | Should Throw 400
    }

    It "should reject datasets with malformed ids" {
        { New-GcbqDataset "test-?ata-4" } | Should Throw 400
    }

    It "should reject datasets with empty ids" {
        { New-GcbqDataset "" } | Should Throw "Cannot validate argument on parameter 'DatasetId'"
    }

    It "should reject datasets that do not have unique ids" {
        try {
            $data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
            $data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
            $data.DatasetReference.DatasetId = "test_data_id6"
            $data2 = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
            $data2.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
            $data2.DatasetReference.DatasetId = "test_data_id6"
            $data | New-GcbqDataset
            { $data2 | New-GcbqDataset -ErrorAction Stop} | Should Throw 409
        }
        finally {
            Get-GcbqDataset "test_data_id6" | Remove-GcbqDataset
        }
    }

    It "should use the -Project tag correctly" {
        try {
            $data = New-GcbqDataset -Project $project "test_data_id7" -Name "Testdata" -Description "Some interesting data!"
            $data | Should Not BeNullOrEmpty
            $data.DatasetReference.ProjectId | Should Be $project
        }
        finally {
            Get-GcbqDataset "test_data_id7" | Remove-GcbqDataset
        }
    }

    It "should throw when you try to add to a project that doesnt exist" {
        { New-GcbqDataset -Project $nonExistProject "test_data_id8" `
            -Name "Testdata" -Description "Some interesting data!" } | Should Throw 404
    }

    It "should throw when you try to add to a project that is not yours" {
        { New-GcbqDataset -Project $accessErrProject "test_data_id9" `
            -Name "Testdata" -Description "Some interesting data!" } | Should Throw 400
    }
}

Describe "Remove-GcbqDataset" {

    It "should not delete the dataset if -WhatIf is specified" {
        try{
            $dataset = New-GcbqDataset "test_set_if"
            $dataset | Remove-GcbqDataset -WhatIf
            $remainder = Get-GcbqDataset "test_set_if"
            $remainder.DatasetReference.DatasetId | Should Be "test_set_if"
        }
        finally {
            Get-GcbqDataset "test_set_if" | Remove-GcbqDataset
        }
    }
    
    It "should delete an empty dataset from the pipeline with no -Force" {
        try{
            New-GcbqDataset "test_set_1"
        }
        finally {
            Get-GcbqDataset "test_set_1" | Remove-GcbqDataset
            { Get-GcbqDataset "test_set_1" -ErrorAction Stop } | Should Throw 404
        }
    }

    It "should delete an empty dataset from an argument with no -Force" {
        try {
            New-GcbqDataset "test_set_2"
            $a = Get-GcbqDataset "test_set_2" 
        }
        finally {
            Remove-GcbqDataset -InputObject $a
            { Get-GcbqDataset "test_set_2" -ErrorAction Stop } | Should Throw 404
        }
    }

    It "should delete a dataset by value with explicit project" {
        try {
            New-GcbqDataset -Project $project "test_set_explicit"
        }
        finally {
            Remove-GcbqDataset -Project $project -Dataset "test_set_explicit"
            { Get-GcbqDataset "test_set_explicit" -ErrorAction Stop } | Should Throw 404
        }
    }

    It "should delete a nonempty dataset as long as -Force is specified" {
        try {
            New-GcbqDataset -Project $project "test_set_force"
            New-GcbqTable -Project $project -DatasetId "test_set_force" -Table "force_test_table"
        }
        finally {
            Remove-GcbqDataset -Project $project -Dataset "test_set_force" -Force
            { Get-GcbqDataset "test_set_force" -ErrorAction Stop } | Should Throw 404
        }
    }

    It "should handle when a dataset does not exist" {
        $data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
        $data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
        $data.DatasetReference.DatasetId = "test_set_not_on_server"
        $data.DatasetReference.ProjectId = $project
        { Remove-GcbqDataset -InputObject $data } | Should Throw 404
    }

    It "should handle projects that do not exist" {
        { Remove-GcbqDataset -Project $nonExistProject -Dataset $nonExistDataset } | Should Throw 404
    }

    It "should handle project:dataset combinations that do not exist" {
        { Remove-GcbqDataset -Project $project -Dataset $nonExistDataset } | Should Throw 404
    }

    It "should handle projects that the user does not have permissions for" {
        { Remove-GcbqDataset -Project $accessErrProject -Dataset $nonExistDataset } | Should Throw 400
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
