. $PSScriptRoot\..\BigQuery\BqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-BqDataset" {

    It "should list datasets" {
        try {
            New-BqDataset "test_id_2"
            New-BqDataset "test_id_3"
            $a = Get-BqDataset
            $a.Count | Should BeGreaterThan 1
        } finally {
            Get-BqDataset "test_id_2" |  Remove-BqDataset
            Get-BqDataset "test_id_3" |  Remove-BqDataset
        }
    }

    It "should list by label" {
        try {
            $data1 = New-BqDataset "test_label_1"
            $data2 = New-BqDataset "test_label_2"
            $data2 = $data2 | Set-BqDataset -SetLabel @{"pstestlabel"="one"}
            $data3 = New-BqDataset "test_label_3"
            $data3 = $data3 | Set-BqDataset -SetLabel @{"pstestlabel"="three";"psaltlabel"="two"}
            
            $ds = Get-BqDataset -Filter "pstestlabel"
            $ds.Count | Should Be 2
            $ds[0].DatasetReference.DatasetId | Should Not Be "test_label_1"
            $ds[1].DatasetReference.DatasetId | Should Not Be "test_label_1"

            $ds = Get-BqDataset -Filter "pstestlabel:one"
            $ds.Count | Should Be 1
            $ds[0].DatasetReference.DatasetId | Should Be "test_label_2"

            $ds = Get-BqDataset -Filter "psaltlabel:two"
            $ds.Count | Should Be 1
            $ds[0].DatasetReference.DatasetId | Should Be "test_label_3"
        } finally {
            Get-BqDataset "test_label_1" | Remove-BqDataset
            Get-BqDataset "test_label_2" | Remove-BqDataset
            Get-BqDataset "test_label_3" | Remove-BqDataset
        }
    }

    It "should get a dataset that exists and has permissions" {
        try {
            New-BqDataset "test_id_4"
            $a = Get-BqDataset "test_id_4"
            $a.DatasetReference.DatasetId | Should Be "test_id_4"
        } finally {
            Get-BqDataset "test_id_4" |  Remove-BqDataset
        }
    }

    It "should list datasets and then get complete dataset objects from the DatasetData" {
        try {
            New-BqDataset "test_id_2"
            $a = Get-BqDataset | Get-BqDataset
            $a[0].GetType() | Should Be "Google.Apis.Bigquery.v2.Data.Dataset"
        } finally {
            Get-BqDataset "test_id_2" |  Remove-BqDataset
        }
    }

    It "should get using a pre-existing Dataset (to refresh data from the cloud)" {
        try {
            New-BqDataset "test_id_2"
            $a = Get-BqDataset "test_id_2"
            $b = $a | Get-BqDataset
            $b.GetType() | Should Be "Google.Apis.Bigquery.v2.Data.Dataset"
        } finally {
            Get-BqDataset "test_id_2" |  Remove-BqDataset
        }
    }

    It "should get using a DatasetReference" {
        try {
            New-BqDataset "test_id_2"
            $a = Get-BqDataset "test_id_2"
            $b = $a.DatasetReference | Get-BqDataset
            $b.GetType() | Should Be "Google.Apis.Bigquery.v2.Data.Dataset"
        } finally {
            Get-BqDataset "test_id_2" |  Remove-BqDataset
        }
    }

    It "should handle when a dataset does not exist" {
        { Get-BqDataset "test_id_5" } | Should Throw 404
    }

    It "should handle projects that do not exist" {
        { Get-BqDataset "test_id_5" -Project $nonExistProject} | Should Throw 404
    }

    It "should handle projects that the user does not have permissions for" {
        { Get-BqDataset "test_id_5" -Project $accessErrProject } | Should Throw 400
    }
}

