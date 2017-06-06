// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Creates a new object that tells a firewall to allow a protocol.
    /// </para>
    /// <para type="description">
    /// Creates a new AllowedData object which can be passed through the pipeline too the Allowed parameter of
    /// the Add-GceFirewall cmdlet.
    /// </para>
    /// <example>
    /// <code>
    /// New-GceFirewallProtocol tcp -Port 80, 443 |
    ///     New-GceFirewallProtocol esp |
    ///     Add-GceFirewall -Project "your-project" -Name "firewall-name"
    /// </code>
    /// <para>Creates two GceFirewallProtocol objects, and sends them to the Add-GceFirewall cmdlet.</para>
    /// </example>
    /// <example>
    /// <code>
    /// New-GceFirewallProtocol tcp -Port 80..443 |
    ///     Add-GceFirewall -Project "your-project" -Name "firewall-name"
    /// </code>
    /// <para>
    /// Creates a GceFirewallProtocol object with port range 80 to 443, and sends them to
    /// the Add-GceFirewall cmdlet.
    /// </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/firewalls#resource)">
    /// [Firewall resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GceFirewallProtocol", DefaultParameterSetName = ParameterSetNames.Default)]
    [OutputType(typeof(Firewall.AllowedData))]
    public class NewFirewallProtocolCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string Default = "Default";
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
        /// allowed. Example inputs include: "22", "80","443", "12345-12349" and "80..443".
        /// </para>
        /// </summary>
        [Parameter]
        public List<string> Port { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Pipeline to append the new AllowedData to.
        /// </para>
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetNames.AppendPipeline)]
        public object Pipeline { get; set; }

        /// <summary>
        /// Actually create the new object and append it to either the pipeline or the given IList.
        /// </summary>
        protected override void EndProcessing()
        {
            if (Port != null)
            {
                Port = Port.Select(portString => portString.Replace("..", "-")).ToList();
            }

            var newData = new Firewall.AllowedData
            {
                IPProtocol = IPProtocol,
                Ports = Port
            };

            WriteObject(newData);
            base.EndProcessing();
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
}
