. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets
$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

$userEmail = "powershelltesting@gmail.com"
$groupEmail = "test-group-for-google-cloud-powershell@google.com"
$domain = "example.com"
# Time to sleep before a bucket is created. This is to ensure we don't
# exceed the quota (which is set per second).
# Documentation on the error can be found at https://cloud.google.com/storage/docs/json_api/v1/status-codes
# under usageLimits.userRateLimitExceeded. More information about the quota can be found at
# https://cloud.google.com/appengine/docs/quotas#Safety_Quotas.
$sleepTime = 2

# Helper function to compare ACLs.
function CompareAcl($firstAcl, $secondAcl) {
    return ($firstAcl.Bucket -eq $secondAcl.Bucket) -and `
           ($firstAcl.Role -eq $secondAcl.Role) -and `
           ($firstAcl.Entity -eq $secondAcl.Entity) -and `
           ($firstAcl.SelfLink -eq $secondAcl.SelfLink)
           ($firstAcl.Kind -eq $secondAcl.Kind)
}

Describe "Get-GcsBucketAcl" {
    $r = Get-Random
    $script:bucketName = "gcps-testing-acl-bucket-$r"
    $bucketAclKind = "storage#bucketAccessControl"
    Start-Sleep -Seconds $sleepTime
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
    Start-Sleep -Seconds $sleepTime
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

        # Test that any user has access to the bucket now.
        $link = (Get-GcsBucket -Name $bucketName).SelfLink
        $response = Invoke-WebRequest $link
        $response.StatusCode | Should Be 200

        # Test that the newly created object doesn't have the ACL we just added. This is because we
        # modified the ACLs for the Bucket but not the default ACLs applied to every new object created in this bucket.
        $objectName = "test-object-$r"
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName $objectName -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "allUsers"}) | Should BeNullOrEmpty

        # If we try to download this object, we should not be able to.
        $link = (Get-GcsObject -Bucket $bucketName -ObjectName $objectName).SelfLink
        { Invoke-WebRequest $link } | Should Throw "401"
    }

    It "should work for -AllAuthenticatedUsers" {
        $addedAcl = Add-GcsBucketAcl -Name $bucketName -Role Reader -AllAuthenticatedUsers
        $acl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "allAuthenticatedUsers"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true

        # Test that the newly created object doesn't have the ACL we just added. This is because we
        # modified the ACLs for the Bucket but not the default ACLs applied to every new object created in this bucket.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "allAuthenticatedUsers"}) | Should BeNullOrEmpty
    }

    It "should throw error for wrong user" {
        { Add-GcsBucketAcl -Name $bucketName -Role Reader -User blahblahblah@example.com } |
            Should Throw "Unknown user email address"
    }

    It "should work for valid user" {
        $addedAcl = Add-GcsBucketAcl -Name $bucketName -Role Owner -User $userEmail
        $acl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "user-$userEmail"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true

        # Test that the newly created object doesn't have the ACL we just added. This is because we
        # modified the ACLs for the Bucket but not the default ACLs applied to every new object created in this bucket.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "user-$userEmail"}) | Should BeNullOrEmpty
    }

    It "should throw error for wrong group" {
        { Add-GcsBucketAcl -Name $bucketName -Role Reader -Group $userEmail } |
            Should Throw "Could not find group"
    }

    It "should work for valid group" {
        $addedAcl = Add-GcsBucketAcl -Name $bucketName -Role Owner -Group $groupEmail
        $acl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "group-$groupEmail"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true

        # Test that the newly created object doesn't have the ACL we just added. This is because we
        # modified the ACLs for the Bucket but not the default ACLs applied to every new object created in this bucket.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "group-$groupEmail"}) | Should BeNullOrEmpty
    }

    It "should throw error for wrong domain" {
        { Add-GcsBucketAcl -Name $bucketName -Role Reader -Domain example.thisdomaincannottrulyexist } |
            Should Throw "Could not find domain"
    }

    It "should work for valid domain" {
        $addedAcl = Add-GcsBucketAcl -Name $bucketName -Role Owner -Domain $domain
        $acl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "domain-$domain"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true

        # Test that the newly created object doesn't have the ACL we just added. This is because we
        # modified the ACLs for the Bucket but not the default ACLs applied to every new object created in this bucket.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "domain-$domain"}) | Should BeNullOrEmpty
    }
}

Describe "Remove-GcsBucketAcl" {
    $r = Get-Random
    $script:bucketName = "gcps-testing-acl-bucket-$r"
    Start-Sleep -Seconds $sleepTime
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
        Add-GcsBucketAcl -Name $bucketName -Role Reader -AllUsers
        $allUsersAcl = (Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "allUsers"})
        $allUsersAcl | Should Not BeNullOrEmpty
        Remove-GcsBucketAcl -Name $bucketName -AllUsers
        $allUsersAcl = (Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "allUsers"})
        $allUsersAcl | Should BeNullOrEmpty

        # Test that non-authenticated user does not have access to the bucket now.
        $link = (Get-GcsBucket -Name $bucketName).SelfLink
        { Invoke-WebRequest $link } | Should Throw "401"
    }

    It "should work for -AllAuthenticatedUsers" {
        Add-GcsBucketAcl -Name $bucketName -Role Reader -AllAuthenticatedUsers
        $allAuthenticatedUsersAcl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "allAuthenticatedUsers"}
        $allAuthenticatedUsersAcl | Should Not BeNullOrEmpty
        Remove-GcsBucketAcl -Name $bucketName -AllAuthenticatedUsers
        $allAuthenticatedUsersAcl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "allAuthenticatedUsers"}
        $allAuthenticatedUsersAcl | Should BeNullOrEmpty
    }

    It "should work for user" {
        Add-GcsBucketAcl -Name $bucketName -Role Owner -User $userEmail
        $userAcl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "user-$userEmail"}
        $userAcl | Should Not BeNullOrEmpty
        Remove-GcsBucketAcl -Name $bucketName -User $userEmail
        $userAcl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "user-$userEmail"}
        $userAcl | Should BeNullOrEmpty
    }

    It "should work for group" {
        Add-GcsBucketAcl -Name $bucketName -Role Owner -Group $groupEmail
        $groupAcl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "group-$groupEmail"}
        $groupAcl | Should Not BeNullOrEmpty
        Remove-GcsBucketAcl -Name $bucketName -Group $groupEmail
        $groupAcl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "group-$groupEmail"}
        $groupAcl | Should BeNullOrEmpty
    }

    It "should work for domain" {
        Add-GcsBucketAcl -Name $bucketName -Role Owner -Domain $domain
        $domainAcl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "domain-$domain"}
        $domainAcl | Should Not BeNullOrEmpty
        Remove-GcsBucketAcl -Name $bucketName -Domain $domain
        $domainAcl = Get-GcsBucketAcl -Name $bucketName | Where-Object {$_.Entity -match "domain-$domain"}
        $domainAcl | Should BeNullOrEmpty
    }
}

Describe "Get-GcsObjectAcl" {
    $r = Get-Random
    $script:bucketName = "gcps-testing-acl-bucket-$r"
    $objectName = "testing-object-$r"
    $objectAclKind = "storage#objectAccessControl"
    Start-Sleep -Seconds $sleepTime
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
        { Get-GcsObjectAcl -Bucket "asdf" -ObjectName "unimportant" } | Should Throw "403"
    }
}

Describe "Add-GcsObjectAcl" {
    $r = Get-Random
    $script:bucketName = "gcps-testing-acl-bucket-$r"
    $objectName = "testing-object-$r"
    Start-Sleep -Seconds $sleepTime
    New-GcsBucket -Name $bucketName
    New-GcsObject -Bucket $bucketName -ObjectName $objectName -Value "Not Important."

    AfterAll {
        Remove-GcsBucket -Name $script:bucketName -Force
    }

    It "should throw error for non-existent objec" {
        { Add-GcsObjectAcl -Bucket $bucketName -ObjectName "gcps-object-no-exist" -Role Reader -AllUsers } |
            Should Throw "404"
    }

    It "should work for -AllUsers" {
        $addedAcl = Add-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Role Reader -AllUsers
        $acl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName | Where-Object {$_.Entity -match "allUsers"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true

        # Test that any user has access to the object now.
        $link = (Get-GcsObject -Bucket $bucketName -ObjectName $objectName).SelfLink
        $response = Invoke-WebRequest $link
        $response.StatusCode | Should Be 200
    }

    It "should work for -AllAuthenticatedUsers" {
        $addedAcl = Add-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Role Reader -AllAuthenticatedUsers
        $acl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName |
            Where-Object {$_.Entity -match "allAuthenticatedUsers"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true
    }

    It "should throw error for wrong user" {
        { Add-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Role Reader -User blahblahblah@example.com } |
            Should Throw "Unknown user email address"
    }

    It "should work for valid user" {
        $addedAcl = Add-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Role Owner -User $userEmail
        $acl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName |
            Where-Object {$_.Entity -match "user-$userEmail"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true
    }

    It "should throw error for wrong group" {
        { Add-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Role Reader -Group $userEmail } |
            Should Throw "Could not find group"
    }

    It "should work for valid group" {
        $addedAcl = Add-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Role Owner -Group $groupEmail
        $acl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName |
            Where-Object {$_.Entity -match "group-$groupEmail"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true
    }

    It "should throw error for wrong domain" {
        { Add-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Role Reader -Domain example.thisdomaincannottrulyexist } |
            Should Throw "Could not find domain"
    }

    It "should work for valid domain" {
        $addedAcl = Add-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Role Owner -Domain $domain
        $acl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName | Where-Object {$_.Entity -match "domain-$domain"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true
    }
}

Describe "Remove-GcsObjectAcl" {
    $r = Get-Random
    $script:bucketName = "gcps-testing-acl-bucket-$r"
    $objectName = "testing-object-$r"
    Start-Sleep -Seconds $sleepTime
    New-GcsBucket -Name $bucketName
    New-GcsObject -Bucket $bucketName -ObjectName $objectName -Value "Not Important."

    AfterAll {
        Remove-GcsBucket -Name $script:bucketName -Force
    }

    It "should throw error for non-existent bucket" {
        { Remove-GcsObjectAcl -Bucket $bucketName -ObjectName "gcps-object-no-exist" -AllUsers } | Should Throw "404"
    }

    It "should throw error for non-existent ACL" {
        { Remove-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Domain google.com } | Should Throw "404"
    }

    It "should work for -AllUsers" {
        Add-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Role Reader -AllUsers
        $allUsersAcl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName | Where-Object {$_.Entity -match "allUsers"}
        $allUsersAcl | Should Not BeNullOrEmpty
        Remove-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -AllUsers
        $allUsersAcl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName | Where-Object {$_.Entity -match "allUsers"}
        $allUsersAcl | Should BeNullOrEmpty

        # Test that non-authenticated user does not have access to the object now.
        $link = (Get-GcsObject -Bucket $bucketName -ObjectName $objectName).SelfLink
        { Invoke-WebRequest $link } | Should Throw "401"
    }

    It "should work for -AllAuthenticatedUsers" {
        Add-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Role Reader -AllAuthenticatedUsers
        $allAuthenticatedUsersAcl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName | Where-Object {$_.Entity -match "allAuthenticatedUsers"}
        $allAuthenticatedUsersAcl | Should Not BeNullOrEmpty
        Remove-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -AllAuthenticatedUsers
        $allAuthenticatedUsersAcl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName | Where-Object {$_.Entity -match "allAuthenticatedUsers"}
        $allAuthenticatedUsersAcl | Should BeNullOrEmpty
    }

    It "should work for user" {
        Add-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Role Owner -User $userEmail
        $userAcl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName | Where-Object {$_.Entity -match "user-$userEmail"}
        $userAcl | Should Not BeNullOrEmpty
        Remove-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -User $userEmail
        $userAcl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName | Where-Object {$_.Entity -match "user-$userEmail"}
        $userAcl | Should BeNullOrEmpty
    }

    It "should work for group" {
        Add-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Role Owner -Group $groupEmail
        $groupAcl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName | Where-Object {$_.Entity -match "group-$groupEmail"}
        $groupAcl | Should Not BeNullOrEmpty
        Remove-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Group $groupEmail
        $groupAcl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName | Where-Object {$_.Entity -match "group-$groupEmail"}
        $groupAcl | Should BeNullOrEmpty
    }

    It "should work for domain" {
        Add-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Role Owner -Domain $domain
        $domainAcl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName | Where-Object {$_.Entity -match "domain-$domain"}
        $domainAcl | Should Not BeNullOrEmpty
        Remove-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName -Domain $domain
        $domainAcl = Get-GcsObjectAcl -Bucket $bucketName -ObjectName $objectName | Where-Object {$_.Entity -match "domain-$domain"}
        $domainAcl | Should BeNullOrEmpty
    }
}

Describe "Get-GcsDefaultObjectAcl" {
    $r = Get-Random
    $script:bucketName = "gcps-testing-acl-bucket-$r"
    $defaultObjectAcl = "storage#objectAccessControl"
    Start-Sleep -Seconds $sleepTime
    New-GcsBucket -Name $bucketName

    AfterAll {
        Remove-GcsBucket -Name $script:bucketName -Force
    }

    It "should fail to return for non-existent buckets" {
        { Get-GcsDefaultObjectAcl -Name "gcps-bucket-no-exist" } | Should Throw "404"
    }

    It "should work" {
        $bucketAcls = Get-GcsDefaultObjectAcl -Name $bucketName
        # By default, a bucket should have project-owners-ID, project-editors-ID, project-viewers-ID ACL.
        $bucketAcls.Count -ge 3 | Should Be $true
        $projectOwnerAcl = $bucketAcls | Where-Object {$_.Entity -like "project-owners*"} | Select -First 1

        $projectOwnerAcl | Should Not BeNullOrEmpty
        $projectOwnerAcl.Entity | Should Match "project-owners"
        $projectOwnerAcl.Kind | Should BeExactly $defaultObjectAcl
        $projectOwnerAcl.Role | Should BeExactly "OWNER"

        $projectEditorAcl = $bucketAcls | Where-Object {$_.Entity -like "project-editors*"} | Select -First 1
        $projectEditorAcl | Should Not BeNullOrEmpty
        $projectEditorAcl.Entity | Should Match "project-editors"
        $projectEditorAcl.Kind | Should BeExactly $defaultObjectAcl
        $projectEditorAcl.Role | Should BeExactly "OWNER"

        $projectViewerAcl = $bucketAcls | Where-Object {$_.Entity -like "project-viewers*"} | Select -First 1
        $projectViewerAcl | Should Not BeNullOrEmpty
        $projectViewerAcl.Entity | Should Match "project-viewers"
        $projectViewerAcl.Kind | Should BeExactly $defaultObjectAcl
        $projectViewerAcl.Role | Should BeExactly "READER"
    }

    It "should give access errors as appropriate" {
        { Get-GcsDefaultObjectAcl -Name "asdf" } | Should Throw "403"
    }
}

Describe "Add-GcsDefaultObjectAcl" {
    $r = Get-Random
    $script:bucketName = "gcps-testing-acl-bucket-$r"
    Start-Sleep -Seconds $sleepTime
    New-GcsBucket -Name $bucketName

    AfterAll {
        Remove-GcsBucket -Name $script:bucketName -Force
    }

    It "should throw error for non-existent bucket" {
        { Add-GcsDefaultObjectAcl -Name "gcps-bucket-no-exist" -Role Reader -AllUsers } | Should Throw "404"
    }

    It "should work for -AllUsers" {
        $addedAcl = Add-GcsDefaultObjectAcl -Name $bucketName -Role Reader -AllUsers
        $acl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "allUsers"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true

        # Test that the newly created object has the ACL we just add.
        $objectName = "test-object-$r"
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName $objectName -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "allUsers" -and $_.Role -eq "Reader"}) | Should Not BeNullOrEmpty

        # Test that any user can at least have access to the object now.
        $link = (Get-GcsObject -Bucket $bucketName -ObjectName $objectName).SelfLink
        $response = Invoke-WebRequest $link
        $response.StatusCode | Should Be 200
    }

    It "should work for -AllAuthenticatedUsers" {
        $addedAcl = Add-GcsDefaultObjectAcl -Name $bucketName -Role Reader -AllAuthenticatedUsers
        $acl = Get-GcsDefaultObjectAcl -Name $bucketName |
            Where-Object {$_.Entity -match "allAuthenticatedUsers"} |
            Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true

        # Test that the newly created object has the ACL we just add.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "allAuthenticatedUsers" -and $_.Role -eq "Reader"}) |
            Should Not BeNullOrEmpty
    }

    It "should throw error for wrong user" {
        { Add-GcsBucketAcl -Name $bucketName -Role Reader -User blahblahblah@example.com } |
            Should Throw "Unknown user email address"
    }

    It "should work for valid user" {
        $addedAcl = Add-GcsDefaultObjectAcl -Name $bucketName -Role Owner -User $userEmail
        $acl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "user-$userEmail"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true

        # Test that the newly created object has the ACL we just add.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "user-$userEmail" -and $_.Role -eq "Owner"}) | Should Not BeNullOrEmpty
    }

    It "should throw error for wrong group" {
        { Add-GcsDefaultObjectAcl -Name $bucketName -Role Reader -Group $userEmail } |
            Should Throw "Could not find group"
    }

    It "should work for valid group" {
        $addedAcl = Add-GcsDefaultObjectAcl -Name $bucketName -Role Owner -Group $groupEmail
        $acl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "group-$groupEmail"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true

        # Test that the newly created object has the ACL we just add.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "group-$groupEmail" -and $_.Role -eq "Owner"}) | Should Not BeNullOrEmpty
    }

    It "should throw error for wrong domain" {
        { Add-GcsDefaultObjectAcl -Name $bucketName -Role Reader -Domain example.thisdomaincannottrulyexist } |
            Should Throw "Could not find domain"
    }

    It "should work for valid domain" {
        $addedAcl = Add-GcsDefaultObjectAcl -Name $bucketName -Role Owner -Domain $domain
        $acl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "domain-$domain"} | Select -First 1
        CompareAcl $addedAcl $acl | Should Be $true

        # Test that the newly created object has the ACL we just add.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "domain-$domain" -and $_.Role -eq "Owner"}) | Should Not BeNullOrEmpty
    }
}

Describe "Remove-GcsDefaultObjectAcl" {
    $r = Get-Random
    $script:bucketName = "gcps-testing-acl-bucket-$r"
    Start-Sleep -Seconds $sleepTime
    New-GcsBucket -Name $bucketName

    AfterAll {
        Remove-GcsBucket -Name $script:bucketName -Force
    }

    It "should throw error for non-existent bucket" {
        { Add-GcsDefaultObjectAcl -Name "gcps-bucket-no-exist" -Role Reader -AllUsers } | Should Throw "404"
    }

    It "should throw error for non-existent ACL" {
        { Remove-GcsDefaultObjectAcl -Name $bucketName -Domain google.com } | Should Throw "404"
    }

    It "should work for -AllUsers" {
        Add-GcsDefaultObjectAcl -Name $bucketName -Role Reader -AllUsers
        $allUsersAcl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "allUsers"}
        $allUsersAcl | Should Not BeNullOrEmpty
        Remove-GcsDefaultObjectAcl -Name $bucketName -AllUsers
        $allUsersAcl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "allUsers"}
        $allUsersAcl | Should BeNullOrEmpty

        # Test that the newly created object doesn't have the ACL we just add.
        $objectName = "test-object-$r"
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName $objectName -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "allUsers" -and $_.Role -eq "Reader"}) | Should BeNullOrEmpty

        # Test that non-authenticated user does not have access to the object now.
        $link = (Get-GcsObject -Bucket $bucketName -ObjectName $objectName).SelfLink
        { Invoke-WebRequest $link } | Should Throw "401"
    }

    It "should work for -AllAuthenticatedUsers" {
        Add-GcsDefaultObjectAcl -Name $bucketName -Role Reader -AllAuthenticatedUsers
        $allAuthenticatedUsersAcl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "allAuthenticatedUsers"}
        $allAuthenticatedUsersAcl | Should Not BeNullOrEmpty
        Remove-GcsDefaultObjectAcl -Name $bucketName -AllAuthenticatedUsers
        $allAuthenticatedUsersAcl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "allAuthenticatedUsers"}
        $allAuthenticatedUsersAcl | Should BeNullOrEmpty

        # Test that the newly created object doesn't have the ACL we just add.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "allAuthenticatedUsers" -and $_.Role -eq "Reader"}) |
            Should BeNullOrEmpty
    }

    It "should work for user" {
        Add-GcsDefaultObjectAcl -Name $bucketName -Role Owner -User $userEmail
        $userAcl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "user-$userEmail"}
        $userAcl | Should Not BeNullOrEmpty
        Remove-GcsDefaultObjectAcl -Name $bucketName -User $userEmail
        $userAcl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "user-$userEmail"}
        $userAcl | Should BeNullOrEmpty

        # Test that the newly created object doesn't have the ACL we just add.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "user-$userEmail" -and $_.Role -eq "Owner"}) | Should BeNullOrEmpty
    }

    It "should work for group" {
        Add-GcsDefaultObjectAcl -Name $bucketName -Role Owner -Group $groupEmail
        $groupAcl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "group-$groupEmail"}
        $groupAcl | Should Not BeNullOrEmpty
        Remove-GcsDefaultObjectAcl -Name $bucketName -Group $groupEmail
        $groupAcl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "group-$groupEmail"}
        $groupAcl | Should BeNullOrEmpty

        # Test that the newly created object doesn't have the ACL we just add.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "group-$groupEmail" -and $_.Role -eq "Owner"}) | Should BeNullOrEmpty
    }

    It "should work for domain" {
        Add-GcsDefaultObjectAcl -Name $bucketName -Role Owner -Domain $domain
        $domainAcl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "domain-$domain"}
        $domainAcl | Should Not BeNullOrEmpty
        Remove-GcsDefaultObjectAcl -Name $bucketName -Domain $domain
        $domainAcl = Get-GcsDefaultObjectAcl -Name $bucketName | Where-Object {$_.Entity -match "domain-$domain"}
        $domainAcl | Should BeNullOrEmpty

        # Test that the newly created object doesn't have the ACL we just add.
        $objectAcl = (New-GcsObject -Bucket $bucketName -ObjectName "test-object-$r" -Value "blah" -Force).Acl
        ($objectAcl | Where-Object {$_.Entity -match "domain-$domain" -and $_.Role -eq "Owner"}) | Should BeNullOrEmpty
    }
}

Reset-GCloudConfig $oldActiveConfig $configName
