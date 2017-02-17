// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.CloudStorage
{
    /// <summary>
    /// Base class for ACL-related Cloud Storage cmdlets. Contains inherited parameters to be used in derived classes.
    /// Derived classes are expected to have 6 different parameter sets, corresponding to the scope of the ACL being
    /// added/removed. e.g. User, Group, Team, etc.
    /// </summary>
    public abstract class GcsAclCmdlet : GcsCmdlet
    {
        protected class ParameterSetNames
        {
            public const string Project = "Team";
            public const string User = "User";
            public const string Group = "Group";
            public const string Domain = "Domain";
            public const string AllUsers = "AllUsers";
            public const string AllAuthenticatedUsers = "AllAuthenticatedUsers";
        }

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
        /// This is used in conjunction with -ProjectRole parameter to specify a project team.
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

        /// <summary>
        /// Returns the entity holding the access control based on the cmdlet parameters.
        /// Entity will be of the form user-userId, user-emailAddress, group-groupId,
        /// group-emailAddress, project-role-projectNumber, allUsers or allAuthenticatedUsers.
        /// </summary>
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
    /// Add an access control to a Google Cloud Storage bucket.
    /// </para>
    /// <para type="description">
    /// Add an access control to a Google Cloud Storage bucket for an entity.
    /// Entity can be user ID, user email address, project team, group ID,
    /// group email address, all users or all authenticated users.
    /// The roles that can be assigned to an entity are Reader, Writer and Owner.
    /// User must have access to the bucket.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Add-GcsBucketAcl -Role Reader -Bucket "my-bucket" -User user@example.com</code>
    ///   <para>Adds reader access control to bucket "my-bucket" for user user@example.com.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Add-GcsBucketAcl -Role Writer -Bucket "my-bucket" -Domain example.com</code>
    ///   <para>Adds writer access control to bucket "my-bucket" for the domain example.com.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Add-GcsBucketAcl -Role Owner -Bucket "my-bucket" -AllUsers</code>
    ///   <para>Adds owner access control to bucket "my-bucket" for all users.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Add-GcsBucketAcl -Role Owner -Bucket "my-bucket" -ProjectRole Owners -ProjectNumber 3423432</code>
    ///   <para>Adds owner access control to bucket "my-bucket" for all owners of project 3423432.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/access-control/lists)">
    /// [Access Control Lists (ACLs)]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/json_api/v1/bucketAccessControls)">
    /// [Bucket Access Controls]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcsBucketAcl", DefaultParameterSetName = ParameterSetNames.User)]
    [OutputType(typeof(BucketAccessControl))]
    public class AddGcsBucketAcl : GcsAclCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket that the access control will be applied to.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [Alias("Bucket")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

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
            BucketAccessControlsResource.InsertRequest request = Service.BucketAccessControls.Insert(bucketAcl, Name);
            BucketAccessControl response = request.Execute();
            WriteObject(response);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets all the access controls of a Google Cloud Storage bucket.
    /// </para>
    /// <para type="description">
    /// Gets all the access controls of a Google Cloud Storage bucket. 
    /// User must have access to the bucket.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GetGcsBucketAcl -Bucket "my-bucket"</code>
    ///   <para>Gets all access controls of bucket "my-bucket".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/access-control/lists)">
    /// [Access Control Lists (ACLs)]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/json_api/v1/bucketAccessControls)">
    /// [Bucket Access Controls]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcsBucketAcl")]
    [OutputType(typeof(BucketAccessControls))]
    public class GetGcsBucketAcl : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket that we retrieves the access controls from.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [Alias("Bucket")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            BucketAccessControlsResource.ListRequest request = Service.BucketAccessControls.List(Name);
            BucketAccessControls response = request.Execute();
            WriteObject(response.Items, true);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes an access control from a Google Cloud Storage bucket.
    /// </para>
    /// <para type="description">
    /// Removes an access control from a Google Cloud Storage bucket for an entity.
    /// Entity can be user ID, user email address, project team, group ID,
    /// group email address, all users or all authenticated users.
    /// The roles that can be assigned to an entity are Reader, Writer and Owner.
    /// User must have access to the bucket. Assumes the entity already
    /// have an access control for the bucket.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcsBucketAcl -Bucket "my-bucket" -User user@example.com</code>
    ///   <para>Removes access control to bucket "my-bucket" for user user@example.com.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcsBucketAcl -Bucket "my-bucket" -Domain example.com</code>
    ///   <para>Removes access control to bucket "my-bucket" for the domain example.com.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcsBucketAcl -Bucket "my-bucket" -AllUsers</code>
    ///   <para>Removes access control to bucket "my-bucket" for all users.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcsBucketAcl -Bucket "my-bucket" -ProjectRole Owners -ProjectNumber 3423432</code>
    ///   <para>Removes access control to bucket "my-bucket" for all owners of project 3423432.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/access-control/lists)">
    /// [Access Control Lists (ACLs)]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/json_api/v1/bucketAccessControls)">
    /// [Bucket Access Controls]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsBucketAcl", DefaultParameterSetName = ParameterSetNames.User)]
    public class RemoveGcsBucketAcl : GcsAclCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket that the access control will be removed from.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [Alias("Bucket")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            string entity = GetAclEntity();
            BucketAccessControlsResource.DeleteRequest request = Service.BucketAccessControls.Delete(Name, entity);
            request.Execute();
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Add an access control to a Google Cloud Storage object.
    /// </para>
    /// <para type="description">
    /// Add an access control to a Google Cloud Storage object for an entity.
    /// Entity can be user ID, user email address, project team, group ID,
    /// group email address, all users or all authenticated users.
    /// The roles that can be assigned to an entity are Reader, Writer and Owner.
    /// User must have access to the object.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Add-GcsObjectAcl -Role Reader -Bucket "my-bucket" -ObjectName "my-object" -User user@example.com</code>
    ///   <para>Adds reader access control to the object "my-object" in bucket "my-bucket" for user user@example.com.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Add-GcsObjectAcl -Role Writer -Bucket "my-bucket" -ObjectName "my-object"  -Domain example.com</code>
    ///   <para>Adds writer access control to the object "my-object" in bucket "my-bucket" for the domain example.com.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Add-GcsObjectAcl -Role Owner -Bucket "my-bucket" -ObjectName "my-object"  -AllUsers</code>
    ///   <para>Adds owner access control to the object "my-object" in bucket "my-bucket" for all users.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Add-GcsObjectAcl -Role Owner -Bucket "my-bucket" -ObjectName "my-object"  -ProjectRole Owners -ProjectNumber 3423432
    ///   </code>
    ///   <para>
    ///   Adds owner access control to the object "my-object" in bucket "my-bucket" for all owners of project 3423432.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/access-control/lists)">
    /// [Access Control Lists (ACLs)]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/json_api/v1/objectAccessControls)">
    /// [Object Access Controls]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcsObjectAcl", DefaultParameterSetName = ParameterSetNames.User)]
    [OutputType(typeof(ObjectAccessControl))]
    public class AddGcsObjectAcl : GcsAclCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket that the access control will be applied to.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the object that the access control will be applied to.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
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
            ObjectAccessControl objAcl = new ObjectAccessControl()
            {
                Role = Role.ToUpper(),
                Entity = GetAclEntity(),
            };
            ObjectAccessControlsResource.InsertRequest request = Service.ObjectAccessControls.Insert(objAcl, Bucket, ObjectName);
            ObjectAccessControl response = request.Execute();
            WriteObject(response);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets all the access controls of a Google Cloud Storage object.
    /// </para>
    /// <para type="description">
    /// Gets all the access controls of a Google Cloud Storage object. 
    /// User must have access to the object.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcsObjectAcl -Bucket "my-bucket" -ObjectName "my-object"</code>
    ///   <para>Gets all access controls of the object "my-object" in bucket "my-bucket".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/access-control/lists)">
    /// [Access Control Lists (ACLs)]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/json_api/v1/objectAccessControls)">
    /// [Object Access Controls]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcsObjectAcl")]
    [OutputType(typeof(ObjectAccessControls))]
    public class GetGcsObjectAcl : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket that we retrieves the access controls from.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the object that we retrieves the access controls from.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string ObjectName { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            ObjectAccessControlsResource.ListRequest request = Service.ObjectAccessControls.List(Bucket, ObjectName);
            ObjectAccessControls response = request.Execute();
            WriteObject(response.Items, true);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes an access control from a Google Cloud Storage object.
    /// </para>
    /// <para type="description">
    /// Removes an access control from a Google Cloud Storage object for an entity.
    /// Entity can be user ID, user email address, project team, group ID,
    /// group email address, all users or all authenticated users.
    /// The roles that can be assigned to an entity are Reader, Writer and Owner.
    /// User must have access to the bucket. Assumes the entity already
    /// have an access control for the bucket.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcsObjectAcl -Bucket "my-bucket" -ObjectName "my-object" -User user@example.com</code>
    ///   <para>Removes access control to the object "my-object" in bucket "my-bucket" for user user@example.com.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcsObjectAcl -Bucket "my-bucket" -ObjectName "my-object" -Domain example.com</code>
    ///   <para>Removes access control to the object "my-object" in bucket "my-bucket" for the domain example.com.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcsObjectAcl -Bucket "my-bucket" -ObjectName "my-object" -AllUsers</code>
    ///   <para>Removes access control to the object "my-object" in bucket "my-bucket" for all users.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Remove-GcsObjectAcl -Bucket "my-bucket" -ObjectName "my-object" -ProjectRole Owners -ProjectNumber 3423432
    ///   </code>
    ///   <para>Removes access control to the object "my-object" in bucket "my-bucket" for all owners of project 3423432.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/access-control/lists)">
    /// [Access Control Lists (ACLs)]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/json_api/v1/objectAccessControls)">
    /// [Object Access Controls]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsObjectAcl", DefaultParameterSetName = ParameterSetNames.User)]
    [OutputType(typeof(Bucket))]
    public class RemoveGcsObjectAcl : GcsAclCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket that the access control will be removed from.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Bucket { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the object that the access control will be removed from.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string ObjectName { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            string entity = GetAclEntity();
            ObjectAccessControlsResource.DeleteRequest request = Service.ObjectAccessControls.Delete(Bucket, ObjectName, entity);
            request.Execute();
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Add a default access control to a Google Cloud Storage bucket.
    /// </para>
    /// <para type="description">
    /// Add a default access control to a Google Cloud Storage bucket for an entity.
    /// The default access control will be aplied to a new object when no access control is provided.
    /// Entity can be user ID, user email address, project team, group ID,
    /// group email address, all users or all authenticated users.
    /// The roles that can be assigned to an entity are Reader, Writer and Owner.
    /// User must have access to the bucket.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Add-GcsDefaultObjectAcl -Role Reader -Bucket "my-bucket" -User user@example.com</code>
    ///   <para>Adds reader default access control to bucket "my-bucket" for user user@example.com.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Add-GcsDefaultObjectAcl -Role Writer -Bucket "my-bucket" -Domain example.com</code>
    ///   <para>Adds writer default access control to bucket "my-bucket" for the domain example.com.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Add-GcsDefaultObjectAcl -Role Owner -Bucket "my-bucket" -AllUsers</code>
    ///   <para>Adds owner default access control to bucket "my-bucket" for all users.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Add-GcsDefaultObjectAcl -Role Owner -Bucket "my-bucket" -ProjectRole Owners -ProjectNumber 3423432
    ///   </code>
    ///   <para>Adds owner default access control to bucket "my-bucket" for all owners of project 3423432.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/access-control/lists)">
    /// [Access Control Lists (ACLs)]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/json_api/v1/defaultObjectAccessControls)">
    /// [Default Access Controls]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcsDefaultObjectAcl", DefaultParameterSetName = ParameterSetNames.User)]
    [OutputType(typeof(ObjectAccessControl))]
    public class AddGcsDefaultObjectAcl : GcsAclCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket that the access control will be applied to.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [Alias("Bucket")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

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
            ObjectAccessControl defaultAcl = new ObjectAccessControl()
            {
                Role = Role.ToUpper(),
                Entity = GetAclEntity(),
            };
            DefaultObjectAccessControlsResource.InsertRequest request = Service.DefaultObjectAccessControls.Insert(defaultAcl, Name);
            ObjectAccessControl response = request.Execute();
            WriteObject(response);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets all the default access controls of a Google Cloud Storage object.
    /// </para>
    /// <para type="description">
    /// Gets all the default access controls of a Google Cloud Storage object. 
    /// User must have access to the object.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcsDefaultObjectAcl -Bucket "my-bucket" -ObjectName "my-object"</code>
    ///   <para>Gets all default access controls of the object "my-object" in bucket "my-bucket".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/access-control/lists)">
    /// [Access Control Lists (ACLs)]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/json_api/v1/defaultObjectAccessControls)">
    /// [Default Access Controls]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcsDefaultObjectAcl")]
    [OutputType(typeof(ObjectAccessControls))]
    public class GetGcsDefaultObjectAcl : GcsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket that we retrieves the access controls from.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [Alias("Bucket")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            DefaultObjectAccessControlsResource.ListRequest request = Service.DefaultObjectAccessControls.List(Name);
            ObjectAccessControls response = request.Execute();
            WriteObject(response.Items, true);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes a default access control from a Google Cloud Storage bucket.
    /// </para>
    /// <para type="description">
    /// Removes a default access control from a Google Cloud Storage bucket for an entity.
    /// Entity can be user ID, user email address, project team, group ID,
    /// group email address, all users or all authenticated users.
    /// The roles that can be assigned to an entity are Reader, Writer and Owner.
    /// User must have access to the bucket. Assumes the entity already
    /// have an access control for the bucket.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcsDefaultObjectAcl -Bucket "my-bucket" -User user@example.com</code>
    ///   <para>Removes default access control to bucket "my-bucket" for user user@example.com.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcsDefaultObjectAcl -Bucket "my-bucket" -Domain example.com</code>
    ///   <para>Removes default access control to bucket "my-bucket" for the domain example.com.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcsDefaultObjectAcl -Bucket "my-bucket" -AllUsers</code>
    ///   <para>Removes default access control to bucket "my-bucket" for all users.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcsDefaultObjectAcl -Bucket "my-bucket" -ProjectRole Owners -ProjectNumber 3423432</code>
    ///   <para>Removes default access control to bucket "my-bucket" for all owners of project 3423432.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/access-control/lists)">
    /// [Access Control Lists (ACLs)]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/storage/docs/json_api/v1/defaultObjectAccessControls)">
    /// [Default Access Controls]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcsDefaultObjectAcl", DefaultParameterSetName = ParameterSetNames.User)]
    public class RemoveGcsDefaultObjectAcl : GcsAclCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the bucket that the access control will be removed from.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [Alias("Bucket")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            string entity = GetAclEntity();
            DefaultObjectAccessControlsResource.DeleteRequest request = Service.DefaultObjectAccessControls.Delete(Name, entity);
            request.Execute();
        }
    }
}