Describe "Set-BqDataset" {

    It "should update trivial metadata fields via pipeline. (not DatabaseId)" {
        try {
            New-BqDataset "test_dataset_id1" -Name "Testdata" -Description "Some interesting data!" -Expiration $oneDaySec
            $data = Get-BqDataset "test_dataset_id1"
            $data.FriendlyName = "Some Test Data"
            $data.Description = "A new description!"
            $data.DefaultTableExpirationMs = $threeDayMs

            $data = $data | Set-BqDataset
            $data | Should Not BeNullOrEmpty
            $data.FriendlyName | Should Be "Some Test Data"
            $data.Description | Should Be "A new description!"
            $data.DefaultTableExpirationMs | Should Be $threeDayMs
        } finally {
            Get-BqDataset "test_dataset_id1" | Remove-BqDataset
        }
    }

    It "should update trivial metadata fields via parameter. (not DatabaseId)" {
        try {
            New-BqDataset "test_dataset_id2" -Name "Testdata" -Description "Some interesting data!" -Expiration $oneDaySec
            $data = Get-BqDataset "test_dataset_id2"
            $data.FriendlyName = "Some Test Data"
            $data.Description = "A new description!"
            $data.DefaultTableExpirationMs = $threeDayMs

            $data = Set-BqDataset $data
            $data | Should Not BeNullOrEmpty
            $data.FriendlyName | Should Be "Some Test Data"
            $data.Description | Should Be "A new description!"
            $data.DefaultTableExpirationMs | Should Be $threeDayMs
        } finally {
            Get-BqDataset "test_dataset_id2" | Remove-BqDataset
        }
    }

    It "should not overwrite data if a set request is malformed" {
        try {
            New-BqDataset "test_dataset_id3" -Name "Testdata" -Description "Some interesting data!" -Expiration $oneDaySec
            $data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
            $data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
            { Set-BqDataset $data } | Should Throw "is missing"
        } finally {
            Get-BqDataset "test_dataset_id3" | Remove-BqDataset
        }
    } 

    It "should add when a dataset does not exist" {
        try {
            $data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
            $data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
            $data.DatasetReference.DatasetId = "test_dataset_id7"
            $data.DatasetReference.ProjectId = $project
            $set = Set-BqDataset $data
            $set.DatasetReference.DatasetId | Should Be "test_dataset_id7"
        } finally {
            Get-BqDataset "test_dataset_id7" | Remove-BqDataset
        }
    } 
    
    It "should label things properly" {
        try {
            $data = New-BqDataset "test_label_1"
            $data = $data | Set-BqDataset -SetLabel @{"test"="one"}
            $data.Labels.Count | Should Be 1
            $data.Labels["test"] | Should Be "one"
        } finally {
            Get-BqDataset "test_label_1" | Remove-BqDataset
        }
    }

    It "should handle a lot of labels at once" {
        try {
            $data = New-BqDataset "test_label_2"
            $data = $data | Set-BqDataset -SetLabel @{"test"="one";"other"="two";"third"="three"}
            $data.Labels.Count | Should Be 3
            $data.Labels["test"] | Should Be "one"
            $data.Labels["other"] | Should Be "two"
            $data.Labels["third"] | Should Be "three"
        } finally {
            Get-BqDataset "test_label_2" | Remove-BqDataset
        }
    }

    It "should clear just one label" {
        try {
            $data = New-BqDataset "test_label_3"
            $data = $data | Set-BqDataset -SetLabel @{"test"="one";"other"="two"}
            $data = $data | Set-BqDataset -ClearLabel "test"
            $data.Labels.Count | Should Be 1
            $data.Labels["other"] | Should Be "two"
        } finally {
            Get-BqDataset "test_label_3" | Remove-BqDataset
        }
    }

    It "should clear multiple labels + nonexistant labels" {
        try {
            $data = New-BqDataset "test_label_4"
            $data = $data | Set-BqDataset -SetLabel @{"test"="one";"other"="two"}
            $data = $data | Set-BqDataset -ClearLabel "test","other","doesnotexist"
            $data.Labels | Should Be $null
        } finally {
            Get-BqDataset "test_label_4" | Remove-BqDataset
        }
    }
}

