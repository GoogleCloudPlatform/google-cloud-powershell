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
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the subscription to be created. Subscription must not exist.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string Subscription { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the topic that the subscription belongs to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Topic), Property = nameof(Apis.Pubsub.v1.Data.Topic.Name))]
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

    /// <summary>
    /// <para type="synopsis">
    /// Retrieves one or more Google Cloud PubSub subscriptions.
    /// </para>
    /// <para type="description">
    /// Retrieves one or more Google Cloud PubSub subscriptions. The cmdlet will search for subscriptions
    /// in the default project if -Project is not used. If -Topic is used, the cmdlet will only return
    /// subscriptions belonging to the specified topic. If -Subscription is used, the cmdlet will only return
    /// subscriptions whose names match the subscriptions' names provided.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcpsSubscription</code>
    ///   <para> This command retrieves all subscriptions in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcpsSubscription -Topic "my-topic" -Project "my-project"</code>
    ///   <para> This command retrieves all subscriptions that belong to topic "my-topic" in the project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcpsSubscription -Subscription "subscription1", "subscription2"</code>
    ///   <para> This command retrieves subscriptions "subscription1" and "subscription2" in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcpsSubscription -Subscription "subscription1", "subscription2" -Topic "my-topic"</code>
    ///   <para> This command retrieves subscriptions "subscription1" and "subscription2" in the topic "my-topic".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/subscriber#overview-of-subscriptions)">
    /// [Subscription]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcpsSubscription")]
    public class GetGcpsSubscription : GcpsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for subscriptions. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names of the subscriptions to be retrieved.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0)]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string[] Subscription { get; set; }

        /// <summary>
        /// <para type="description">
        /// The topic to check for subscriptions.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Topic), Property = nameof(Apis.Pubsub.v1.Data.Topic.Name))]
        [ValidateNotNullOrEmpty]
        public string Topic { get; set; }

        protected override void ProcessRecord()
        {
            if (Subscription != null)
            {
                Subscription = Subscription.Select(item => GetProjectPrefixForSubscription(item, Project)).ToArray();
            }

            // Handles the case where user wants to list all subscriptions in a particular topic.
            // In this case, we will have to make a call to get the name of all the subscriptions in that topic
            // before calling get request on each subscription.
            if (Topic != null)
            {
                Topic = GetProjectPrefixForTopic(Topic, Project);
                ProjectsResource.TopicsResource.SubscriptionsResource.ListRequest listRequest =
                    Service.Projects.Topics.Subscriptions.List(Topic);
                do
                {
                    ListTopicSubscriptionsResponse response = null;
                    try
                    {
                        response = listRequest.Execute();
                    }
                    catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                    {
                        WriteResourceMissingError(
                            exceptionMessage: $"Topic '{Topic}' does not exist in project '{Project}'.",
                            errorId: "TopicNotFound",
                            targetObject: Topic);
                    }

                    if (response?.Subscriptions != null)
                    {
                        // If user gives us a list of subscriptions to search for, we have to make sure that
                        // those subscriptions belong to the topic.
                        if (Subscription != null)
                        {
                            IEnumerable<string> selectedSubscriptions = response.Subscriptions
                                .Where(sub => Subscription.Contains(sub, System.StringComparer.OrdinalIgnoreCase));
                            GetSubscriptions(selectedSubscriptions);
                        }
                        else
                        {
                            GetSubscriptions(response.Subscriptions);
                        }
                    }
                    listRequest.PageToken = response.NextPageToken;
                }
                while (!Stopping && listRequest.PageToken != null);
                return;
            }

            // If no topic is given, then we are left with 2 cases:
            // 1. User gives us a list of subscriptions: we just find those subscriptions and returned.
            // 2. User does not give us any subscriptions: we just return all subscriptions in the project.
            if (Subscription != null && Subscription.Length > 0)
            {
                GetSubscriptions(Subscription.Select(item => GetProjectPrefixForSubscription(item, Project)));
            }
            else
            {
                ProjectsResource.SubscriptionsResource.ListRequest listRequest =
                    Service.Projects.Subscriptions.List($"projects/{Project}");
                do
                {
                    ListSubscriptionsResponse response = listRequest.Execute();

                    if (response.Subscriptions != null)
                    {
                        WriteObject(response.Subscriptions, true);
                    }
                    listRequest.PageToken = response.NextPageToken;
                }
                while (!Stopping && listRequest.PageToken != null);
            }
        }

        /// <summary>
        /// Given a list of subscription names, writes the corresponding subscriptions.
        /// </summary>
        private void GetSubscriptions(IEnumerable<string> subscriptionNames)
        {
            foreach (string subscriptionName in subscriptionNames)
            {
                try
                {
                    ProjectsResource.SubscriptionsResource.GetRequest getRequest = Service.Projects.Subscriptions.Get(subscriptionName);
                    Subscription subscription = getRequest.Execute();
                    WriteObject(subscription);
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    WriteResourceMissingError(
                        exceptionMessage: $"Subscription '{subscriptionName}' does not exist in project '{Project}'.",
                        errorId: "SubscriptionNotFound",
                        targetObject: subscriptionName);
                }
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Changes the config of a subscription.
    /// </para>
    /// <para type="description">
    /// Changes the config of a subscription from push to pull and vice versa. The cmdlet can also be used to
    /// change the endpoint of a push subscription. Will raise error if the subscription cannot be found.
    /// No errors will be raised if a subscription with a pull config is set to pull config again or if a subscription
    /// with push config is set to the same endpoint.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Set-GcpsSubscriptionConfig -Subscription "my-subscription" -PullConfig</code>
    ///   <para> This command sets the config of subscription "my-subscription" in the default project to pull config.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcpsSubscription -Topic "my-topic" | Set-GcpsSubscriptionConfig -PullConfig</code>
    ///   <para> This command sets the config of all subscriptions of topic "my-topic" to pull config by pipelining.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GcpsSubscriptionConfig -Subscription "my-subscription" -PushEndpoint https://www.example.com -Project "my-project"
    ///   </code>
    ///   <para>
    ///   This command sets the config of subscription "my-subscription" in the project "my-project" to
    ///   a push config with endpoint https://www.example.com.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/subscriber#overview-of-subscriptions)">
    /// [Subscription]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/reference/rest/v1/projects.subscriptions#PushConfig)">
    /// [Push Config]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GcpsSubscriptionConfig")]
    public class SetGcpsSubscriptionConfig : GcpsCmdlet
    {
        private class ParameterSetNames
        {
            public const string PushConfig = "PushConfig";
            public const string PullConfig = "PullConfig";
        }

        /// <summary>
        /// <para type="description">
        /// The project that the config's subscription belongs to. If not set via PowerShell parameter processing,
        /// will default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the subscription that the config belongs to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [Alias("Name")]
        [ValidateNotNullOrEmpty]
        [PropertyByTypeTransformation(Property = nameof(Apis.Pubsub.v1.Data.Subscription.Name), TypeToTransform = typeof(Subscription))]
        public string Subscription { get; set; }

        /// <summary>
        /// <para type="description">
        /// A URL locating the endpoint to which messages should be pushed.
        /// For example, a Webhook endpoint might use "https://example.com/push".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.PushConfig)]
        [ValidateNotNullOrEmpty]
        public string PushEndpoint { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the cmdlet will change config of the subscription to a pull config.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.PullConfig)]
        public SwitchParameter PullConfig { get; set; }

        protected override void ProcessRecord()
        {
            Subscription = GetProjectPrefixForSubscription(Subscription, Project);

            ModifyPushConfigRequest requestBody = new ModifyPushConfigRequest();

            if (!PullConfig.IsPresent)
            {
                PushConfig pushConfig = new PushConfig() { PushEndpoint = PushEndpoint };
                requestBody.PushConfig = pushConfig;
            }
            else
            {
                // Setting this to null will change a push config to a pull config.
                requestBody.PushConfig = null;
            }

            try
            {
                ProjectsResource.SubscriptionsResource.ModifyPushConfigRequest request =
                    Service.Projects.Subscriptions.ModifyPushConfig(requestBody, Subscription);
                request.Execute();
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                WriteResourceMissingError(
                    exceptionMessage: $"Subscription '{Subscription}' does not exist in project '{Project}'.",
                    errorId: "SubscriptionNotFound",
                    targetObject: Subscription);
            }
        }
    }

    /// <summary>
    /// Removes Google Cloud PubSub subscriptions.
    /// <para type="description">
    /// Removes one or more Gooogle Cloud PubSub subscriptions. Will raise errors if the subscriptions do not exist.
    /// The cmdlet will delete the subscriptions in the default project if -Project is not used.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcpsSubscription -Subscription "my-subscription"</code>
    ///   <para>This command removes subscription "my-subscription" in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcpsTopic -Subscription "subscription1", "subscription2" -Project "my-project"</code>
    ///   <para>
    ///   This command removes 2 topics ("subscription1" and "subscription1") in the project "my-project".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcpsSubscription -Topic "my-topic" | Remove-GcpsSubscription</code>
    ///   <para>
    ///   This command removes all subscriptions to topic "my-topic" by pipelining from Get-GcpsSubscription.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/subscriber#delete)">
    /// [Deleting a Subscription]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcpsSubscription", SupportsShouldProcess = true)]
    public class RemoveGcpsSubscription : GcpsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for subscriptions. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names of the subscriptions to be removed. Subscriptions must exist.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0, ValueFromPipelineByPropertyName = true)]
        [ArrayPropertyTransform(typeof(Subscription), nameof(Apis.Pubsub.v1.Data.Subscription.Name))]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string[] Subscription { get; set; }

        protected override void ProcessRecord()
        {
            foreach (string subscriptionName in Subscription)
            {
                string formattedSubscription = GetProjectPrefixForSubscription(subscriptionName, Project);
                try
                {
                    if (ShouldProcess(formattedSubscription, "Remove Subscription"))
                    {
                        ProjectsResource.SubscriptionsResource.DeleteRequest request = Service.Projects.Subscriptions.Delete(formattedSubscription);
                        request.Execute();
                    }
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    WriteResourceMissingError(
                        exceptionMessage: $"Subscription '{subscriptionName}' does not exist in project '{Project}'.",
                        errorId: "SubscriptionNotFound",
                        targetObject: subscriptionName);
                }
            }
        }
    }
}
