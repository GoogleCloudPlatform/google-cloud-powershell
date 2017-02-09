// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Pubsub.v1;
using Google.Apis.Pubsub.v1.Data;
using Google.PowerShell.Common;
using System.Management.Automation;
using System.Net;

namespace Google.PowerShell.PubSub
{
    /// <summary>
    /// <para type="synopsis">
    /// Creates new Google Cloud PubSub topics.
    /// </para>
    /// <para type="description">
    /// Creates one or more Gooogle Cloud PubSub topics. Will raise errors if the topics already exist.
    /// The cmdlet will create the topics in the default project if -Project is not used.
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GcpsTopic -Topic "my-topic"</code>
    ///   <para>This command creates a new topic called "my-topic" in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GcpsTopic -Topic "topic1", "topic2" -Project "my-project"</code>
    ///   <para>
    ///   This command creates 2 topics ("topic1" and "topic2") in the project "my-project".
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/publisher#create-a-topic)">
    /// [Creating a Topic]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcpsTopic")]
    public class NewGcpsTopic : GcpsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to create the topics in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names of the topics to be created. Topics must not exist.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("Name")]
        [ValidateNotNullOrEmpty]
        public string[] Topic { get; set; }

        protected override void ProcessRecord()
        {
            foreach (string topicName in Topic)
            {
                string formattedTopicname = GetProjectPrefixForTopic(topicName, Project);
                Topic topic = new Topic() { Name = formattedTopicname };
                ProjectsResource.TopicsResource.CreateRequest request = Service.Projects.Topics.Create(topic, formattedTopicname);
                try
                {
                    Topic response = request.Execute();
                    WriteObject(response);
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
                {
                    WriteResourceExistsError(
                        exceptionMessage: $"Cannot create topic '{topicName}' because it already exists.",
                        errorId: "TopicAlreadyExists",
                        targetObject: topicName);
                }
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Retrieves Google Cloud PubSub topics.
    /// </para>
    /// <para type="description">
    /// Retrieves one or more Gooogle Cloud PubSub topics.
    /// If -Topic is not used, the cmdlet will return all the topics under the specified project
    /// (default project if -Project is not used). Otherwise, the cmdlet will return a list of topics
    /// matching the topic names specified in -Topic and will raise an error for any topic that cannot be found.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcpsTopic</code>
    ///   <para>This command retrieves all topics in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcpsTopic -Topic "topic1", "topic2" -Project "my-project"</code>
    ///   <para>
    ///   This command retrieves 2 topics ("topic1" and "topic2") in the project "my-project".
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/publisher#list-topics-in-your-project)">
    /// [Listing a Topic]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcpsTopic")]
    public class GetGcpsTopic : GcpsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for topics. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names of the topics to be retrieved.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0)]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string[] Topic { get; set; }

        protected override void ProcessRecord()
        {
            if (Topic != null && Topic.Length > 0)
            {
                foreach (string topicName in Topic)
                {
                    string formattedTopicName = GetProjectPrefixForTopic(topicName, Project);
                    try
                    {
                        ProjectsResource.TopicsResource.GetRequest getRequest = Service.Projects.Topics.Get(formattedTopicName);
                        WriteObject(getRequest.Execute());
                    }
                    catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                    {
                        WriteResourceMissingError(
                            exceptionMessage: $"Topic '{topicName}' does not exist in project '{Project}'.",
                            errorId: "TopicNotFound",
                            targetObject: topicName);
                    }
                }
            }
            else
            {
                ProjectsResource.TopicsResource.ListRequest listRequest = Service.Projects.Topics.List($"projects/{Project}");
                do
                {
                    ListTopicsResponse response = listRequest.Execute();
                    if (response.Topics != null)
                    {
                        WriteObject(response.Topics, true);
                    }
                    listRequest.PageToken = response.NextPageToken;
                }
                while (!Stopping && listRequest.PageToken != null);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes Google Cloud PubSub topics.
    /// </para>
    /// <para type="description">
    /// Removes one or more Gooogle Cloud PubSub topics. Will raise errors if the topics do not exist.
    /// The cmdlet will delete the topics in the default project if -Project is not used.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcpsTopic -Topic "my-topic"</code>
    ///   <para>This command removes topic "my-topic" in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcpsTopic -Topic "topic1", "topic2" -Project "my-project"</code>
    ///   <para>
    ///   This command removes 2 topics ("topic1" and "topic2") in the project "my-project".
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/pubsub/docs/publisher#delete)">
    /// [Deleting a Topic]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcpsTopic", SupportsShouldProcess = true)]
    public class RemoveGcpsTopic : GcpsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to remove the topics in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names of the topics to be removed.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [ArrayPropertyTransform(typeof(Topic), nameof(Apis.Pubsub.v1.Data.Topic.Name))]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string[] Topic { get; set; }

        protected override void ProcessRecord()
        {
            foreach (string topicName in Topic)
            {
                string formattedTopicName = GetProjectPrefixForTopic(topicName, Project);
                try
                {
                    if (ShouldProcess(formattedTopicName, "Remove Topic"))
                    {
                        ProjectsResource.TopicsResource.DeleteRequest request = Service.Projects.Topics.Delete(formattedTopicName);
                        request.Execute();
                    }
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    WriteResourceMissingError(
                        exceptionMessage: $"Topic '{topicName}' does not exist in project '{Project}'.",
                        errorId: "TopicNotFound",
                        targetObject: topicName);
                }
            }
        }
    }
}
