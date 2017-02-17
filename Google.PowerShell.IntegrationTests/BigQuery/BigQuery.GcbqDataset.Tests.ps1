. $PSScriptRoot\..\BigQuery\GcbqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

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
            $data | New-GcbqDataset -outvariable newdata
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
            $data | New-GcbqDataset -outvariable newdata
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
            $newdata = New-GcbqDataset -Dataset $data
            $newdata | Should Not BeNullOrEmpty
            $newdata.DatasetReference.DatasetId | Should Be "test_data_id3"
            $newdata.FriendlyName | Should Be "PipeTest"
        }
        finally {
            Get-GcbqDataset "test_data_id3" | Remove-GcbqDataset
        }
    }

    It "should not work with no arguments (meaning no datasetId)" {
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
            { $data2 | New-GcbqDataset } | Should Throw 409
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

Describe "Get-GcbqDataset" {

    It "should list zero datasets" {
        $a = Get-GcbqDataset
        $a.Datasets | Should BeNullOrEmpty
    }
    
    It "should list one dataset" {
        try {
            New-GcbqDataset "test_id_1"
            $a = Get-GcbqDataset
            $a.Datasets.Count | Should Be 1
        }
        finally {
            Get-GcbqDataset "test_id_1" |  Remove-GcbqDataset
        }
    }

    It "should list two datasets" {
        try {
            New-GcbqDataset "test_id_2"
            New-GcbqDataset "test_id_3"
            $a = Get-GcbqDataset
            $a.Datasets.Count | Should Be 2
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

    It "should handle when a dataset does not exist" {
        { Get-GcbqDataset "test_id_5" } | Should Throw
    }

    It "should handle projects that do not exist" {
        { Get-GcbqDataset -Project $nonExistProject "test_id_5" } | Should Throw 404
    }

    It "should handle projects that the user does not have permissions for" {
        { Get-GcbqDataset -Project $accessErrProject "test_id_5" } | Should Throw 400
    }
}

Describe "Remove-GcbqDataset" {

    It "should delete an empty dataset from the pipeline with no -Force" {
        try{
            New-GcbqDataset "test_set_1"
        }
        finally {
            Get-GcbqDataset "test_set_1" | Remove-GcbqDataset
        }
    }

    It "should delete an empty dataset from an argument with no -Force" {
        try {
            New-GcbqDataset "test_set_2"
            $a = Get-GcbqDataset "test_set_2" 
        }
        finally {
            Remove-GcbqDataset -ByObject $a
        }
    }

    #TODO(ahandley): Add in tests that check the -Force switch when New-GcbqTable is written.

    It "should handle when a dataset does not exist" {
        try {
            $data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
            $data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
            $data.DatasetReference.DatasetId = "test_set_3"
            $data.DatasetReference.ProjectId = $project
        }
        finally {
            { Remove-GcbqDataset -ByObject $data } | Should Throw 404
        }
    }

    It "should handle projects that do not exist" {
        try {
            $data = New-GcbqDataset "test_set_4"
            { Get-GcbqDataset "test_set_4" | Remove-GcbqDataset `
                -Project $nonExistProject } | Should Throw 404
        }
        finally {
            Remove-GcbqDataset -ByObject $data
        }
    }

    It "should handle projects that the user does not have permissions for" {
        try {
            $data = New-GcbqDataset "test_set_5"
            { Get-GcbqDataset "test_set_5" | Remove-GcbqDataset `
                -Project $accessErrProject } | Should Throw 400
        }
        finally {
            Remove-GcbqDataset -ByObject $data
        }
    }
}

Describe "Set-GcbqDataset" {

    It "should update trivial metadata fields via pipeline (not DatabaseId)" {
        try {
            New-GcbqDataset "test_dataset_id1" -Name "Testdata" -Description "Some interesting data!" -Expiration $oneDaySec
            $data = Get-GcbqDataset "test_dataset_id1"
            $data.FriendlyName = "Some Test Data"
            $data.Description = "A new description!"
            $data.DefaultTableExpirationMs = $threeDayMs
            $data | Set-GcbqDataset -outvariable data
            $data | Should Not BeNullOrEmpty
            $data.FriendlyName | Should Be "Some Test Data"
            $data.Description | Should Be "A new description!"
            $data.DefaultTableExpirationMs | Should Be $threeDayMs
        }
        finally {
            Get-GcbqDataset "test_dataset_id1" | Remove-GcbqDataset
        }
    }

    It "should update trivial metadata fields via parameter (not DatabaseId)" {
        try {
            New-GcbqDataset "test_dataset_id1" -Name "Testdata" -Description "Some interesting data!" -Expiration $oneDaySec
            $data = Get-GcbqDataset "test_dataset_id1"
            $data.FriendlyName = "Some Test Data"
            $data.Description = "A new description!"
            $data.DefaultTableExpirationMs = $threeDayMs
            $data = Set-GcbqDataset -ByObject $data
            $data | Should Not BeNullOrEmpty
            $data.FriendlyName | Should Be "Some Test Data"
            $data.Description | Should Be "A new description!"
            $data.DefaultTableExpirationMs | Should Be $threeDayMs
        }
        finally {
            Get-GcbqDataset "test_dataset_id1" | Remove-GcbqDataset
        }
    }

    #TODO(ahandley): Add in tests for updating Access field when cmdlet support has been built.

}

Reset-GCloudConfig $oldActiveConfig $configName
