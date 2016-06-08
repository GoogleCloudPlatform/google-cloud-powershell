using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using static Google.Apis.Compute.v1.FirewallsResource;

namespace Google.PowerShell.ComputeEngine
{
    [Cmdlet(VerbsCommon.Get, "GceFirewall")]
    public class GetFirewallCmdlet : GceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

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
                foreach (Firewall firewall in response.Items)
                {
                    yield return firewall;
                }
                pageToken = response.NextPageToken;
            } while (pageToken != null);
        }
    }

    [Cmdlet(VerbsCommon.Add, "GceFirewall")]
    public class AddFirewallCmdlet : GceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        public string Name { get; set; }

        [Parameter]
        public List<Firewall.AllowedData> Allowed { get; set; }

        [Parameter]
        public string Description { get; set; }

        [Parameter]
        public string Network { get; set; }

        [Parameter]
        public List<string> SourceRange { get; set; }

        [Parameter]
        public List<string> SourceTag { get; set; }

        [Parameter]
        public List<string> TargetTag { get; set; }

        protected override void ProcessRecord()
        {
            var firewall = new Firewall {
                Name = Name,
                Allowed = Allowed,
                Description = Description,
                Network = Network,
                SourceRanges = SourceRange,
                SourceTags = SourceTag,
                TargetTags = TargetTag
            };
            InsertRequest request = Service.Firewalls.Insert(firewall, Project);
            WaitForGlobalOperation(Project, request.Execute());
        }
    }

    [Cmdlet(VerbsCommon.Remove, "GceFirewall")]
    public class RemoveFirewallCmdlet : GceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Project))]
        public string Project { get; set; }

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
}
