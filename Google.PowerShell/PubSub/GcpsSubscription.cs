// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Pubsub.v1;
using Google.Apis.Pubsub.v1.Data;
using Google.PowerShell.Common;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net;

namespace Google.PowerShell.PubSub
{
    /// <summary>
    /// <para type="synopsis">
    /// Creates a new Google Cloud PubSub subscription.
    /// </para>
    /// <para type="description">
    /// Creates a new Google Cloud PubSub subscription. Will raise errors if the subscription already exist.
    /// The cmdlet will create the subscription in the default project if -Project is not used.
    /// Subscription created will default to pull mode if -PushEndPoint is not used.
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GcpsSubscription -Topic "my-topic" -Subscription "my-subscription"</code>
    ///   <para>
    ///   This command creates a new subscription called "my-subscription" that subscribes
    ///   to "my-topic" in the default project.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GcpsTopic -Topic "my-topic" -Subscription "my-subscription" -Project "my-project" -AckDeadline 30</code>
    ///   <para>
    ///   This command creates a new subscription called "my-subscription" that subscribes to "my-topic"
    ///   in the "my-project" project with an acknowledgement deadline of 30s.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GcpsTopic -Topic "my-topic" `
    ///                         -Subscription "my-subscription" `
    ///                         -PushEndpoint https://www.example.com/push `
    ///                         -PushEndpointAttributes @{"x-goog-version" = "v1beta"}
    ///   </code>
    ///   <para>
    ///   This command creates a new subscription called "my-subscription" that subscribes to "my-topic"
    ///   in the "my-project" project with a push endpoint at https://www.example.com/push and the attribute
    ///   "x-goog-version" of the endpoint set to "v1beta".
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/subscriber#overview-of-subscriptions)">
    /// [Subscription]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/reference/rest/v1/projects.subscriptions#PushConfig)">
    /// [Push Config]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/subscriber#ack_deadline)">
    /// [Ack Deadline]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcpsSubscription")]
    public class NewGcpsSubscription : GcpsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to create the subscription in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names of the subscription to be created. Subscription must not exist.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string Subscription { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names of the topic that the subscription belongs to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public string Topic { get; set; }

        /// <summary>
        /// <para type="description">
        /// The number of seconds after delivery, during which the subscriber must acknowledge the
        /// receipt of a pull or push message. By default, the deadline is 10 seconds.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public int? AckDeadline { get; set; }

        /// <summary>
        /// <para type="description">
        /// A URL locating the endpoint to which messages should be pushed.
        /// For example, a Webhook endpoint might use "https://example.com/push".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string PushEndpoint { get; set; }

        protected override void ProcessRecord()
        {
            Topic = GetProjectPrefixForTopic(Topic, Project);
            Subscription = GetProjectPrefixForSubscription(Subscription, Project);

            Subscription subscriptionBody = new Subscription()
            {
                Name = Subscription,
                Topic = Topic
            };

            if (AckDeadline.HasValue)
            {
                subscriptionBody.AckDeadlineSeconds = AckDeadline.Value;
            }

            if (PushEndpoint != null)
            {
                subscriptionBody.PushConfig = new PushConfig() { PushEndpoint = PushEndpoint };
            }

            ProjectsResource.SubscriptionsResource.CreateRequest request =
                Service.Projects.Subscriptions.Create(subscriptionBody, Subscription);
            try
            {
                Subscription response = request.Execute();
                WriteObject(response);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
            {
                WriteResourceExistsError(
                    exceptionMessage: $"Cannot create '{Subscription}' in project '{Project}' because it already exists.",
                    errorId: "SubscriptionAlreadyExists",
                    targetObject: Subscription);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                WriteResourceMissingError(
                    exceptionMessage: $"Topic '{Topic}' does not exist in project '{Project}'.",
                    errorId: "TopicNotFound",
                    targetObject: Topic);
            }
        }
    }
}
