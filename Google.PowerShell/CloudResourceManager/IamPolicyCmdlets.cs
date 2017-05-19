// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.CloudResourceManager.v1;
using Google.Apis.CloudResourceManager.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using IamService = Google.Apis.Iam.v1.IamService;

namespace Google.PowerShell.CloudResourceManager
{
    /// <summary>
    /// Base class for IAM Policy Binding Cmdlets. Contains common method to retrieve all grantable roles.
    /// </summary>
    public class GcIamPolicyBindingCmdlet : CloudResourceManagerCmdlet
    {
        // IAM Service used for getting roles that can be granted to a project.
        private Lazy<IamService> _iamService =
            new Lazy<IamService>(() => new IamService(GetBaseClientServiceInitializer()));

        /// <summary>
        /// Dictionary of roles with key as project and value as the roles available in the project.
        /// This dictionary is used for caching the various roles available in a project.
        /// </summary>
        private static ConcurrentDictionary<string, string[]> s_rolesDictionary =
            new ConcurrentDictionary<string, string[]>();

        /// <summary>
        /// Returns all the possible roles that can be granted in a project.
        /// This is used to provide tab completion for -Role parameter.
        /// </summary>
        protected string[] GetGrantableRoles(string projectName)
        {
            // We cache the roles in s_rolesDictionary to speed up processing.
            if (!s_rolesDictionary.ContainsKey(projectName))
            {
                var roleRequestBody = new Apis.Iam.v1.Data.QueryGrantableRolesRequest()
                {
                    // The full resource name has to start with "//".
                    FullResourceName = $"//cloudresourcemanager.googleapis.com/projects/{projectName}"
                };

                try
                {
                    Apis.Iam.v1.RolesResource.QueryGrantableRolesRequest roleRequest =
                        _iamService.Value.Roles.QueryGrantableRoles(roleRequestBody);
                    Apis.Iam.v1.Data.QueryGrantableRolesResponse response = roleRequest.Execute();

                    s_rolesDictionary[projectName] = response.Roles.Select(role => role.Name).ToArray();
                }
                catch
                {
                    // In the case that we cannot get all the possible roles, we just do not provide tab completion.
                    s_rolesDictionary[projectName] = new string[] { };
                }
            }

            return s_rolesDictionary[projectName];
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Lists all IAM policy bindings in a project.
    /// </para>
    /// <para type="description">
    /// Lists all IAM policy bindings in a project. The cmdlet will use the default project if -Project is not used.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcIamPolicyBinding</code>
    ///   <para>This command gets all the IAM policy bindings from the default project.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/iam/docs/overview)">
    /// [Google Cloud IAM]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcIamPolicyBinding")]
    public class GetGcIamPolicyBindingCmdlet : GcIamPolicyBindingCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for IAM Policies. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        protected override void ProcessRecord()
        {
            ProjectsResource.GetIamPolicyRequest request = Service.Projects.GetIamPolicy(new GetIamPolicyRequest(), $"{Project}");
            Policy policy = request.Execute();
            WriteObject(policy.Bindings, true);
        }
    }

    /// <summary>
    /// Base class for cmdlet that add or remove IAM Policy Bindings.
    /// Contains all the neccessary parameters. Inherited class just needs to implement ProcessRecord.
    /// </summary>
    public class AddOrRemoveGcIamPolicyBindingCmdlet : GcIamPolicyBindingCmdlet, IDynamicParameters
    {
        protected class ParameterSetNames
        {
            public const string User = nameof(User);
            public const string ServiceAccount = nameof(ServiceAccount);
            public const string Group = nameof(Group);
            public const string Domain = nameof(Domain);
        }

        /// <summary>
        /// <para type="description">
        /// The project for the IAM Policy Bindings. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Email address that represents a specific Google account.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.User)]
        [ValidateNotNullOrEmpty]
        public string User { get; set; }

        /// <summary>
        /// <para type="description">
        /// Email address that represents a service account.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ServiceAccount)]
        [ValidateNotNullOrEmpty]
        public string ServiceAccount { get; set; }

