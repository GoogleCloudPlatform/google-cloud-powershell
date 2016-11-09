using Google.Apis.Pubsub.v1;
using Google.PowerShell.Common;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Google.PowerShell.PubSub
{
    /// <summary>
    /// Base class for Google Cloud PubSub Cmdlets.
    /// </summary>
    public class GcpsCmdlet : GCloudCmdlet
    {
        public PubsubService Service { get; private set; }

        public GcpsCmdlet()
        {
            Service = new PubsubService(GetBaseClientServiceInitializer());
        }

        /// <summary>
        /// Prefix projects/{project name}/topics/{topic} to topicName if not present.
        /// </summary>
        protected string PrefixProjectToTopic(string topicName, string project)
        {
            if (string.Equals(topicName, "_deleted-topic_", System.StringComparison.OrdinalIgnoreCase))
            {
                return "_deleted-topic_";
            }
            if (!string.IsNullOrWhiteSpace(topicName) && !topicName.StartsWith($"projects/{project}/topics"))
            {
                topicName = $"projects/{project}/topics/{topicName}";
            }
            return topicName;
        }

        /// <summary>
        /// Prefix projects/{project name}/subscriptions/{subscriptions} to subscriptionName if not present.
        /// </summary>
        protected string PrefixProjectToSubscription(string subscriptionName, string project)
        {
            if (!string.IsNullOrWhiteSpace(subscriptionName) && !subscriptionName.StartsWith($"projects/{project}/subscriptions"))
            {
                subscriptionName = $"projects/{project}/subscriptions/{subscriptionName}";
            }
            return subscriptionName;
        }
    }
}
