using Google.Apis.Pubsub.v1;
using Google.Apis.Pubsub.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Text;

namespace Google.PowerShell.PubSub
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets a Google Cloud PubSub message from a pull config subscription.
    /// </para>
    /// <para type="description">
    /// Gets a Google Cloud PubSub message from a pull config subscription.
    /// Will raise errors if the subscription does not exist. The default project will be used to search
    /// for the subscription if -Project is not used. If -AutoAck switch is supplied, each message
    /// received will be acknowledged automatically.
    /// If there is more than one message for the subscription, the cmdlet may not get all of them in one call.
    /// By default, the cmdlet will block until at least one message is returned.
    /// If -ReturnImmediately is used, the cmdlet will not block.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcpsMessage -Subscription "my-subscription"</code>
    ///   <para>This command pulls down one or more messages from the subscription "my-subscription" in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcpsMessage -Subscription "my-subscription" -ReturnImmediately</code>
    ///   <para>
    ///   This command pulls down one or more messages from the subscription "my-subscription" in the default project
    ///   and it will not block even if no messages are returned.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcpsMessage -Subscription "my-subscription" -Project "my-project" -MaxMessage 10</code>
    ///   <para>
    ///   This command pulls down a maximum of 10 messages from the subscription "my-subscription" in the project "my-project".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcpsMessage -Subscription "my-subscription" -AutoAck</code>
    ///   <para>
    ///   This command pulls down one or more messages from the subscription "my-subscription" in the default project
    ///   and sends an acknowledgement for each message.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/reference/rest/v1/PubsubMessage)">
    /// [PubSub Message]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/subscriber#receiving-pull-messages)">
    /// [Receiving Pull Messages]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcpsMessage")]
    public class GetGcpsMessage : GcpsCmdlet
    {
        private const int DefaultMaxMessages = 100;

        /// <summary>
        /// <para type="description">
        /// The project to check for the subscription. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the subscription to pull the messages from.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Subscription), Property = nameof(Subscription.Name))]
        [Alias("Subscription")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The maximum number of messages that can be returned.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public int? MaxMessages { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, automatically send acknowledgement for each message received.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter AutoAck { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the cmdlet will not block when there are no messages.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter ReturnImmediately { get; set; }

        protected override void ProcessRecord()
        {
            Name = GetProjectPrefixForSubscription(Name, Project);
            PullRequest requestBody = new PullRequest();
            requestBody.ReturnImmediately = ReturnImmediately.IsPresent;
            requestBody.MaxMessages = MaxMessages.HasValue ? MaxMessages : DefaultMaxMessages;
            if (requestBody.MaxMessages <= 0)
            {
                throw new PSArgumentException("MaxMessages parameter should have a value greater than 0.");
            }

            // Send the pull request. Raise error if subscription is not found.
            ProjectsResource.SubscriptionsResource.PullRequest request = Service.Projects.Subscriptions.Pull(requestBody, Name);
            PullResponse response = null;
            try
            {
                response = request.Execute();
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                WriteResourceMissingError(
                    exceptionMessage: $"Subscription '{Name}' does not exist in project '{Project}'.",
                    errorId: "SubscriptionNotFound",
                    targetObject: Name);
                return;
            }

            IList<ReceivedMessage> receivedMessages = response?.ReceivedMessages;
            if (receivedMessages == null || receivedMessages.Count == 0)
            {
                return;
            }

            // Send acknowledgement for all the messages if -AutoAck is present.
            if (AutoAck.IsPresent)
            {
                AcknowledgeRequest ackRequestBody = new AcknowledgeRequest()
                {
                    AckIds = receivedMessages.Select(message => message.AckId).ToList()
                };
                ProjectsResource.SubscriptionsResource.AcknowledgeRequest ackRequest =
                    Service.Projects.Subscriptions.Acknowledge(ackRequestBody, Name);
                ackRequest.Execute();
            }

            foreach (ReceivedMessage receivedMessage in receivedMessages)
            {
                PubSubMessageWithAckIdAndSubscription messageWithAck = new PubSubMessageWithAckIdAndSubscription(receivedMessage, Name);
                // Convert the base 64 encoded message data.
                if (!string.IsNullOrWhiteSpace(messageWithAck.Data))
                {
                    byte[] base64Bytes = Convert.FromBase64String(messageWithAck.Data);
                    messageWithAck.Data = Encoding.UTF8.GetString(base64Bytes);
                }
                if (AutoAck.IsPresent)
                {
                    // Remove the AckId 
                    messageWithAck.AckId = null;
                }
                WriteObject(messageWithAck);
            }
        }
    }

    /// <summary>
    /// Class that extends PubSub Message by adding AckId and Subscription fields.
    /// We added these fields to the PubSub Message to allow pipelining the results
    /// from Get-GcpsMessage to other cmdlets.
    /// </summary>
    public class PubSubMessageWithAckIdAndSubscription : PubsubMessage
    {
        public PubSubMessageWithAckIdAndSubscription() : base() { }

        /// <summary>
        /// Given a received message (returned from a pull request from a subscription)
        /// and a subscription, construct a PubSub message with AckId and subscription.
        /// </summary>
        public PubSubMessageWithAckIdAndSubscription(ReceivedMessage receivedMessage, string subscription)
        {
            Subscription = subscription;
            AckId = receivedMessage.AckId;
            if (receivedMessage.Message != null)
            {
                Attributes = receivedMessage.Message.Attributes;
                Data = receivedMessage.Message.Data;
                ETag = receivedMessage.Message.ETag;
                MessageId = receivedMessage.Message.MessageId;
                PublishTime = receivedMessage.Message.PublishTime;
            }
        }

        /// <summary>
        /// The AckId of the message.
        /// </summary>
        public string AckId { get; set; }

        /// <summary>
        /// The Subscription that this message belongs to.
        /// </summary>
        public string Subscription { get; set; }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a Google Cloud PubSub message.
    /// </para>
    /// <para type="description">
    /// Creates a Google Cloud PubSub message. The message created is intended to be used with
    /// Publish-GcpsMessage cmdlet to publish it to a topic. The message payload must not be empty;
    /// it must contain either a non-empty data field or at least one attribute.
    /// The cmdlet will base64-encode the message data.
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GcpsMessage -Data "my-data"</code>
    ///   <para>This command creates a new message with data "my-data".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GcpsMessage -Data "my-data" -Attributes @{"key"="value"}</code>
    ///   <para>
    ///   This command creates a new message with data "my-data" and an attribute pair "key", "value".
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/reference/rest/v1/PubsubMessage)">
    /// [PubSub Message]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcpsMessage")]
    public class NewGcpsMessage : GcpsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The message payload. This will be base64-encoded by the cmdlet.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Data { get; set; }

        /// <summary>
        /// <para type="description">
        /// Optional attributes for this message.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public Hashtable Attributes { get; set; }

        protected override void ProcessRecord()
        {
            PubsubMessage psMessage = ConstructPubSubMessage(Data, Attributes);
            WriteObject(psMessage);
        }

        internal static PubsubMessage ConstructPubSubMessage(string data, Hashtable attributes)
        {
            Dictionary<string, string> attributesDict = null;
            if (attributes != null && attributes.Count > 0)
            {
                attributesDict = ConvertToDictionary<string, string>(attributes);
            }
            // A valid PubSub message must have either a non-empty message or a non-empty attributes.
            if (attributesDict == null && string.IsNullOrWhiteSpace(data))
            {
                throw new ArgumentException("Cannot construct a PubSub message because both the message data and the attributes are empty.");
            }
            PubsubMessage psMessage = new PubsubMessage() { Data = data, Attributes = attributesDict };
            return psMessage;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Publishes one or more PubSub messages to a topic.
    /// </para>
    /// <para type="description">
    /// Publishes one or more PubSub messages to a topic. Will raise errors if the topic does not exist.
    /// The cmdlet will search for the topic in the default project if -Project is not used.
    /// To publish more than one message, use -Message parameter with an array of messages constructed from New-GcpsMessage.
    /// Otherwise, use -Data and -Attribute parameters to publish a single message.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Publish-GcpsTopic -Topic "my-topic" -Data "This is a test." -Attributes @{"key"="value"}</code>
    ///   <para>
    ///   This command publishes a message with the specified data and attribute to topic "my-topic" in the default project.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> $message1 = New-GcpsMessage -Data "my-data"
    ///   PS C:\> $message2 = New-GcpsMessage -Data "my-data2" -Attributes @{"key"="test"}
    ///   PS C:\> Publish-GcpsTopic -Topic "my-topic" -Message $message1, $message2 -Project "my-project"
    ///   </code>
    ///   <para>
    ///   This command publishes 2 messages to topic "my-topic" in the project "my-project".
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/publisher#publish-messages-to-a-topic)">
    /// [Publishing Messages to a Topic]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsData.Publish, "GcpsMessage", DefaultParameterSetName = ParameterSetNames.DataAndAttributes)]
    public class PublishGcpsMessage : GcpsCmdlet
    {
        private class ParameterSetNames
        {
            public const string DataAndAttributes = "DataAndAttributes";
            public const string Message = "Message";
        }

        /// <summary>
        /// <para type="description">
        /// The project to check for topic. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The topic to which the messages will be published.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Topic), Property = nameof(Apis.Pubsub.v1.Data.Topic.Name))]
        [ValidateNotNullOrEmpty]
        public string Topic { get; set; }

        /// <summary>
        /// <para type="description">
        /// The data message that will be published.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DataAndAttributes)]
        [ValidateNotNullOrEmpty]
        public string Data { get; set; }

        /// <summary>
        /// <para type="description">
        /// Attributes of the message that will be published.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DataAndAttributes)]
        [ValidateNotNullOrEmpty]
        public Hashtable Attributes { get; set; }

        /// <summary>
        /// <para type="description">
        /// Messages to be published. Use this parameter to publish one or more messages.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.Message)]
        [ValidateNotNullOrEmpty]
        public PubsubMessage[] Message { get; set; }

        protected override void ProcessRecord()
        {
            Topic = GetProjectPrefixForTopic(Topic, Project);
            if (ParameterSetName == ParameterSetNames.DataAndAttributes)
            {
                PubsubMessage psMessage = NewGcpsMessage.ConstructPubSubMessage(Data, Attributes);
                Message = new PubsubMessage[] { psMessage };
            }

            // Encode data in each message to base64.
            foreach (PubsubMessage psMessage in Message)
            {
                if (!string.IsNullOrWhiteSpace(psMessage.Data))
                {
                    psMessage.Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(psMessage.Data));
                }
            }

            PublishRequest requestBody = new PublishRequest() { Messages = Message };
            ProjectsResource.TopicsResource.PublishRequest publishRequest = Service.Projects.Topics.Publish(requestBody, Topic);
            try
            {
                PublishResponse response = publishRequest.Execute();

                // The MessageIds is a server-assigned ID of the published message.
                // They are in the same order as the messages in the request.
                // We will use them to populate each message in the request with the corresponding message id.
                IList<string> returnedMessageIds = response.MessageIds;
                if (returnedMessageIds.Count != requestBody.Messages.Count)
                {
                    throw new InvalidOperationException($"Only {returnedMessageIds.Count} out of {requestBody.Messages.Count} published.");
                }

                for (int index = 0; index < returnedMessageIds.Count; index += 1)
                {
                    PubsubMessage publishedMessage = requestBody.Messages[index];
                    publishedMessage.MessageId = returnedMessageIds[index];
                    if (!string.IsNullOrWhiteSpace(publishedMessage.Data))
                    {
                        byte[] decodedMessageDataBytes = Convert.FromBase64String(publishedMessage.Data);
                        publishedMessage.Data = Encoding.UTF8.GetString(decodedMessageDataBytes);
                    }
                    WriteObject(publishedMessage);
                }
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

    // Cmdlet class that perform action when given a list of messages pull from subscriptions.
    // Derived classes will implement PerformActionOnMessages.
    public class ProcessGcpsAck : GcpsCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByObject = "ByObject";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// The project that the subscription belongs to. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the subscription that the messages are pulled from.
        /// This parameter is used with -AckId parameter.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByName)]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Subscription), Property = nameof(Apis.Pubsub.v1.Data.Subscription.Name))]
        [Alias("Name")]
        [ValidateNotNullOrEmpty]
        public string Subscription { get; set; }

        /// <summary>
        /// <para type="description">
        /// The list of AckIds of the pulled messages from the provided subscription.
        /// This parameter is used with -Name parameter.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = ParameterSetNames.ByName)]
        [ValidateNotNullOrEmpty]
        public string[] AckId { get; set; }

        /// <summary>
        /// <para type="description">
        /// The list of PubSub messages that the cmdlet will send acknowledgement for.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ByObject)]
        [ValidateNotNullOrEmpty]
        public PubSubMessageWithAckIdAndSubscription[] InputObject { get; set; }

        protected override void ProcessRecord()
        {
            if (ParameterSetName == ParameterSetNames.ByName)
            {
                Subscription = GetProjectPrefixForSubscription(Subscription, Project);
                PerformAction(AckId.ToList(), Subscription);
            }

            if (ParameterSetName == ParameterSetNames.ByObject)
            {
                // We group the message with the subscription name as key and Ack IDs as values and send 1 request per subscription.
                IEnumerable<IGrouping<string, string>> messageGroups =
                    InputObject.GroupBy(message => message.Subscription, message => message.AckId);
                foreach (IGrouping<string, string> messageGroup in messageGroups)
                {
                    PerformAction(messageGroup.ToList(), messageGroup.Key);
                }
            }
        }

        protected virtual void PerformAction(IList<string> ackIds, string subscriptionName)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Sends acknowledgement for one or more PubSub messages.
    /// </para>
    /// <para type="description">
    /// Sends acknowledgement for one or more PubSub messages. Will raise errors if the subscription that the messages are pulled from
    /// does not exist. The cmdlet will search for the subscription and the messages in the default project if -Project is not used.
    /// To send acknowledgement for messages from a single subscription, use -Subscription to provide the name of the subscription
    /// and -AckId to provide a list of Ack Ids for that subscription. To send acknowledgement for messages objects returned by
    /// Get-GcpsMessage cmdlet, use the -InputObject parameter.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Send-GcpsAck -Subscription "my-subscription" -AckId "ackId"</code>
    ///   <para>
    ///   This command sends acknowledgement for message with Ack Id "ackId" from subscription "my-subscription" in the default project.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Send-GcpsAck -Subscription "my-subscription" -AckId "ackId1", "ackId2" -Project "my-project"</code>
    ///   <para>
    ///   This command sends acknowledgement for messages with Ack Ids "ackId1" and "ackId2" from subscription"my-subscription"
    ///   in the project "my-project".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> $messages = Get-GcpsMessage -Subscription "my-subscription"
    ///   PS C:\> Send-GcpsAck -InputObject $messages
    ///   </code>
    ///   <para>
    ///   This command sends acknowledgement for messages pulled from subscription "my-subscription"
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/subscriber#receiving-pull-messages)">
    /// [Receiving and Sending Acknowledge for Pull Messages]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommunications.Send, "GcpsAck")]
    public class SendGcpsAck : ProcessGcpsAck
    {
        protected override void PerformAction(IList<string> ackIds, string subscriptionName)
        {
            AcknowledgeRequest requestBody = new AcknowledgeRequest() { AckIds = ackIds };
            try
            {
                ProjectsResource.SubscriptionsResource.AcknowledgeRequest request =
                    Service.Projects.Subscriptions.Acknowledge(requestBody, subscriptionName);
                request.Execute();
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                WriteResourceMissingError(
                    exceptionMessage: $"Subscription '{subscriptionName}' does not exist in project '{Project}'.",
                    errorId: "SubscriptionNotFound",
                    targetObject: subscriptionName);
                return;
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Setting acknowledgement deadline in seconds for one or more PubSub messages.
    /// </para>
    /// <para type="description">
    /// Setting acknowledgement deadline in seconds for one or more PubSub messages.
    /// Will raise errors if the subscription that the messages are pulled from does not exist.
    /// The cmdlet will search for the subscription and the messages in the default project if -Project is not used.
    /// To set the acknowledgement deadline for messages from a single subscription, use -Subscription to provide the name of the subscription
    /// and -AckId to provide a list of Ack Ids for that subscription. To send acknowledgement for messages objects returned by
    /// Get-GcpsMessage cmdlet, use the -InputObject parameter.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Set-GcpsAckDeadline -Subscription "my-subscription" -AckId "ackId" -AckDeadline 10</code>
    ///   <para>
    ///   This command sets the acknowledgement deadline for message with Ack Id "ackId" from subscription "my-subscription"
    ///   in the default project to 10s.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///     PS C:\> Set-GcpsAckDeadline -Subscription "my-subscription" `
    ///                 -AckId "ackId1", "ackId2" -Project "my-project" -AckDeadline 10
    ///   </code>
    ///   <para>
    ///   This command sets the acknowledgement deadline for messages with Ack Ids "ackId1" and "ackId2" from subscription
    ///   "my-subscription" in the project "my-project" to 10s.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> $messages = Get-GcpsMessage -Subscription "my-subscription"
    ///   PS C:\> Set-GcpsAckDeadline -InputObject $messages -AckDeadline 10
    ///   </code>
    ///   <para>
    ///   This command sets the acknowledgement deadline for messages pulled from subscription "my-subscription" to 10s.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/subscriber#ack_deadline)">
    /// [Pull Messages Acknowledgement Deadline]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GcpsAckDeadline")]
    public class SetGcpsAckDeadline : ProcessGcpsAck
    {
        /// <summary>
        /// <para type="description">
        /// The ack deadline to be set (in seconds).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        public int AckDeadline { get; set; }

        protected override void PerformAction(IList<string> ackIds, string subscriptionName)
        {
            ModifyAckDeadlineRequest requestBody = new ModifyAckDeadlineRequest()
            {
                AckDeadlineSeconds = AckDeadline,
                AckIds = ackIds
            };

            try
            {
                ProjectsResource.SubscriptionsResource.ModifyAckDeadlineRequest request =
                    Service.Projects.Subscriptions.ModifyAckDeadline(requestBody, subscriptionName);
                request.Execute();
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                WriteResourceMissingError(
                    exceptionMessage: $"Subscription '{subscriptionName}' does not exist in project '{Project}'.",
                    errorId: "SubscriptionNotFound",
                    targetObject: subscriptionName);
                return;
            }
        }
    }
}
