. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
# We use another project for testing adding policy bindings so in case we messed up the permission, other tests won't fail.
$script:gcloudPowerShellProject2 = "gcloud-powershell-testing-2"
# Service account email that is used by appveyor. This service acccount has permission on both PowerShell testing projects.
$script:appveyorServiceAccEmail = "appveyorci-testing@gcloud-powershell-testing.iam.gserviceaccount.com"
# Test google account email.
$script:user = "powershelltesting@gmail.com"
# Test google group.
$script:group = "test-group-for-google-cloud-powershell@google.com"

Describe "Get-GcIamPolicyBinding" {
    It "should work" {
        $policies = Get-GcIamPolicyBinding
        $policies.Count -gt 0 | Should Be $true

        $editors = $policies | Where-Object {$_.Role -eq "roles/editor"}
        $editors.Members.Count -gt 0 | Should Be $true
        $editors.Members -contains "serviceAccount:$script:appveyorServiceAccEmail" | Should Be $true
    }

    It "should throw if we don't have permission" {
        { Get-GcIamPolicyBinding -Project "asdf" } | Should Throw "403"
    }
}

Describe "Add-GcIamPolicyBinding" {
    # Returns true if binding for member $member to role $role exists in project $projectToCheck.
    function Test-Binding($member, $role, $projectToCheck) {
        $bindings = Get-GcIamPolicyBinding -Project $projectToCheck
        $binding = $bindings | Where-Object {$_.Role -eq $role}
        if ($null -eq $binding -or $null -eq $binding.Members) {
            return $false
        }
        return ($binding.Members -contains $member)
    }

    It "should work for -User" {
        $role = "roles/browser"
        $member = "user:$user"

        try {
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $false
            Add-GcIamPolicyBinding -User $user -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $true
        }
        finally {
            gcloud projects remove-iam-policy-binding $gcloudPowerShellProject2 `
                                                      --member="user:$user" --role="$role" 2>$null
        }
    }

    It "should work for -Group" {
        $role = "roles/logging.viewer"
        $member = "group:$group"

        try {
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $false
            Add-GcIamPolicyBinding -Group $group -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $true
        }
        finally {
            gcloud projects remove-iam-policy-binding $gcloudPowerShellProject2 `
                                                      --member="group:$group" --role="$role" 2>$null
        }
    }

    It "should work for -ServiceAccount" {
        $role = "roles/datastore.viewer"
        $member = "serviceAccount:$appveyorServiceAccEmail"

        try {
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $false
            Add-GcIamPolicyBinding -ServiceAccount $appveyorServiceAccEmail -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $true
        }
        finally {
            gcloud projects remove-iam-policy-binding $gcloudPowerShellProject2 `
                                                      --member="serviceAccount:$appveyorServiceAccEmail" --role="$role" 2>$null
        }
    }

    It "should work for -Domain" {
        $role = "roles/viewer"
        $domain = "google.com"
        $member = "domain:$domain"

        try {
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $false
            Add-GcIamPolicyBinding -Domain $domain -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $true
        }
        finally {
            gcloud projects remove-iam-policy-binding $gcloudPowerShellProject2 `
                                                      --member="domain:$domain" --role="$role" 2>$null
        }
    }

    It "should work if we add binding multiple times" {
        $role = "roles/browser"
        $user = "quoct@google.com"
        $group = "cloudsharp-eng@google.com"
        $memberOne = "user:$user"
        $memberTwo = "group:$group"

        try {
            Test-Binding $memberOne $role $gcloudPowerShellProject2 | Should Be $false
            Test-Binding $memberTwo $role $gcloudPowerShellProject2 | Should Be $false
            Add-GcIamPolicyBinding -User $user -Role $role -Project $gcloudPowerShellProject2
            Add-GcIamPolicyBinding -Group $group -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $memberOne $role $gcloudPowerShellProject2 | Should Be $true
            Test-Binding $memberTwo $role $gcloudPowerShellProject2 | Should Be $true
        }
        finally {
            gcloud projects remove-iam-policy-binding $gcloudPowerShellProject2 `
                                                      --member="user:$user" --role="$role" 2>$null
            gcloud projects remove-iam-policy-binding $gcloudPowerShellProject2 `
                                                      --member="group:$group" --role="$role" 2>$null
        }
    }

    It "should throw if we don't have permission" {
        $role = "roles/browser"
        { Add-GcIamPolicyBinding -User $user -Role $role -Project "asdf" } | Should Throw "403"
    }
}

