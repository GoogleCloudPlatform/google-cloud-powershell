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
    /// <para type="synopsis">
    /// Add an IAM policy binding to a project.
    /// </para>
    /// <para type="description">
    /// Add an IAM policy binding to a project. The cmdlet will use the default project if -Project is not used.
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
    public class AddGcIamPolicyBindingCmdlet : GcIamPolicyBindingCmdlet, IDynamicParameters
    {
        private class ParameterSetNames
        {
            public const string User = nameof(User);
            public const string ServiceAccount = nameof(ServiceAccount);
            public const string Group = nameof(Group);
            public const string Domain = nameof(Domain);
        }

        /// <summary>
        /// <para type="description">
        /// The project to set the IAM Policy Bindings. If not set via PowerShell parameter processing, will
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
                ParameterAttribute paramAttribute = new ParameterAttribute()
                {
                    Mandatory = true,
                    HelpMessage = "Role that is assigned to the specified member."
                };
                List<Attribute> attributeLists = new List<Attribute>() { paramAttribute };

                if (Project != null)
                {
                    // If the cmdlet is not executing and the user is only using tab completion, the string project
                    // will have double quotes at the start and end so we have to trim that.
                    Project = Project.Trim('"');

                    // If the project is a variable, then we have to extract out the variable name.
                    if (Project.StartsWith("$"))
                    {
                        // Try to resolve the variable project, if unsuccessful, set it to an empty string.
                        Project = ResolveVariable(Project, string.Empty).ToString();
                    }
                }

                // If we cannot resolve the variable or the user has not entered parameter for the project yet,
                // project will be null here.
                if (string.IsNullOrWhiteSpace(Project))
                {
                    Project = CloudSdkSettings.GetSettingsValue(CloudSdkSettings.CommonProperties.Project);
                }

                string[] roles = GetGrantableRoles(Project);
                // If there are no roles, do not add validate set attribute to the parameter (hence, no tab completion).
                if (roles.Length != 0)
                {
                    var validateSetAttribute = new ValidateSetAttribute(roles);
                    validateSetAttribute.IgnoreCase = true;
                    attributeLists.Add(validateSetAttribute);
                }

                Collection<Attribute> attributes = new Collection<Attribute>(attributeLists);
                var param = new RuntimeDefinedParameter("Role", typeof(string), attributes);
                _dynamicParameters.Add("Role", param);
            }

            return _dynamicParameters;
        }

        /// <summary>
        /// Returns the appropriate member string based on the parameter set.
        /// </summary>
        private string GetMember()
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

        protected override void ProcessRecord()
        {
            if (_dynamicParameters == null
                || !_dynamicParameters.ContainsKey("Role")
                || string.IsNullOrWhiteSpace(_dynamicParameters["Role"].Value?.ToString()))
            {
                throw new PSArgumentNullException("Role");
            }

            string role = _dynamicParameters["Role"].Value.ToString();
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
                    if (!binding.Members.Contains(role, StringComparer.OrdinalIgnoreCase))
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
}
