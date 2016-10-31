. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

# Helper function to compare ACLs.
function CompareAcl($firstAcl, $secondAcl) {
    return ($firstAcl.Bucket -eq $secondAcl.Bucket) -and `
           ($firstAcl.Id -eq $secondAcl.Id) -and `
           ($firstAcl.Role -eq $secondAcl.Role) -and `
           ($firstAcl.Entity -eq $secondAcl.Entity) -and `
           ($firstAcl.SelfLink -eq $secondAcl.SelfLink)
           ($firstAcl.Kind -eq $secondAcl.Kind)
}

Describe "Get-GcsBucketAcl" {
    $r = Get-Random
    $script:bucketName = "gcps-testing-acl-bucket-$r"
    $bucketAclKind = "storage#bucketAccessControl"
    New-GcsBucket -Name $bucketName

    AfterAll {
        Remove-GcsBucket -Name $script:bucketName -Force
    }

    It "should fail to return for non-existent buckets" {
        { Get-GcsBucketAcl -Name "gcps-bucket-no-exist" } | Should Throw "404"
    }

    It "should work" {
        $bucketAcls = Get-GcsBucketAcl -Name $bucketName
        # By default, a bucket should have project-owners-ID, project-editors-ID, project-viewers-ID ACL.
        $bucketAcls.Count -ge 3 | Should Be $true
        $projectOwnerAcl = $bucketAcls | Where-Object {$_.Entity -like "project-owners*"} | Select -First 1

        $projectOwnerAcl | Should Not BeNullOrEmpty
        $projectOwnerAcl.Entity | Should Match "project-owners"
        $projectOwnerAcl.Kind | Should BeExactly $bucketAclKind
        $projectOwnerAcl.Role | Should BeExactly "OWNER"

        $projectEditorAcl = $bucketAcls | Where-Object {$_.Entity -like "project-editors*"} | Select -First 1
        $projectEditorAcl | Should Not BeNullOrEmpty
        $projectEditorAcl.Entity | Should Match "project-editors"
        $projectEditorAcl.Kind | Should BeExactly $bucketAclKind
        $projectEditorAcl.Role | Should BeExactly "OWNER"

        $projectViewerAcl = $bucketAcls | Where-Object {$_.Entity -like "project-viewers*"} | Select -First 1
        $projectViewerAcl | Should Not BeNullOrEmpty
        $projectViewerAcl.Entity | Should Match "project-viewers"
        $projectViewerAcl.Kind | Should BeExactly $bucketAclKind
        $projectViewerAcl.Role | Should BeExactly "READER"
    }

    It "should give access errors as appropriate" {
        { Get-GcsBucketAcl -Name "asdf" } | Should Throw "403"
    }
}

Describe "Add-GcsBucketAcl" {
    $r = Get-Random
    $script:bucketName = "gcps-testing-acl-bucket-$r"
    $userEmail = "quoct@google.com"
    $groupEmail = "test-group-for-google-cloud-powershell@google.com"
    $domain = "example.com"
    New-GcsBucket -Name $bucketName

    AfterAll {
        Remove-GcsBucket -Name $script:bucketName -Force
    }

    It "should throw error for non-existent bucket" {
        { Add-GcsBucketAcl -Name "gcps-bucket-no-exist" -Role Reader -AllUsers } | Should Throw "404"
    }

    It "should work for -AllUsers" {
        $addedAcl = Add-GcsBucketAcl -Name $bucketName -Role Reader -AllUsers
        $acl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "allUsers"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -like "allUsers"}) | Should BeNullOrEmpty
    }

    It "should work for -AllAuthenticatedUser" {
        $addedAcl = Add-GcsBucketAcl -Name $bucketName -Role Reader -AllAuthenticatedUsers
        $acl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "allAuthenticatedUsers"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true
        # Test that the newly created object don't have the ACL we just add.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -like "allAuthenticatedUsers"}) | Should BeNullOrEmpty
    }

    It "should throw error for wrong user" {
        { Add-GcsBucketAcl -Name $bucketName -Role Reader -User blahblahblah@example.com } |
            Should Throw "Unknown user email address"
    }

    It "should work for valid user" {
        $addedAcl = Add-GcsBucketAcl -Name $bucketName -Role Owner -User $userEmail
        $acl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "user-$userEmail"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true
        # Test that the newly created object don't have the ACL we just add.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -like "$userEmail"}) | Should BeNullOrEmpty
    }

    It "should throw error for wrong group" {
        { Add-GcsBucketAcl -Name $bucketName -Role Reader -Group $userEmail } |
            Should Throw "Could not find group"
    }

    It "should work for valid group" {
        $addedAcl = Add-GcsBucketAcl -Name $bucketName -Role Owner -Group $groupEmail
        $acl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "group-$groupEmail"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true
        # Test that the newly created object don't have the ACL we just add.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -like "$groupEmail"}) | Should BeNullOrEmpty
    }

    It "should throw error for wrong domain" {
        { Add-GcsBucketAcl -Name $bucketName -Role Reader -Domain example.thisdomaincannottrulyexist } |
            Should Throw "Could not find domain"
    }

    It "should work for valid domain" {
        $addedAcl = Add-GcsBucketAcl -Name $bucketName -Role Owner -Domain $domain
        $acl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "domain-$domain"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true
        # Test that the newly created object don't have the ACL we just add.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -like $domain}) | Should BeNullOrEmpty
    }
}

