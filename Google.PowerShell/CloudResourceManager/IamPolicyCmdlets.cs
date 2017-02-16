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
    public class GetGcIamPolicyBindingCmdlet : CloudResourceManagerCmdlet
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
    public class AddGcIamPolicyBindingCmdlet : CloudResourceManagerCmdlet, IDynamicParameters
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
        /// Dictionary of roles with key as project and value as the roles available in the project.
        /// This dictionary is used for caching the various roles available in a project.
        /// </summary>
        private static ConcurrentDictionary<string, string[]> s_rolesDictionary =
            new ConcurrentDictionary<string, string[]>();

        private IamService iamService = new IamService(GetBaseClientServiceInitializer());

        public object GetDynamicParameters()
        {
            if (_dynamicParameters == null)
            {
                _dynamicParameters = new RuntimeDefinedParameterDictionary();
                ParameterAttribute paramAttribute = new ParameterAttribute()
                {
                    Mandatory = true,
                    HelpMessage = "Role that is assigned to the specified members."
                };
                List<Attribute> attributeLists = new List<Attribute>() { paramAttribute };

                if (Project != null)
                {
                    // If the cmdlet is not executing and user is only using tab completion, the string project
                    // will have double quotes at the start and end so we have to trim that.
                    Project = Project.Trim('"');

                    // If the project is a variable, then we have to extract out the variable name.
                    if (Project.StartsWith("$"))
                    {
                        // In case project is something like $script:variableName.
                        if (Project.Contains(":"))
                        {
                            Project = Project.Split(new char[] { ':' }, 2).Last();
                        } else
                        {
                            Project = Project.Substring(1);
                        }
                        // If we cannot get the variable, set it to empty.
                        Project = GetVariableValue(Project, string.Empty).ToString();
                    }
                }

                // If we cannot resolve variable or user has not entered parameter for project yet, project
                // will be null here.
                if (string.IsNullOrWhiteSpace(Project))
                {
                    Project = CloudSdkSettings.GetSettingsValue(CloudSdkSettings.CommonProperties.Project);
                }

                if (!s_rolesDictionary.ContainsKey(Project))
                {
                    var roleRequestBody = new Apis.Iam.v1.Data.QueryGrantableRolesRequest()
                    {
                        FullResourceName = $"//cloudresourcemanager.googleapis.com/projects/{Project}"
                    };

                    try
                    {
                        Apis.Iam.v1.RolesResource.QueryGrantableRolesRequest roleRequest =
                            iamService.Roles.QueryGrantableRoles(roleRequestBody);
                        Apis.Iam.v1.Data.QueryGrantableRolesResponse response = roleRequest.Execute();

                        s_rolesDictionary[Project] = response.Roles.Select(role => role.Name).ToArray();
                    }
                    catch
                    {
                        s_rolesDictionary[Project] = new string[] { };
                    }
                }

                string[] roles = s_rolesDictionary[Project];
                if (roles.Length != 0)
                {
                    var validateSetAttribute = new ValidateSetAttribute(s_rolesDictionary[Project]);
                    validateSetAttribute.IgnoreCase = true;
                    attributeLists.Add(validateSetAttribute);
                }

                Collection<Attribute> attributes = new Collection<Attribute>(attributeLists);
                var param = new RuntimeDefinedParameter("Role", typeof(string), attributes);
                _dynamicParameters.Add("Role", param);
            }

            return _dynamicParameters;
        }

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
                || _dynamicParameters["Role"].Value == null)
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
                    } else
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
