. $PSScriptRoot\..\BigQuery\GcbqCmdlets.ps1
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "New-GcbqDataset" {

	It "should take a name, description, and time to make a dataset" {
		$data = New-GcbqDataset "test_data_id1" -Name "Testdata" -Description "Some interesting data!" -Timeout 86400000
		$data | Should Not BeNullOrEmpty
		$data.DatasetReference.DatasetId | Should Be "test_data_id1"
		$data.FriendlyName | Should Be "Testdata"
		$data.Description | Should Be "Some interesting data!"
		$data.DefaultTableExpirationMs | Should Be 86400000
		Get-GcbqDataset "test_data_id1" | Remove-GcbqDataset
	}

	It "should accept a dataset object from pipeline" {
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
		Get-GcbqDataset "test_data_id2" | Remove-GcbqDataset
	}

	It "should accept a dataset object from a command line argument" {
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
		Get-GcbqDataset "test_data_id3" | Remove-GcbqDataset
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
		$data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
		$data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
		$data.DatasetReference.DatasetId = "test_data_id6"
		$data2 = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
		$data2.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
		$data2.DatasetReference.DatasetId = "test_data_id6"
		$data | New-GcbqDataset
		{ $data2 | New-GcbqDataset } | Should Throw 409
		Get-GcbqDataset "test_data_id6" | Remove-GcbqDataset
	}

	It "should use the -Project tag correctly" {
		$data = New-GcbqDataset -Project $project "test_data_id7" -Name "Testdata" -Description "Some interesting data!"
		$data | Should Not BeNullOrEmpty
		$data.DatasetReference.ProjectId | Should Be $project
		Get-GcbqDataset "test_data_id7" | Remove-GcbqDataset
	}

	It "should throw when you try to add to a project that doesnt exist" {
		{ New-GcbqDataset -Project $nonExistProject "test_data_id8" -Name "Testdata" -Description "Some interesting data!" } | Should Throw 404
    }

	It "should throw when you try to add to a project that is not yours" {
		{ New-GcbqDataset -Project $accessErrProject "test_data_id9" -Name "Testdata" -Description "Some interesting data!" } | Should Throw 400
	}
}

Describe "Get-GcbqDataset" {

	It "should list zero datasets" {
		$a = Get-GcbqDataset
		$a.Datasets | Should BeNullOrEmpty
	}
	
	It "should list one dataset" {
		New-GcbqDataset "test_id_1"
		$a = Get-GcbqDataset
		$a.Datasets.Count | Should Be 1
		Get-GcbqDataset "test_id_1" |  Remove-GcbqDataset
	}

	It "should list two datasets" {
		New-GcbqDataset "test_id_2"
		New-GcbqDataset "test_id_3"
		$a = Get-GcbqDataset
		$a.Datasets.Count | Should Be 2
		Get-GcbqDataset "test_id_2" |  Remove-GcbqDataset
		Get-GcbqDataset "test_id_3" |  Remove-GcbqDataset
	}

	It "should get a dataset that exists and has permissions" {
		New-GcbqDataset "test_id_4"
		$a = Get-GcbqDataset "test_id_4"
		$a.DatasetReference.DatasetId | Should Be "test_id_4"
		Get-GcbqDataset "test_id_4" |  Remove-GcbqDataset
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
		New-GcbqDataset "test_set_1"
		Get-GcbqDataset "test_set_1" | Remove-GcbqDataset
	}

	It "should delete an empty dataset from an argument with no -Force" {
		New-GcbqDataset "test_set_2"
		$a = Get-GcbqDataset "test_set_2" 
		Remove-GcbqDataset -ByObject $a
	}

	#TODO (ahandley) add in test that check the -Force switch when New-GcbqTable is written

	It "should handle when a dataset does not exist" {
		$data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
		$data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
		$data.DatasetReference.DatasetId = "test_set_3"
		$data.DatasetReference.ProjectId = $project
		{ Remove-GcbqDataset -ByObject $data } | Should Throw 404
	}

	It "should handle projects that do not exist" {
		$data = New-GcbqDataset "test_set_4"
		{ Get-GcbqDataset "test_set_4" | Remove-GcbqDataset -Project $nonExistProject } | Should Throw 404
		Remove-GcbqDataset -ByObject $data
	}

	It "should handle projects that the user does not have permissions for" {
		$data = New-GcbqDataset "test_set_5"
		{ Get-GcbqDataset "test_set_5" | Remove-GcbqDataset -Project $accessErrProject } | Should Throw 400
		Remove-GcbqDataset -ByObject $data
	}

	#TODO (ahandley) add in logic at the end of each DESCRIBE block to kill all remaining datasets - use get-* filter
}

Describe "Set-GcbqDataset" {

}

Reset-GCloudConfig $oldActiveConfig $configName
