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
    }

	It "should not work with no arguments (meaning no datasetId)" {
		$data = New-GcbqDataset | Should Throw
    }

	It "should reject datasets that do not have unique ids" {
		$data = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
		$data.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
        $data.DatasetReference.DatasetId = "test_data_id6"
		$data2 = New-Object -TypeName Google.Apis.Bigquery.v2.Data.Dataset
		$data2.DatasetReference = New-Object -TypeName Google.Apis.Bigquery.v2.Data.DatasetReference
        $data2.DatasetReference.DatasetId = "test_data_id6"
		$data | New-GcbqDataset
		{ $data2 | New-GcbqDataset } | Should Throw
    }

	It "should use the -Project tag correctly" {
		$data = New-GcbqDataset -Project $project "test_data_id7" -Name "Testdata" -Description "Some interesting data!"
		$data | Should Not BeNullOrEmpty
		$data.DatasetReference.ProjectId | Should Be $project
    }

	It "should throw when you try to add to a project that doesnt exist" {
		{ New-GcbqDataset -Project $nonExistProject "test_data_id8" -Name "Testdata" -Description "Some interesting data!" } | Should Throw
    }

	It "should throw when you try to add to a project that is not yours" {
		{ New-GcbqDataset -Project $accessErrProject "test_data_id9" -Name "Testdata" -Description "Some interesting data!" } | Should Throw
    }

	#add afterall block that deletes test datasets when remove-x command is done
}

Describe "Get-GcbqDataset" {

}

Describe "Set-GcbqDataset" {

}

Describe "Remove-GcbqDataset" {

}

Reset-GCloudConfig $oldActiveConfig $configName