Describe "Remove-GcIamPolicyBinding" {
    # Returns true if binding for member $member to role $role exists in project $projectToCheck.
    function Test-Binding($member, $role, $projectToCheck) {
        $bindings = Get-GcIamPolicyBinding -Project $projectToCheck
        $binding = $bindings | Where-Object {$_.Role -eq $role}
        if ($null -eq $binding -or $null -eq $binding.Members) {
            return $false
        }
        return ($binding.Members -contains $member)
    }

    It "should work for -User" {
        $role = "roles/browser"
        $member = "user:$user"

        try {
            Add-GcIamPolicyBinding -User $user -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $true
        }
        finally {
            Remove-GcIamPolicyBinding -User $user -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $false
        }
    }

    It "should work for -Group" {
        $role = "roles/logging.viewer"
        $member = "group:$group"

        try {
            Add-GcIamPolicyBinding -Group $group -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $true
        }
        finally {
            Remove-GcIamPolicyBinding -Group $group -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $false
        }
    }

    It "should work for -ServiceAccount" {
        $role = "roles/datastore.viewer"
        $member = "serviceAccount:$appveyorServiceAccEmail"

        try {
            Add-GcIamPolicyBinding -ServiceAccount $appveyorServiceAccEmail `
                                   -Role $role `
                                   -Project $gcloudPowerShellProject2
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $true
        }
        finally {
            Remove-GcIamPolicyBinding -ServiceAccount $appveyorServiceAccEmail `
                                   -Role $role `
                                   -Project $gcloudPowerShellProject2
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $false
        }
    }

    It "should work for -Domain" {
        $role = "roles/viewer"
        $domain = "google.com"
        $member = "domain:$domain"

        try {
            Add-GcIamPolicyBinding -Domain $domain -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $true
        }
        finally {
            Remove-GcIamPolicyBinding -Domain $domain -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $false
        }
    }

    It "should work if we remove binding multiple times" {
        $role = "roles/browser"
        $user = "quoct@google.com"
        $group = "cloudsharp-eng@google.com"
        $memberOne = "user:$user"
        $memberTwo = "group:$group"

        try {
            Add-GcIamPolicyBinding -User $user -Role $role -Project $gcloudPowerShellProject2
            Add-GcIamPolicyBinding -Group $group -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $memberOne $role $gcloudPowerShellProject2 | Should Be $true
            Test-Binding $memberTwo $role $gcloudPowerShellProject2 | Should Be $true
        }
        finally {
            Remove-GcIamPolicyBinding -User $user -Role $role -Project $gcloudPowerShellProject2
            Remove-GcIamPolicyBinding -Group $group -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $memberOne $role $gcloudPowerShellProject2 | Should Be $false
            Test-Binding $memberTwo $role $gcloudPowerShellProject2 | Should Be $false
        }
    }

    It "should not remove binding if -WhatIf is used" {
        $role = "roles/browser"
        $member = "user:$user"

        try {
            Add-GcIamPolicyBinding -User $user -Role $role -Project $gcloudPowerShellProject2
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $true
        }
        finally {
            Remove-GcIamPolicyBinding -User $user -Role $role -Project $gcloudPowerShellProject2 -WhatIf
            Test-Binding $member $role $gcloudPowerShellProject2 | Should Be $true
        }
    }

    It "should not throw error if the binding does not exist" {
        $role = "roles/owner"
        { Remove-GcIamPolicyBinding -User $user -Role $role -Project $gcloudPowerShellProject2 } |
            Should Not Throw
    }

    It "should throw if we don't have permission" {
        $role = "roles/browser"
        { Remove-GcIamPolicyBinding -User $user -Role $role -Project "asdf" } | Should Throw "403"
    }
}
