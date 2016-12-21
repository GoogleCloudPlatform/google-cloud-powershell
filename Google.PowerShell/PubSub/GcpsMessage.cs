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
    /// To publish more than 1 message, use -Message parameter with an array of messages constructed from New-GcpsMessage.
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
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The topic to which the messages will be published.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
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
}
