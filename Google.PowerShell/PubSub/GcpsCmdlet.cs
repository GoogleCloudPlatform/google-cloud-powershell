// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Pubsub.v1;
using Google.PowerShell.Common;

namespace Google.PowerShell.PubSub
{
    /// <summary>
    /// Base class for Google Cloud PubSub cmdlets.
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
        protected string GetProjectPrefixForTopic(string topicName, string project)
        {
            if (!string.IsNullOrWhiteSpace(topicName) && !topicName.StartsWith($"projects/{project}/topics"))
            {
                topicName = $"projects/{project}/topics/{topicName}";
            }
            return topicName;
        }

        /// <summary>
        /// Prefix projects/{project name}/subscriptions/{subscriptions} to subscriptionName if not present.
        /// </summary>
        protected string GetProjectPrefixForSubscription(string subscriptionName, string project)
        {
            if (!string.IsNullOrWhiteSpace(subscriptionName) && !subscriptionName.StartsWith($"projects/{project}/subscriptions"))
            {
                subscriptionName = $"projects/{project}/subscriptions/{subscriptionName}";
            }
            return subscriptionName;
        }
    }
}
