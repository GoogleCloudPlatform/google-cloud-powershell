using Google.Apis.Pubsub.v1;
using Google.Apis.Pubsub.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;

namespace Google.PowerShell.PubSub
{
    [Cmdlet(VerbsCommon.New, "GcpsMessage")]
    public class NewGcpsMessage : GcpsCmdlet
    {
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Data { get; set; }

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
            string base64EncodedMessage = null;
            if (attributes != null && attributes.Count > 0)
            {
                attributesDict = ConvertToDictionary<string, string>(attributes);
            }
            if (!string.IsNullOrWhiteSpace(data))
            {
                base64EncodedMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
            }
            // A valid PubSub message must have either a non-empty message or a non-empty attributes.
            if (attributesDict == null && base64EncodedMessage == null)
            {
                throw new ArgumentException("Cannot construct a PubSub message because both the message and the attributes are empty.");
            }
            PubsubMessage psMessage = new PubsubMessage() { Data = base64EncodedMessage, Attributes = attributesDict };
            return psMessage;
        }
    }

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
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public string Topic { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DataAndAttributes)]
        [ValidateNotNullOrEmpty]
        public string Data { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DataAndAttributes)]
        [ValidateNotNullOrEmpty]
        public Hashtable Attributes { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.Message)]
        [ValidateNotNullOrEmpty]
        public PubsubMessage[] Message { get; set; }

        protected override void ProcessRecord()
        {
            Topic = GetProjectPrefixForTopic(Topic, Project);
            PublishRequest requestBody = null;
            if (ParameterSetName == ParameterSetNames.DataAndAttributes)
            {
                PubsubMessage psMessage = NewGcpsMessage.ConstructPubSubMessage(Data, Attributes);
                requestBody = new PublishRequest() { Messages = new List<PubsubMessage>() { psMessage } };
            }
            else
            {
                requestBody = new PublishRequest() { Messages = Message };
            }
            ProjectsResource.TopicsResource.PublishRequest publishRequest = Service.Projects.Topics.Publish(requestBody, Topic);
            PublishResponse response = publishRequest.Execute();

            WriteObject(response.MessageIds, true);
        }
    }
}