        /// <summary>
        /// <para type="description">
        /// Email address that represents a Google group.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.Group)]
        [ValidateNotNullOrEmpty]
        public string Group { get; set; }

        /// <summary>
        /// <para type="description">
        /// A Google Apps domain name that represents all the users of that domain.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.Domain)]
        [ValidateNotNullOrEmpty]
        public string Domain { get; set; }

        /// <summary>
        /// This dynamic parameter dictionary is used by PowerShell to generate parameters dynamically.
        /// </summary>
        private RuntimeDefinedParameterDictionary _dynamicParameters;

        /// <summary>
        /// Creates a dynamic parameter "Role" based on the Project parameter.
        /// This function will first issue a call to the server to get all the possible roles based on
        /// the Project parameter. It will then create a dynamic parameter that corresponds to:
        /// [Parameter(Mandatory = true, HelpMessage = "Role that is assigned to the specified member.")]
        /// [ValidateSet(The possible roles we get from the server)]
        /// public string Role { get; set; }
        /// </summary>
        public object GetDynamicParameters()
        {
            if (_dynamicParameters == null)
            {
                _dynamicParameters = new RuntimeDefinedParameterDictionary();

                // Try to resolve Project variable to a string, use default value from the SDK if we fail to do so.
                Project = GetCloudSdkSettingValue(CloudSdkSettings.CommonProperties.Project, Project);

                string[] roles = GetGrantableRoles(Project);

                RuntimeDefinedParameter param = GenerateRuntimeParameter(
                    parameterName: "Role",
                    helpMessage: "Role that is assigned to the specified member.",
                    validSet: roles,
                    isMandatory: true);

                _dynamicParameters.Add("Role", param);
            }

            return _dynamicParameters;
        }

        /// <summary>
        /// Returns the appropriate role of the member.
        /// </summary>
        protected string GetRole()
        {
            if (_dynamicParameters == null
                || !_dynamicParameters.ContainsKey("Role")
                || string.IsNullOrWhiteSpace(_dynamicParameters["Role"].Value?.ToString()))
            {
                throw new PSArgumentNullException("Role");
            }
            return _dynamicParameters["Role"].Value.ToString();
        }

