// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net;
using System.Threading.Tasks;

namespace Google.PowerShell.CloudStorage
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Cloud Storage buckets
    /// </para>
    /// <para type="description">
    /// If a name is specified, gets the Google Cloud Storage bucket with the given name. The gcloud user must
    /// have access to view the bucket.
    /// </para>
    /// <para type="description">
    /// If a name is not specified, gets all Google Cloud Storage buckets owned by a project. The project can
    /// be specifed. If it is not, the project in the active Cloud SDK configuration will be used. The gcloud
    /// user must have access to view the project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket "widget-co-logs"</code>
    ///   <para>Get the bucket named "widget-co-logs".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket -Project "widget-co"</code>
    ///   <para>Get all buckets for project "widget-co".</para>
    /// </example>
    /// <example>
    ///   <code>Get-GcsBucket</code>
    ///   <para>Get all buckets for current project in the active gcloud configuration.</para>
    /// </example>
    /// </summary>
    public abstract class GcsAclCmdlet : GcsCmdlet
    {
        protected class ParameterSetNames
        {
            public const string Project = "User";
            public const string User = "Group";
            public const string Group = "Domain";
            public const string Domain = "Team";
            public const string AllUsers = "AllUsers";
            public const string AllAuthenticatedUsers = "AllAuthenticatedUsers";
            public const string Default = "Default";
        }

        /// <summary>
        /// <para type="description">
        /// The name of the bucket that the access control will be applied to.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public virtual string BucketName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The user holding the access control. This can either be an email or a user ID.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.User)]
        [Alias("UserId", "UserEmail")]
        [ValidateNotNullOrEmpty]
        public string User { get; set; }

        /// <summary>
        /// <para type="description">
        /// The group holding the access control. This can either be an email or an ID.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.Group)]
        [Alias("GroupId", "GroupEmail")]
        [ValidateNotNullOrEmpty]
        public string Group { get; set; }

        /// <summary>
        /// <para type="description">
        /// The domain holding the access control.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.Domain)]
        [ValidateNotNullOrEmpty]
        public string Domain { get; set; }

        /// <summary>
        /// <para type="description">
        /// The project number of the project holding the access control.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.Project)]
        [ValidateNotNullOrEmpty]
        public string ProjectNumber { get; set; }

        /// <summary>
        /// <para type="description">
        /// The project role (in the project specified by -ProjectNumber) holding the access control.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.Project)]
        [ValidateSet("Owners", "Editors", "Viewers", IgnoreCase = true)]
        public string ProjectRole { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the access control will be for all user.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AllUsers)]
        public SwitchParameter AllUsers { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, the access control will be for all authenticated user.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.AllAuthenticatedUsers)]
        public SwitchParameter AllAuthenticatedUsers { get; set; }

        protected string GetAclEntity()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.User:
                    return $"user-{User}";
                case ParameterSetNames.Group:
                    return $"group-{Group}";
                case ParameterSetNames.Domain:
                    return $"domain-{Domain}";
                case ParameterSetNames.Project:
                    return $"project-{ProjectRole.ToLower()}-{ProjectNumber}";
                case ParameterSetNames.AllUsers:
                    return "allUsers";
                case ParameterSetNames.AllAuthenticatedUsers:
                    return "allAuthenticatedUsers";
                default:
                    throw UnknownParameterSetException;
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Cloud Storage buckets
    /// </para>
    /// <para type="description">
    /// If a name is specified, gets the Google Cloud Storage bucket with the given name. The gcloud user must
    /// have access to view the bucket.
    /// </para>
    /// <para type="description">
    /// If a name is not specified, gets all Google Cloud Storage buckets owned by a project. The project can
    /// be specifed. If it is not, the project in the active Cloud SDK configuration will be used. The gcloud
    /// user must have access to view the project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket "widget-co-logs"</code>
    ///   <para>Get the bucket named "widget-co-logs".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket -Project "widget-co"</code>
    ///   <para>Get all buckets for project "widget-co".</para>
    /// </example>
    /// <example>
    ///   <code>Get-GcsBucket</code>
    ///   <para>Get all buckets for current project in the active gcloud configuration.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcsBucketAcl", DefaultParameterSetName = ParameterSetNames.User)]
    [OutputType(typeof(Bucket))]
    public class AddGcsBucketAcl : GcsAclCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The role of the access control.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateSet("Reader", "Writer", "Owner", IgnoreCase = true)]
        public string Role { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            BucketAccessControl bucketAcl = new BucketAccessControl()
            {
                Role = Role.ToUpper(),
                Entity = GetAclEntity()
            };
            BucketAccessControlsResource.InsertRequest request =
                Service.BucketAccessControls.Insert(bucketAcl, BucketName);
            BucketAccessControl response = request.Execute();
            WriteObject(response);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Cloud Storage buckets
    /// </para>
    /// <para type="description">
    /// If a name is specified, gets the Google Cloud Storage bucket with the given name. The gcloud user must
    /// have access to view the bucket.
    /// </para>
    /// <para type="description">
    /// If a name is not specified, gets all Google Cloud Storage buckets owned by a project. The project can
    /// be specifed. If it is not, the project in the active Cloud SDK configuration will be used. The gcloud
    /// user must have access to view the project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket "widget-co-logs"</code>
    ///   <para>Get the bucket named "widget-co-logs".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket -Project "widget-co"</code>
    ///   <para>Get all buckets for project "widget-co".</para>
    /// </example>
    /// <example>
    ///   <code>Get-GcsBucket</code>
    ///   <para>Get all buckets for current project in the active gcloud configuration.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcsBucketAcl")]
    [OutputType(typeof(Bucket))]
    public class GetGcsBucketAcl : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket that we retrieves the access controls from.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public virtual string BucketName { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            BucketAccessControlsResource.ListRequest request = Service.BucketAccessControls.List(BucketName);
            BucketAccessControls response = request.Execute();
            WriteObject(response.Items, true);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Cloud Storage buckets
    /// </para>
    /// <para type="description">
    /// If a name is specified, gets the Google Cloud Storage bucket with the given name. The gcloud user must
    /// have access to view the bucket.
    /// </para>
    /// <para type="description">
    /// If a name is not specified, gets all Google Cloud Storage buckets owned by a project. The project can
    /// be specifed. If it is not, the project in the active Cloud SDK configuration will be used. The gcloud
    /// user must have access to view the project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket "widget-co-logs"</code>
    ///   <para>Get the bucket named "widget-co-logs".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket -Project "widget-co"</code>
    ///   <para>Get all buckets for project "widget-co".</para>
    /// </example>
    /// <example>
    ///   <code>Get-GcsBucket</code>
    ///   <para>Get all buckets for current project in the active gcloud configuration.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsBucketAcl", DefaultParameterSetName = ParameterSetNames.User)]
    [OutputType(typeof(Bucket))]
    public class RemoveGcsBucketAcl : GcsAclCmdlet
    {
        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            string entity = GetAclEntity();
            BucketAccessControlsResource.DeleteRequest request =
                Service.BucketAccessControls.Delete(BucketName, entity);
            request.Execute();
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Cloud Storage buckets
    /// </para>
    /// <para type="description">
    /// If a name is specified, gets the Google Cloud Storage bucket with the given name. The gcloud user must
    /// have access to view the bucket.
    /// </para>
    /// <para type="description">
    /// If a name is not specified, gets all Google Cloud Storage buckets owned by a project. The project can
    /// be specifed. If it is not, the project in the active Cloud SDK configuration will be used. The gcloud
    /// user must have access to view the project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket "widget-co-logs"</code>
    ///   <para>Get the bucket named "widget-co-logs".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket -Project "widget-co"</code>
    ///   <para>Get all buckets for project "widget-co".</para>
    /// </example>
    /// <example>
    ///   <code>Get-GcsBucket</code>
    ///   <para>Get all buckets for current project in the active gcloud configuration.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcsObjectAcl", DefaultParameterSetName = ParameterSetNames.User)]
    [OutputType(typeof(Bucket))]
    public class AddGcsObjectAcl : GcsAclCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the object that the access control will be applied to.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The role of the access control.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateSet("Reader", "Owner", IgnoreCase = true)]
        public string Role { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            ObjectAccessControl bucketAcl = new ObjectAccessControl()
            {
                Role = Role.ToUpper(),
                Entity = GetAclEntity(),
            };
            ObjectAccessControlsResource.InsertRequest request =
                Service.ObjectAccessControls.Insert(bucketAcl, BucketName, ObjectName);
            ObjectAccessControl response = request.Execute();
            WriteObject(response);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Cloud Storage buckets
    /// </para>
    /// <para type="description">
    /// If a name is specified, gets the Google Cloud Storage bucket with the given name. The gcloud user must
    /// have access to view the bucket.
    /// </para>
    /// <para type="description">
    /// If a name is not specified, gets all Google Cloud Storage buckets owned by a project. The project can
    /// be specifed. If it is not, the project in the active Cloud SDK configuration will be used. The gcloud
    /// user must have access to view the project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket "widget-co-logs"</code>
    ///   <para>Get the bucket named "widget-co-logs".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket -Project "widget-co"</code>
    ///   <para>Get all buckets for project "widget-co".</para>
    /// </example>
    /// <example>
    ///   <code>Get-GcsBucket</code>
    ///   <para>Get all buckets for current project in the active gcloud configuration.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcsObjectAcl")]
    [OutputType(typeof(Bucket))]
    public class GetGcsObjectAcl : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the object that the access control will be applied to.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string ObjectName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the bucket that we retrieves the access controls from.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public virtual string BucketName { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            ObjectAccessControlsResource.ListRequest request = Service.ObjectAccessControls.List(BucketName, ObjectName);
            ObjectAccessControls response = request.Execute();
            WriteObject(response.Items, true);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Cloud Storage buckets
    /// </para>
    /// <para type="description">
    /// If a name is specified, gets the Google Cloud Storage bucket with the given name. The gcloud user must
    /// have access to view the bucket.
    /// </para>
    /// <para type="description">
    /// If a name is not specified, gets all Google Cloud Storage buckets owned by a project. The project can
    /// be specifed. If it is not, the project in the active Cloud SDK configuration will be used. The gcloud
    /// user must have access to view the project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket "widget-co-logs"</code>
    ///   <para>Get the bucket named "widget-co-logs".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket -Project "widget-co"</code>
    ///   <para>Get all buckets for project "widget-co".</para>
    /// </example>
    /// <example>
    ///   <code>Get-GcsBucket</code>
    ///   <para>Get all buckets for current project in the active gcloud configuration.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsObjectAcl", DefaultParameterSetName = ParameterSetNames.User)]
    [OutputType(typeof(Bucket))]
    public class RemoveGcsObjectAcl : GcsAclCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the object that the access control will be applied to.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string ObjectName { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            string entity = GetAclEntity();
            ObjectAccessControlsResource.DeleteRequest request =
                Service.ObjectAccessControls.Delete(BucketName, ObjectName, entity);
            request.Execute();
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Cloud Storage buckets
    /// </para>
    /// <para type="description">
    /// If a name is specified, gets the Google Cloud Storage bucket with the given name. The gcloud user must
    /// have access to view the bucket.
    /// </para>
    /// <para type="description">
    /// If a name is not specified, gets all Google Cloud Storage buckets owned by a project. The project can
    /// be specifed. If it is not, the project in the active Cloud SDK configuration will be used. The gcloud
    /// user must have access to view the project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket "widget-co-logs"</code>
    ///   <para>Get the bucket named "widget-co-logs".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket -Project "widget-co"</code>
    ///   <para>Get all buckets for project "widget-co".</para>
    /// </example>
    /// <example>
    ///   <code>Get-GcsBucket</code>
    ///   <para>Get all buckets for current project in the active gcloud configuration.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "DefaultObjectAcl", DefaultParameterSetName = ParameterSetNames.User)]
    [OutputType(typeof(Bucket))]
    public class AddDefaultObjectAcl : GcsAclCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The role of the access control.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateSet("Reader", "Owner", IgnoreCase = true)]
        public string Role { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            ObjectAccessControl newDefaultAcl = new ObjectAccessControl()
            {
                Role = Role.ToUpper(),
                Entity = GetAclEntity(),
            };
            DefaultObjectAccessControlsResource.InsertRequest request =
                Service.DefaultObjectAccessControls.Insert(newDefaultAcl, BucketName);
            ObjectAccessControl response = request.Execute();
            WriteObject(response);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Cloud Storage buckets
    /// </para>
    /// <para type="description">
    /// If a name is specified, gets the Google Cloud Storage bucket with the given name. The gcloud user must
    /// have access to view the bucket.
    /// </para>
    /// <para type="description">
    /// If a name is not specified, gets all Google Cloud Storage buckets owned by a project. The project can
    /// be specifed. If it is not, the project in the active Cloud SDK configuration will be used. The gcloud
    /// user must have access to view the project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket "widget-co-logs"</code>
    ///   <para>Get the bucket named "widget-co-logs".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket -Project "widget-co"</code>
    ///   <para>Get all buckets for project "widget-co".</para>
    /// </example>
    /// <example>
    ///   <code>Get-GcsBucket</code>
    ///   <para>Get all buckets for current project in the active gcloud configuration.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "DefaultObjectAcl")]
    [OutputType(typeof(Bucket))]
    public class GetDefaultObjectAcl : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket that we retrieves the access controls from.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public virtual string BucketName { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            DefaultObjectAccessControlsResource.ListRequest request =
                Service.DefaultObjectAccessControls.List(BucketName);
            ObjectAccessControls response = request.Execute();
            WriteObject(response.Items, true);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Cloud Storage buckets
    /// </para>
    /// <para type="description">
    /// If a name is specified, gets the Google Cloud Storage bucket with the given name. The gcloud user must
    /// have access to view the bucket.
    /// </para>
    /// <para type="description">
    /// If a name is not specified, gets all Google Cloud Storage buckets owned by a project. The project can
    /// be specifed. If it is not, the project in the active Cloud SDK configuration will be used. The gcloud
    /// user must have access to view the project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket "widget-co-logs"</code>
    ///   <para>Get the bucket named "widget-co-logs".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcsBucket -Project "widget-co"</code>
    ///   <para>Get all buckets for project "widget-co".</para>
    /// </example>
    /// <example>
    ///   <code>Get-GcsBucket</code>
    ///   <para>Get all buckets for current project in the active gcloud configuration.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "DefaultObjectAcl", DefaultParameterSetName = ParameterSetNames.User)]
    [OutputType(typeof(Bucket))]
    public class RemoveDefaultObjectAcl : GcsAclCmdlet
    {
        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            string entity = GetAclEntity();
            DefaultObjectAccessControlsResource.DeleteRequest request =
                Service.DefaultObjectAccessControls.Delete(BucketName, entity);
            request.Execute();
        }
    }
}
