// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.CloudResourceManager.v1;
using Google.Apis.CloudResourceManager.v1.Data;
using Google.PowerShell.Common;
using System.Management.Automation;

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
    ///   <code>PS C:\> Get-GcIamPolicy</code>
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
}
