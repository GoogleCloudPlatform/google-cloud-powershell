using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Google.PowerShell.Compute.Firewalls
{
    [Cmdlet(VerbsCommon.Get, "GceFirewall", DefaultParameterSetName = "FilteredList")]
    public class GetGceFirewallCmdlet : GceCmdlet
    {
        private const string FilteredList = "FilteredList";
        private const string NamedGet = "NamedGet";

        [Parameter(Position = 0, Mandatory = true)]
        public string Project { get; set; }

        [Parameter(ParameterSetName = FilteredList)]
        public string Filter { get; set; }

        [Parameter(Position = 1, ParameterSetName = NamedGet, ValueFromPipeline = true)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            if (ParameterSetName == FilteredList)
            {
                FirewallsResource.ListRequest request = Service.Firewalls.List(Project);
                request.Filter = Filter;
                FirewallList list = request.Execute();
                foreach(Firewall firewall in list.Items)
                {
                    WriteObject(firewall);
                }
                while (list.NextPageToken != null)
                {
                    request = Service.Firewalls.List(Project);
                    request.Filter = Filter;
                    request.PageToken = list.NextPageToken;
                    list = request.Execute();
                    foreach (Firewall firewall in list.Items)
                    {
                        WriteObject(firewall);
                    }

                }
            }
            else if (ParameterSetName == NamedGet)
            {
                var request = Service.Firewalls.Get(Project, Name);
                var response = request.Execute();
                WriteObject(response);
            }
            else
            {
                throw new InvalidOperationException($"{ParameterSetName} is not a valid parameter set.");
            }
        }
    }
}
