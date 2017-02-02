// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <para type="synopsis">
    /// Gets Google Compute Engine forwarding rules.
    /// </para>
    /// <para type="description">
    /// Lists forwarding rules of a project, or gets a specific one.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GceForwardingRule</code>
    ///   <para>This command lists all forwarding rules for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceForwardingRule -Region us-central1</code>
    ///   <para>This command lists all forwarding rules in region "us-central1" for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceForwardingRule "my-forwarding-rule"</code>
    ///   <para>This command gets the forwarding rule named "my-forwarding-rule" in the default project and region.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceForwardingRule -Project my-project -Global</code>
    ///   <para>This command lists all global forwarding rules for the project named "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceForwardingRule "my-forwarding-rule" -Gobal</code>
    ///   <para>This command gets the global forwarding rule named "my-forwarding-rule" in the default project.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/forwardingRules#resource)">
    /// [Forwarding Rule resource definition]
    /// </para>
    [Cmdlet(VerbsCommon.Get, "GceForwardingRule", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(ForwardingRule))]
    public class GetGceForwardingRuleCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string OfRegion = "OfRegion";
            public const string ByLocalName = "ByLocalName";
            public const string ByGlobalName = "ByGlobalName";
        }

        /// <summary>
        /// <para type="description">
        /// The project the forwarding rules belong to. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will retrieve only global forwarding rules.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfProject)]
        [Parameter(ParameterSetName = ParameterSetNames.ByGlobalName, Mandatory = true)]
        public SwitchParameter Global { get; set; }

        /// <summary>
        /// <para type="description">
        /// The region of the forwaring rule to get. Defaults to the region in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfRegion, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByLocalName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the forwarding rule to get.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByLocalName, Mandatory = true, Position = 0)]
        [Parameter(ParameterSetName = ParameterSetNames.ByGlobalName, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.OfProject:
                    WriteObject(GetAllProjectForwardingRules(Project), true);
                    break;
                case ParameterSetNames.OfRegion:
                    WriteObject(GetRegionForwardingRules(Project, Region), true);
                    break;
                case ParameterSetNames.ByLocalName:
                    WriteObject(Service.ForwardingRules.Get(Project, Region, Name).Execute());
                    break;
                case ParameterSetNames.ByGlobalName:
                    WriteObject(Service.GlobalForwardingRules.Get(Project, Name).Execute());
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private IEnumerable<ForwardingRule> GetRegionForwardingRules(string project, string region)
        {
            ForwardingRulesResource.ListRequest request = Service.ForwardingRules.List(project, region);
            do
            {
                ForwardingRuleList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (ForwardingRule forwardingRule in response.Items)
                    {
                        yield return forwardingRule;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }


        private IEnumerable<ForwardingRule> GetAllProjectForwardingRules(string project)
        {
            if (Global)
            {
                GlobalForwardingRulesResource.ListRequest request =
                    Service.GlobalForwardingRules.List(project);
                do
                {
                    ForwardingRuleList response = request.Execute();
                    if (response.Items != null)
                    {
                        foreach (ForwardingRule forwardingRule in response.Items)
                        {
                            yield return forwardingRule;
                        }
                    }
                    request.PageToken = response.NextPageToken;
                } while (!Stopping && request.PageToken != null);

            }
            else
            {
                ForwardingRulesResource.AggregatedListRequest request =
                    Service.ForwardingRules.AggregatedList(project);
                do
                {
                    ForwardingRuleAggregatedList response = request.Execute();
                    if (response.Items != null)
                    {
                        foreach (KeyValuePair<string, ForwardingRulesScopedList> kvp in response.Items)
                        {
                            if (kvp.Value?.ForwardingRules != null)
                            {
                                foreach (ForwardingRule forwardingRule in kvp.Value.ForwardingRules)
                                {
                                    yield return forwardingRule;
                                }
                            }
                        }
                    }
                    request.PageToken = response.NextPageToken;
                } while (!Stopping && request.PageToken != null);
            }
        }
    }
}
