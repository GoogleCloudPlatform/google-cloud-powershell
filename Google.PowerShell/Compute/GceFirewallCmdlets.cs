// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System.Management.Automation;
using static Google.Apis.Compute.v1.FirewallsResource;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets firewall rules for a project.
    /// </para>
    /// <para type="description">
    /// Gets firewall rules for a project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GceFirewall</code>
    ///   <para>Lists all firewall rules in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceFirewall "my-firewall"</code>
    ///   <para>Gets the information of the firewall rule in the default project named "my-firewall".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/firewalls#resource)">
    /// [Firewall resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceFirewall")]
    [OutputType(typeof(Firewall))]
    public class GetGceFirewallCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The Project to get the firewall rule of.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the firewall rule to get. -Name and -Firewall are aliases of this parameter.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, ValueFromPipeline = true)]
        [Alias("Name", "Firewall")]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Firewall))]
        public string FirewallName { get; set; }

        protected override void ProcessRecord()
        {
            if (FirewallName == null)
            {
                WriteObject(GetProjectFirewalls(), true);
            }
            else
            {
                Firewall firewall = Service.Firewalls.Get(Project, FirewallName).Execute();
                WriteObject(firewall);
            }
        }

        private IEnumerable<Firewall> GetProjectFirewalls()
        {
            string pageToken = null;
            do
            {
                ListRequest request = Service.Firewalls.List(Project);
                request.PageToken = pageToken;
                FirewallList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (Firewall firewall in response.Items)
                    {
                        yield return firewall;
                    }
                }
                pageToken = response.NextPageToken;
            } while (pageToken != null);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Adds a new firewall rule.
    /// </para>
    /// <para type="description">
    /// Adds a new firewall rule. When given a pipeline of many Firewall.AllowedData, will collect them all and
    /// create a single new firewall rule.
    /// </para>
    /// <example>
    /// <code>
    /// PS C:\> New-GceFirewallProtocol tcp -Ports 80, 443 |
    ///     New-GceFirewallProtocol esp |
    ///     Add-GceFirewall -Name "my-firewall" -SourceTag my-source -TargetTag my-target
    /// </code>
    /// <para>Creates a new firewall rule in the default project named "my-firewall". The firewall allows
    /// traffic using tcp on ports 80 and 443 as well as the esp protocol from servers tagged my-source to
    /// servers tagged my-target.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/firewalls#resource)">
    /// [Firewall resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceFirewall")]
    [OutputType(typeof(Firewall))]
    public class AddGceFirewallCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the project to add the firewall rule to.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the new firewall rule.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// A list of allowed protocols and ports. you can use New-GceFirewallProtocol to create them.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [Alias("Allowed", "Protocol")]
        public List<Firewall.AllowedData> AllowedProtocol { get; set; }

        /// <summary>
        /// <para type="description">
        /// The human readable description of this firewall rule.
        /// </para>
        /// </summary>
        [Parameter]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// Url of the network resource for this firewall rule. If empty will be the default network.
        /// </para>
        /// </summary>
        [Parameter]
        public string Network { get; set; }

        /// <summary>
        /// <para type="description">
        /// The IP address block that this rule applies to, expressed in CIDR format. One or both of
        /// SourceRange and SourceTag may be set. If both parameters are set, an inbound connection is allowed
        /// if it matches either SourceRange or SourceTag.
        /// </para>
        /// </summary>
        [Parameter]
        public List<string> SourceRange { get; set; }

        /// <summary>
        /// <para type="description">
        /// The instance tag which this rule applies to. One or both of SourceRange and SourceTag may be set.
        /// If both parameters are set, an inbound connection is allowed it matches either SourceRange or
        /// SourceTag. Source tags cannot be used to allow access to an instance's external IP address.
        /// Source tags can only be used to control traffic traveling from an instance inside the same network
        /// as the firewall rule.
        /// </para>
        /// </summary>
        [Parameter]
        public List<string> SourceTag { get; set; }

        /// <summary>
        /// <para type="description">
        /// An instance tag indicating sets of instances located in the network that may make network
        /// connections as specified in allowed[]. If TargetTag is not specified, the firewall rule applies to
        /// all instances on the specified network.
        /// </para>
        /// </summary>
        [Parameter]
        public List<string> TargetTag { get; set; }

        private List<Firewall.AllowedData> _allAllowed = new List<Firewall.AllowedData>();

        /// <summary>
        /// Collect allowed from the pipeline.
        /// </summary>
        protected override void ProcessRecord()
        {
            _allAllowed.AddRange(AllowedProtocol);
        }

        /// <summary>
        /// Create the firewall rule.
        /// </summary>
        protected override void EndProcessing()
        {
            var firewall = new Firewall
            {
                Name = Name,
                Allowed = _allAllowed,
                Description = Description,
                Network = ConstructNetworkName(Network, Project),
                SourceRanges = SourceRange,
                SourceTags = SourceTag,
                TargetTags = TargetTag
            };
            InsertRequest request = Service.Firewalls.Insert(firewall, Project);
            WaitForGlobalOperation(Project, request.Execute());
            WriteObject(Service.Firewalls.Get(Project, Name).Execute());
            base.EndProcessing();
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes a firewall rule from a project.
    /// </para>
    /// <para type="description">
    /// Removes a firewall rule from a project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GceFirewall "my-firewall"</code>
    ///   <para>Removes the firewall named "my-firewall" in the default project.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/firewalls#resource)">
    /// [Firewall resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceFirewall", SupportsShouldProcess = true)]
    public class RemoveGceFirewallCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The name of the project from which to remove the firewall.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the firewall rule to remove.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, ValueFromPipeline = true,
            Mandatory = true, ParameterSetName = ParameterSetNames.ByName)]
        [Alias("Name", "Firewall")]
        public string FirewallName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The firewall object to be removed.
        /// </para>
        /// </summary>
        [Parameter(ValueFromPipeline = true, Mandatory = true, ParameterSetName = ParameterSetNames.ByObject)]
        public Firewall InputObject { get; set; }

        protected override void ProcessRecord()
        {
            string project;
            string firewallName;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    project = Project;
                    firewallName = FirewallName;
                    break;
                case ParameterSetNames.ByObject:
                    project = GetProjectNameFromUri(InputObject.SelfLink);
                    firewallName = InputObject.Name;
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            if (ShouldProcess($"{project}/{firewallName}", "Remove Firewall"))
            {
                DeleteRequest request = Service.Firewalls.Delete(project, firewallName);
                WaitForGlobalOperation(project, request.Execute());
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Sets the data of a firewall rule.
    /// </para>
    /// <para type="description">
    /// Overwrites all data about a firewall rule.
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/firewalls#resource)">
    /// [Firewall resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GceFirewall")]
    [OutputType(typeof(Firewall))]
    public class SetGceFirewallCmdlet : GceConcurrentCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the project that owns the firewall rule to change.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The new firewall rule data.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public Firewall Firewall { get; set; }

        protected override void ProcessRecord()
        {
            Operation operation = Service.Firewalls.Update(Firewall, Project, Firewall.Name).Execute();
            AddGlobalOperation(Project, operation, () =>
            {
                WriteObject(Service.Firewalls.Get(Project, Firewall.Name));
            });
        }
    }
}
