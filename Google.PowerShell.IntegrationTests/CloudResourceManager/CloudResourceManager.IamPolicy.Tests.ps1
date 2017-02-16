. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
# We use another project for testing adding policy bindings so in case we messed up the permission, other tests won't fail.
$script:gcloudPowerShellProject2 = "gcloud-powershell-testing-2"
# Service account email that is used by appveyor. This service acccount has permission on both PowerShell testing projects.
$script:appveyorServiceAccEmail = "appveyorci-testing@gcloud-powershell-testing.iam.gserviceaccount.com"

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
    It "should work for -User" {
        $role = "roles/browser"
        $user = "quoct@google.com"

        try {
            Add-GcIamPolicyBinding -User $user -Role $role -Project $gcloudPowerShellProject2

            $bindings = Get-GcIamPolicyBinding -Project $gcloudPowerShellProject2
            $binding = $bindings | Where-Object {$_.Role -eq $role}
            $binding.Members -contains "user:$user" | Should Be $true
        }
        finally {
            gcloud projects remove-iam-policy-binding $gcloudPowerShellProject2 `
                                                      --member="user:$user" --role="$role" 2>$null
        }
    }

    It "should work for -Group" {
        $role = "roles/logging.viewer"
        $group = "cloudsharp-eng@google.com"

        try {
            Add-GcIamPolicyBinding -Group $group -Role $role -Project $gcloudPowerShellProject2

            $bindings = Get-GcIamPolicyBinding -Project $gcloudPowerShellProject2
            $binding = $bindings | Where-Object {$_.Role -eq $role}
            $binding.Members -contains "group:$group" | Should Be $true
        }
        finally {
            gcloud projects remove-iam-policy-binding $gcloudPowerShellProject2 `
                                                      --member="group:$group" --role="$role" 2>$null
        }
    }

    It "should work for -ServiceAccount" {
        $role = "roles/datastore.viewer"

        try {
            Add-GcIamPolicyBinding -ServiceAccount $appveyorServiceAccEmail -Role $role -Project $gcloudPowerShellProject2
            $bindings = Get-GcIamPolicyBinding -Project $gcloudPowerShellProject2
            $binding = $bindings | Where-Object {$_.Role -eq $role}
            $binding.Members -contains "serviceAccount:$appveyorServiceAccEmail" | Should Be $true
        }
        finally {
            gcloud projects remove-iam-policy-binding $gcloudPowerShellProject2 `
                                                      --member="serviceAccount:$appveyorServiceAccEmail" --role="$role" 2>$null
        }
    }

    It "should work for -Domain" {
        $role = "roles/viewer"
        $domain = "google.com"

        try {
            Add-GcIamPolicyBinding -Domain $domain -Role $role -Project $gcloudPowerShellProject2
            $bindings = Get-GcIamPolicyBinding -Project $gcloudPowerShellProject2
            $binding = $bindings | Where-Object {$_.Role -eq $role}
            $binding.Members -contains "domain:$domain" | Should Be $true
        }
        finally {
            gcloud projects remove-iam-policy-binding $gcloudPowerShellProject2 `
                                                      --member="domain:$domain" --role="$role" 2>$null
        }
    }

    It "should not remove existing bindings" {
        $role = "roles/browser"
        $user = "quoct@google.com"
        $group = "cloudsharp-eng@google.com"

        try {
            Add-GcIamPolicyBinding -User $user -Role $role -Project $gcloudPowerShellProject2
            Add-GcIamPolicyBinding -Group $group -Role $role -Project $gcloudPowerShellProject2

            $bindings = Get-GcIamPolicyBinding -Project $gcloudPowerShellProject2
            $binding = $bindings | Where-Object {$_.Role -eq $role}
            $binding.Members -contains "user:$user" | Should Be $true
            $binding.Members -contains "group:$group" | Should Be $true
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
        $user = "quoct@google.com"
        { Add-GcIamPolicyBinding -User $user -Role $role "asdf" } | Should Throw "403"
    }
}