Describe "New-BqDataset" {

    It "should take a name, description, and time to make a dataset" {
        try {
            $data = New-BqDataset "test_data_id1" -Name "Testdata" -Description "Some interesting data!" -Expiration $oneDaySec
            $data | Should Not BeNullOrEmpty
            $data.DatasetReference.DatasetId | Should Be "test_data_id1"
            $data.FriendlyName | Should Be "Testdata"
            $data.Description | Should Be "Some interesting data!"
            $data.DefaultTableExpirationMs | Should Be $oneDayMs
        } finally {
            Get-BqDataset "test_data_id1" | Remove-BqDataset
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
            $newdata = $data | New-BqDataset
            $newdata | Should Not BeNullOrEmpty
            $newdata.DatasetReference.DatasetId | Should Be "test_data_id2"
            $newdata.FriendlyName | Should Be "PipeTest"
        } finally {
            Get-BqDataset "test_data_id2" | Remove-BqDataset
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
            $newdata = $data | New-BqDataset
            $newdata | Should Not BeNullOrEmpty
            $newdata.DatasetReference.DatasetId | Should Be "test_data_id2"
            $newdata.FriendlyName | Should Be "Pipe-Object Test3!"
            $newdata.Description | Should Be "Some cool data from the pipeline!<>?>#$%^!@&''~"
        } finally {
            Get-BqDataset "test_data_id2" | Remove-BqDataset
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
            $newdata = New-BqDataset -Dataset $data
            $newdata | Should Not BeNullOrEmpty
            $newdata.DatasetReference.DatasetId | Should Be "test_data_id3"
            $newdata.FriendlyName | Should Be "PipeTest"
        } finally {
            Get-BqDataset "test_data_id3" | Remove-BqDataset
        }
    }

    It "should reject datasets with malformed ids" {
        { New-BqDataset "test-?ata-4" } | Should Throw 400
    }

    It "should reject datasets with empty ids" {
        { New-BqDataset "" } | Should Throw "Cannot validate argument on parameter 'DatasetId'"
    }

    It "should reject datasets that do not have unique ids" {
        try {
            $data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
            $data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
            $data.DatasetReference.DatasetId = "test_data_id6"
            $data.DatasetReference.ProjectId = $project
            $data2 = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
            $data2.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
            $data2.DatasetReference.DatasetId = "test_data_id6"
            $data2.DatasetReference.ProjectId = $project
            $data | New-BqDataset
            { $data2 | New-BqDataset } | Should Throw 409
        } finally {
            Get-BqDataset "test_data_id6" | Remove-BqDataset
        }
    }

    It "should use the -Project tag correctly" {
        try {
            $data = New-BqDataset -Project $project "test_data_id7" -Name "Testdata" -Description "Some interesting data!"
            $data | Should Not BeNullOrEmpty
            $data.DatasetReference.ProjectId | Should Be $project
        } finally {
            Get-BqDataset "test_data_id7" | Remove-BqDataset
        }
    }

    It "should throw when you try to add to a project that doesnt exist" {
        { New-BqDataset "test_data_id8" -Project $nonExistProject `
            -Name "Testdata" -Description "Some interesting data!" } | Should Throw 404
    }

    It "should throw when you try to add to a project that is not yours" {
        { New-BqDataset "test_data_id9" -Project $accessErrProject `
            -Name "Testdata" -Description "Some interesting data!" } | Should Throw 400
    }
}

Describe "Remove-BqDataset" {

    It "should not delete the dataset if -WhatIf is specified" {
        try{
            $dataset = New-BqDataset "test_set_if"
            $dataset | Remove-BqDataset -WhatIf
            $remainder = Get-BqDataset "test_set_if"
            $remainder.DatasetReference.DatasetId | Should Be "test_set_if"
        } finally {
            Get-BqDataset "test_set_if" | Remove-BqDataset
        }
    }
    
    It "should delete an empty dataset from the pipeline with no -Force" {
        try{
            New-BqDataset "test_set_1"
        } finally {
            Get-BqDataset "test_set_1" | Remove-BqDataset
            { Get-BqDataset "test_set_1" } | Should Throw 404
        }
    }

    It "should delete an empty dataset from an argument with no -Force" {
        try {
            New-BqDataset "test_set_2"
            $a = Get-BqDataset "test_set_2" 
        } finally {
            Remove-BqDataset $a
            { Get-BqDataset "test_set_2" } | Should Throw 404
        }
    }

    It "should delete a dataset by value with explicit project" {
        try {
            New-BqDataset "test_set_explicit" -Project $project 
        } finally {
            Remove-BqDataset "test_set_explicit" -Project $project
            { Get-BqDataset "test_set_explicit" } | Should Throw 404
        }
    }

    It "should delete a nonempty dataset as long as -Force is specified" {
        try {
            New-BqDataset "test_set_force"
            New-BqTable "force_test_table" -DatasetId "test_set_force"
        } finally {
            Remove-BqDataset "test_set_force" -Force
            { Get-BqDataset "test_set_force" } | Should Throw 404
        }
    }

    It "should handle when a dataset does not exist" {
        $data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
        $data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
        $data.DatasetReference.DatasetId = "test_set_not_on_server"
        $data.DatasetReference.ProjectId = $project
        { Remove-BqDataset $data } | Should Throw 404
    }

    It "should handle projects that do not exist" {
        { Remove-BqDataset $nonExistDataset -Project $nonExistProject } | Should Throw 404
    }

    It "should handle project:dataset combinations that do not exist" {
        { Remove-BqDataset $nonExistDataset -Project $project } | Should Throw 404
    }

    It "should handle projects that the user does not have permissions for" {
        { Remove-BqDataset $nonExistDataset -Project $accessErrProject } | Should Throw 400
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