        /// <summary>
        /// Returns the appropriate member string based on the parameter set.
        /// </summary>
        protected string GetMember()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.User:
                    return $"user:{User.ToLower()}";
                case ParameterSetNames.Group:
                    return $"group:{Group.ToLower()}";
                case ParameterSetNames.Domain:
                    return $"domain:{Domain.ToLower()}";
                case ParameterSetNames.ServiceAccount:
                    return $"serviceAccount:{ServiceAccount.ToLower()}";
                default:
                    throw UnknownParameterSetException;
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Adds an IAM policy binding to a project.
    /// </para>
    /// <para type="description">
    /// Adds an IAM policy binding to a project. The cmdlet will use the default project if -Project is not used.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Add-GcIamPolicyBinding -Role roles/owner -User abc@google.com -Project "my-project"</code>
    ///   <para>This command gives user abc@google.com owner role in the project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Add-GcIamPolicyBinding -Role roles/container.admin -Group my-group@google.com</code>
    ///   <para>This command gives the group my-group@google.com container admin role in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Add-GcIamPolicyBinding -Role roles/container.admin `
    ///                                  -ServiceAccount service@project.iam.gserviceaccount.com
    ///   </code>
    ///   <para>
    ///   This command gives the serviceaccount service@project.iam.gserviceaccount.com
    ///   container admin role in the default project.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Add-GcIamPolicyBinding -Role roles/editor -Domain example.com</code>
    ///   <para>This command gives all users of the domain example.com editor role in the default project.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/iam/docs/overview)">
    /// [Google Cloud IAM]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcIamPolicyBinding", DefaultParameterSetName = ParameterSetNames.User)]
    public class AddGcIamPolicyBindingCmdlet : AddOrRemoveGcIamPolicyBindingCmdlet
    {
        protected override void ProcessRecord()
        {
            string role = GetRole();
            string member = GetMember();

            // We have to search through all the existing bindings and try to insert the role if possible
            // because otherwise, we will delete all the existing bindings. If the role is already there,
            // we don't need to execute the request.
            ProjectsResource.GetIamPolicyRequest getRequest = Service.Projects.GetIamPolicy(new GetIamPolicyRequest(), $"{Project}");
            Policy existingPolicy = getRequest.Execute();
            bool needToExecuteRequest = true;
            bool bindingFound = false;

            foreach (Binding binding in existingPolicy.Bindings)
            {
                if (string.Equals(role, binding.Role, StringComparison.OrdinalIgnoreCase))
                {
                    bindingFound = true;
                    if (!binding.Members.Contains(member, StringComparer.OrdinalIgnoreCase))
                    {
                        binding.Members.Add(member);
                    }
                    else
                    {
                        needToExecuteRequest = false;
                    }
                    break;
                }
            }

            if (!bindingFound)
            {
                var newBinding = new Binding()
                {
                    Role = role,
                    Members = new List<string>() { member }
                };
                existingPolicy.Bindings.Add(newBinding);
                needToExecuteRequest = true;
            }

            if (!needToExecuteRequest)
            {
                WriteObject(existingPolicy.Bindings, true);
            }
            else
            {
                var requestBody = new SetIamPolicyRequest() { Policy = existingPolicy };
                ProjectsResource.SetIamPolicyRequest setRequest =
                    Service.Projects.SetIamPolicy(requestBody, $"{Project}");

                Policy changedPolicy = setRequest.Execute();
                WriteObject(changedPolicy.Bindings, true);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes an IAM policy binding to a project.
    /// </para>
    /// <para type="description">
    /// Removes an IAM policy binding to a project. The cmdlet will use the default project if -Project is not used.
    /// If the binding does not exist, the cmdlet will not raise error.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcIamPolicyBinding -Role roles/owner -User abc@google.com -Project "my-project"</code>
    ///   <para>This command removes the owner role of user abc@google.com in the project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcIamPolicyBinding -Role roles/container.admin -Group my-group@google.com</code>
    ///   <para>
    ///   This command removes the container admin role of the group my-group@google.com in the default project.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Remove-GcIamPolicyBinding -Role roles/container.admin `
    ///                                  -ServiceAccount service@project.iam.gserviceaccount.com
    ///   </code>
    ///   <para>
    ///   This command removes the container admin role of the serviceaccount service@project.iam.gserviceaccount.com
    ///   in the default project.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcIamPolicyBinding -Role roles/editor -Domain example.com</code>
    ///   <para>This command removes the editor role of all users of the domain example.com in the default project.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/iam/docs/overview)">
    /// [Google Cloud IAM]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcIamPolicyBinding", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.User)]
    public class RemoveGcIamPolicyBindingCmdlet : AddOrRemoveGcIamPolicyBindingCmdlet
    {
        protected override void ProcessRecord()
        {
            string role = GetRole();
            string member = GetMember();

            // Remove the role from existing bindings.
            ProjectsResource.GetIamPolicyRequest getRequest = Service.Projects.GetIamPolicy(new GetIamPolicyRequest(), $"{Project}");
            Policy existingPolicy = getRequest.Execute();
            bool needToExecuteRequest = false;

            foreach (Binding binding in existingPolicy.Bindings)
            {
                if (string.Equals(role, binding.Role, StringComparison.OrdinalIgnoreCase))
                {
                    if (binding.Members.Contains(member, StringComparer.OrdinalIgnoreCase))
                    {
                        binding.Members.Remove(member);
                        needToExecuteRequest = true;
                    }
                    break;
                }
            }

            if (!needToExecuteRequest)
            {
                WriteObject(existingPolicy.Bindings, true);
            }
            else
            {
                if (ShouldProcess($"{member}", $"Remove IAM policy binding in project '{Project}' for role '{role}'"))
                {
                    var requestBody = new SetIamPolicyRequest() { Policy = existingPolicy };
                    ProjectsResource.SetIamPolicyRequest setRequest =
                        Service.Projects.SetIamPolicy(requestBody, $"{Project}");

                    Policy changedPolicy = setRequest.Execute();
                    WriteObject(changedPolicy.Bindings, true);
                }
            }
        }
    }
}