Describe "Remove-GcsBucketAcl" {
    $r = Get-Random
    $script:bucketName = "gcps-testing-acl-bucket-$r"
    $userEmail = "quoct@google.com"
    $groupEmail = "test-group-for-google-cloud-powershell@google.com"
    $domain = "example.com"
    New-GcsBucket -Name $bucketName

    AfterAll {
        Remove-GcsBucket -Name $script:bucketName -Force
    }

    It "should throw error for non-existent bucket" {
        { Remove-GcsBucketAcl -Name "gcps-bucket-no-exist" -AllUsers } | Should Throw "404"
    }

    It "should throw error for non-existent ACL" {
        { Remove-GcsBucketAcl -Name $bucketName -Domain google.com } | Should Throw "404"
    }

    It "should work for -AllUsers" {
        $addedAcl = Add-GcsBucketAcl -Name $bucketName -Role Reader -AllUsers
        (Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "allUsers"}) | Should Not BeNullOrEmpty
        Remove-GcsBucketAcl -Name $bucketName -AllUsers
        (Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "allUsers"}) | Should BeNullOrEmpty
    }

    It "should work for -AllAuthenticatedUser" {
        Add-GcsBucketAcl -Name $bucketName -Role Reader -AllAuthenticatedUsers
        (Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "allAuthenticatedUsers"}) | Should Not BeNullOrEmpty
        Remove-GcsBucketAcl -Name $bucketName -AllAuthenticatedUsers
        (Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "allAuthenticatedUsers"}) | Should BeNullOrEmpty
    }

    It "should work for user" {
        Add-GcsBucketAcl -Name $bucketName -Role Owner -User $userEmail
        (Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "user-$userEmail"}) | Should Not BeNullOrEmpty
        Remove-GcsBucketAcl -Name $bucketName -User $userEmail
        (Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "user-$userEmail"}) | Should BeNullOrEmpty
    }

    It "should work for group" {
        Add-GcsBucketAcl -Name $bucketName -Role Owner -Group $groupEmail
        (Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "group-$groupEmail"}) | Should Not BeNullOrEmpty
        Remove-GcsBucketAcl -Name $bucketName -Group $groupEmail
        (Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "group-$groupEmail"}) | Should BeNullOrEmpty
    }

    It "should work for domain" {
        Add-GcsBucketAcl -Name $bucketName -Role Owner -Domain $domain
        (Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "domain-$domain"}) | Should Not BeNullOrEmpty
        Remove-GcsBucketAcl -Name $bucketName -Domain $domain
        (Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "domain-$domain"}) | Should BeNullOrEmpty
    }
}

Describe "Get-GcsObjectAcl" {
    $r = Get-Random
    $script:bucketName = "gcps-testing-acl-bucket-$r"
    $objectName = "testing-object-$r"
    $objectAclKind = "storage#objectAccessControl"
    New-GcsBucket -Name $bucketName
    New-GcsObject -Bucket $bucketName -ObjectName $objectName -Value "Not Important."

    AfterAll {
        Remove-GcsBucket -Name $script:bucketName -Force
    }

    It "should fail to return for non-existent buckets" {
        { Get-GcsObjectAcl -Bucket $bucketName -ObjectName "gcps-bucket-no-exist" } | Should Throw "404"
    }

    It "should work" {
        $objectAcls = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName 
        # By default, the object should have at least project-owners-ID, project-editors-ID, project-viewers-ID ACL.
        $objectAcls.Count -ge 3 | Should Be $true
        $projectOwnerAcl = $objectAcls | Where-Object {$_.Entity -like "project-owners*"} | Select -First 1

        $projectOwnerAcl | Should Not BeNullOrEmpty
        $projectOwnerAcl.Entity | Should Match "project-owners"
        $projectOwnerAcl.Kind | Should BeExactly $objectAclKind
        $projectOwnerAcl.Role | Should BeExactly "OWNER"

        $projectEditorAcl = $objectAcls | Where-Object {$_.Entity -like "project-editors*"} | Select -First 1
        $projectEditorAcl | Should Not BeNullOrEmpty
        $projectEditorAcl.Entity | Should Match "project-editors"
        $projectEditorAcl.Kind | Should BeExactly $objectAclKind
        $projectEditorAcl.Role | Should BeExactly "OWNER"

        $projectViewerAcl = $objectAcls | Where-Object {$_.Entity -like "project-viewers*"} | Select -First 1
        $projectViewerAcl | Should Not BeNullOrEmpty
        $projectViewerAcl.Entity | Should Match "project-viewers"
        $projectViewerAcl.Kind | Should BeExactly $objectAclKind
        $projectViewerAcl.Role | Should BeExactly "READER"
    }

    It "should give access errors as appropriate" {
        { Get-GcsBucket -Bucket "asdf" -ObjectName "unimportant" } | Should Throw "403"
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
