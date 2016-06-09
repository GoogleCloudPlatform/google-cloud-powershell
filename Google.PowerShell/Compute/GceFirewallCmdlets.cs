using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
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
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceFirewall")]
    public class GetFirewallCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The Project to get the firewall rule of.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the firewall rule to get.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, ValueFromPipeline = true)]
        [Alias("Name", "Firewall")]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Firewall))]
        public string FirewallName { get; set; }

        protected override void ProcessRecord()
        {
            IEnumerable<Firewall> output;
            if (FirewallName == null)
            {
                output = GetFirewallList();
            }
            else
            {
                output = new Firewall[] { Service.Firewalls.Get(Project, FirewallName).Execute() };
            }

            foreach(Firewall firewall in output)
            {
                WriteObject(firewall);
            }
        }

        private IEnumerable<Firewall> GetFirewallList()
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
    /// Creates a new Google.Apis.Compute.v1.Data.Firewall.AllowedData object.
    /// </para>
    /// <para type="description">
    /// Creates a new Google.Apis.Compute.v1.Data.Firewall.AllowedData object. The result of this cmdlet can be
    /// used by the Allowed parameter of the New-GceFirewall cmdlet.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GceFirewallAllowed", DefaultParameterSetName = ParameterSetNames.Default)]
    public class NewFirewallAllowedCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string Default = "Default";
            public const string AppendList = "AppendList";
            public const string AppendPipeline = "AppendPipeline";
        }

        /// <summary>
        /// <para type="description">
        /// The IP protocol that is allowed for this rule. This value can either be one of the following
        /// well known protocol strings (tcp, udp, icmp, esp, ah, sctp), or the IP protocol number.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [Alias("Protocol")]
        public string IPProtocol { get; set; }

        /// <summary>
        /// <para type="description">
        /// The ports which are allowed. This parameter is only applicable for UDP or TCP protocol.
        /// Each entry must be either an integer or a range. If not specified, connections through any port are
        /// allowed Example inputs include: "22", "80","443", and "12345-12349".
        /// </para>
        /// </summary>
        [Parameter]
        public List<string> Port { get; set; }

        /// <summary>
        /// <para type="description">
        /// The mutable IList to append the new AllowedData to
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AppendList, Mandatory = true)]
        public IList AppendTo { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Pipeline to append the new AllowedData to.
        /// </para>
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetNames.AppendPipeline)]
        public object Pipeline;

        /// <summary>
        /// Actually create the new object and append it to either the pipeline or the given IList.
        /// </summary>
        protected override void EndProcessing()
        {
            var newData = new Firewall.AllowedData
            {
                IPProtocol = IPProtocol,
                Ports = Port
            };

            switch (ParameterSetName)
            {
                case ParameterSetNames.AppendList:
                    AppendTo.Add(newData);
                    break;
                case ParameterSetNames.AppendPipeline:
                case ParameterSetNames.Default:
                    WriteObject(newData);
                    break;
                default:
                    throw new InvalidOperationException($"{ParameterSetName} is not a valid parameter set.");
            }
        }

        /// <summary>
        /// If appending to a pipeline, pass pipeline objects along.
        /// </summary>
        protected override void ProcessRecord()
        {
            if (ParameterSetName == ParameterSetNames.AppendPipeline)
            {
                WriteObject(Pipeline);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Adds a new firewall rule.
    /// </para>
    /// <para type="description">
    /// Adds a new firewall rule. When given a pipeline of many Firewall.AllowedData, will collect them all and
    /// create a single new firewall.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceFirewall")]
    public class AddFirewallCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the project to add the firewall to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the new firewall
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// A list of allowed protocols and ports.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        public List<Firewall.AllowedData> Allowed { get; set; }

        /// <summary>
        /// <para type="description">
        /// The description of this firewall
        /// </para>
        /// </summary>
        [Parameter]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// Url of the network resource for this firewall rule.
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
        /// as the firewall.
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

        private List<Firewall.AllowedData> AllAllowed { get; set; } = new List<Firewall.AllowedData>();

        /// <summary>
        /// Collect allowed from the pipeline.
        /// </summary>
        protected override void ProcessRecord()
        {
            AllAllowed.AddRange(Allowed);
        }

        /// <summary>
        /// Create the firewall.
        /// </summary>
        protected override void EndProcessing()
        {
            var firewall = new Firewall
            {
                Name = Name,
                Allowed = AllAllowed,
                Description = Description,
                Network = Network,
                SourceRanges = SourceRange,
                SourceTags = SourceTag,
                TargetTags = TargetTag
            };
            InsertRequest request = Service.Firewalls.Insert(firewall, Project);
            WaitForGlobalOperation(Project, request.Execute());
            WriteObject(Service.Firewalls.Get(Project, Name));
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes a firewall from a project.
    /// </para>
    /// <para type="description">
    /// Removes a firewall from a project.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceFirewall")]
    public class RemoveFirewallCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the project from which to remove the firewall.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the firewall to remove.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, ValueFromPipeline = true)]
        [Alias("Name", "Firewall")]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Firewall))]
        public string FirewallName { get; set; }

        protected override void ProcessRecord()
        {
            DeleteRequest request = Service.Firewalls.Delete(Project, FirewallName);
            WaitForGlobalOperation(Project, request.Execute());
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Sets the data of a firewall.
    /// </para>
    /// <para type="description">
    /// Overwrites all data about a firewall.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GceFirewall")]
    public class SetFirewallCmdlet : GceCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the project that owns the firewall to change.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The new firewall data.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public Firewall Firewall { get; set; }

        protected override void ProcessRecord()
        {
            Operation operation = Service.Firewalls.Update(Firewall, Project, Firewall.Name).Execute();
            WaitForGlobalOperation(Project, operation);

        }
    }
}
