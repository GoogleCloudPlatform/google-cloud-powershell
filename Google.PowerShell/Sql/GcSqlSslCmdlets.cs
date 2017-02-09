// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.SQLAdmin.v1beta4;
using Google.Apis.SQLAdmin.v1beta4.Data;
using System.Management.Automation;
using Google.PowerShell.Common;
using System.Collections.Generic;

namespace Google.PowerShell.Sql
{
    /// <summary>
    /// <para type="synopsis">
    /// Retrieves a particular SSL certificate, or lists the current SSL certificates for an instance.
    /// Does not include the private key- for the private key must be saved from the response to initial creation.
    /// </para>
    /// <para type="description">
    /// Retrieves the specified SSL certificate, or lists the current SSL certificates for that instance.
    /// This is determined by if an Sha1Fingerprint is specified or not.
    /// Does not include the private key.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcSqlSslCert "myInstance"</code>
    ///   <para>Gets a list of SSL Certificates for the instance "myInstance".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcSqlSslCert $myInstance</code>
    ///   <para>Gets a list of SSL Certificates for the instance stored in $myInstance.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcSqlSslCert "myInstance" "myFinger"</code>
    ///   <para>
    ///   Get a resource for the SSL Certificate identified by the Sha1Fingerprint "myFinger" for the instance "myInstance".
    ///   </para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlSslCert")]
    [OutputType(typeof(SslCert))]
    public class GetGcSqlSslCmdlet : GcSqlCmdlet
    {
        internal class ParameterSetNames
        {
            public const string GetSingle = "Single";
            public const string GetList = "List";
            public const string GetListInstance = "List from Instance";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Cloud SQL instance name.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.GetSingle)]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.GetList)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// Sha1 FingerPrint for the SSL Certificate.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.GetSingle)]
        public string Sha1Fingerprint { get; set; }

        /// <summary>
        /// <para type="description">
        /// An instance resource that you want to get the SSL certificates from.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.GetListInstance)]
        public DatabaseInstance InstanceObject { get; set; }

        protected override void ProcessRecord()
        {
            if (InstanceObject != null)
            {
                string Project = InstanceObject.Project;
                string Instance = InstanceObject.Name;
                SslCertsResource.ListRequest request = Service.SslCerts.List(Project, Instance);
                SslCertsListResponse result = request.Execute();
                WriteObject(result.Items, true);
            }
            else
            {
                if (Sha1Fingerprint != null)
                {
                    SslCertsResource.GetRequest request = Service.SslCerts.Get(Project, Instance, Sha1Fingerprint);
                    SslCert result = request.Execute();
                    WriteObject(result);
                }
                else
                {
                    SslCertsResource.ListRequest request = Service.SslCerts.List(Project, Instance);
                    SslCertsListResponse result = request.Execute();
                    WriteObject(result.Items, true);
                }
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates an SSL certificate and returns it along with the private key and server certificate authority. 
    /// The new certificate is not usable until the instance is restarted for first-generation instances.
    /// </para>
    /// <para type="description">
    /// Creates an SSL certificate inside the given instance and returns it along with the private key and server certificate authority.
    /// The new certificate is not usable until the instance is restarted for first-generation instances
    /// </para>
    /// <example>
    ///   <code>PS C:\> Add-GcSqlSslCert "myInstance" "myCert"</code>
    ///   <para>Adds the SSL Certificate called "myCert" to the instance "myInstance".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> $myInstance | Add-GcSqlSslCert "myCert"</code>
    ///   <para>Adds the SSL Certificate called "myCert" to the instance stored in $myInstance.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcSqlSslCert", DefaultParameterSetName = ParameterSetNames.ByName)]
    [OutputType(typeof(SslCertDetail))]
    public class AddGcSqlSslCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByInstance = "ByInstance";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Cloud SQL instance name.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByName)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// Distinct name for the certificate being added to the instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 1)]
        [Parameter(ParameterSetName = ParameterSetNames.ByInstance, Mandatory = true, Position = 0)]
        public string CommonName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Instance we want to add an SSL Certificate to.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByInstance, Mandatory = true, ValueFromPipeline = true)]
        public DatabaseInstance InstanceObject { get; set; }

        protected override void ProcessRecord()
        {
            string project;
            string instance;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    instance = Instance;
                    project = Project;
                    break;
                case ParameterSetNames.ByInstance:
                    instance = InstanceObject.Name;
                    project = InstanceObject.Project;
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            SslCertsInsertRequest RequestBody = new SslCertsInsertRequest
            {
                CommonName = CommonName
            };
            SslCertsResource.InsertRequest request = Service.SslCerts.Insert(RequestBody, project, instance);
            WriteVerbose($"Adding the SSL Certificate '{CommonName}' to the Instance {instance}.");
            SslCertsInsertResponse result = request.Execute();
            WriteObject(result.ClientCert);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes the SSL certificate. 
    /// The change will not take effect until the instance is restarted for first-generation instances.
    /// </para>
    /// <para type="description">
    /// Deletes the SSL certificate for the instance.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcSqlSslCert "myInstance" "myFinger"</code>
    ///   <para>
    ///   Removes the SSL Certificate identified with the sha1Fingerprint "myFinger" from the instance "myInstance".
    ///   </para>
    /// </example>
    /// <example>
    ///   <para>Removes the SSL Certificate stored in "myInstance".</para>
    ///   <code>PS C:\> Remove-GcSqlSslCert "myInstance"</code>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcSqlSslCert", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.ByName)]
    public class RemoveGcSqlSslCmdlet : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Cloud SQL instance name.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByName)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// Sha1 FingerPrint for the SSL Certificate you want to delete.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.ByName)]
        public string Sha1Fingerprint { get; set; }

        /// <summary>
        /// <para type="description">
        /// The SSL Certificate that describes the SSL Certificate to remove.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public SslCert Cert { get; set; }

        protected override void ProcessRecord()
        {
            string finger;
            string instance;
            string project;
            string name;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    finger = Sha1Fingerprint;
                    instance = Instance;
                    project = Project;
                    name = Service.SslCerts.Get(Project, Instance, Sha1Fingerprint).Execute().CommonName;
                    break;
                case ParameterSetNames.ByObject:
                    finger = Cert.Sha1Fingerprint;
                    instance = Cert.Instance;
                    project = GetProjectNameFromUri(Cert.SelfLink);
                    name = Cert.CommonName;
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            if (ShouldProcess($"{project}/{instance}/{name}", "Delete SSL Certificate"))
            {
                SslCertsResource.DeleteRequest request = Service.SslCerts.Delete(project, instance, finger);
                WriteVerbose(
                    $"Removing the SSL Certificate with Sha1Fingerprint '{finger}' from the Instance {instance}.");
                Operation result = request.Execute();
                WaitForSqlOperation(result);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Generates a short-lived X509 certificate containing the provided public key
    /// and signed by a private key specific to the target instance.
    /// Users may use the certificate to authenticate as themselves when connecting to the database. 
    /// </para>
    /// <para type="description">
    /// Generates a short-lived X509 certificate containing the provided public key
    /// and signed by a private key specific to the target instance.
    /// Users may use the certificate to authenticate as themselves when connecting to the database. 
    /// </para>
    /// <example>
    ///   <code>
    ///     PS C:\> Add-GcSqlSslEphemeral "myInstance" "-----BEGIN PUBLIC KEY-----..."
    ///   </code>
    ///   <para>Adds an ephemeral SSL Certificate to the instance "myInstance"</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcSqlSslEphemeral")]
    [OutputType(typeof(SslCert))]
    public class AddGcSqlSslEphemeralCmdlet : GcSqlCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Cloud SQL instance name.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// PEM encoded public key to include in the signed certificate.
        /// Should be RSA or EC. Line endings should be LF.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true)]
        public string PublicKey { get; set; }

        protected override void ProcessRecord()
        {
            SslCertsCreateEphemeralRequest body = new SslCertsCreateEphemeralRequest
            {
                PublicKey = PublicKey
            };
            SslCertsResource.CreateEphemeralRequest request = Service.SslCerts.CreateEphemeral(body, Project, Instance);
            WriteVerbose($"Adding an ephemeral SSL Certificate to the Instance {Instance}.");
            SslCert result = request.Execute();
            WriteObject(result);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes all client certificates and generates a new server SSL certificate for the instance. 
    /// </para>
    /// <para type="description">
    /// Deletes all client certificates and generates a new server SSL certificate for the instance. 
    /// The changes will not take effect until the instance is restarted. 
    /// Existing instances without a server certificate will need to call this once to set a server certificate.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Reset-GcSqlSslConfig "myInstance"</code>
    ///   <para>Resets the SSL Certificates for the "myInstance" instance.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Reset-GcSqlSslConfig $instance</code>
    ///   <para>
    ///   Resets the SSL Certificates for the instance represented by the resource stored in $instance.
    ///   </para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Reset, "GcSqlSslConfig", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.ByName)]
    [OutputType(typeof(DatabaseInstance))]
    public class ResetGcSqlSslConfig : GcSqlCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByInstance = "ByInstance";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Cloud SQL instance name.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByName)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// An instance resourve that you want to reset the SSL Configuration for.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ByInstance)]
        public DatabaseInstance InstanceObject { get; set; }

        protected override void ProcessRecord()
        {
            string project;
            string instance;
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    instance = Instance;
                    project = Project;
                    break;
                case ParameterSetNames.ByInstance:
                    instance = InstanceObject.Name;
                    project = InstanceObject.Project;
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            if (ShouldProcess($"{project}/{instance}", "Reset SSL Certificate Configuration"))
            {
                InstancesResource.ResetSslConfigRequest request = Service.Instances.ResetSslConfig(project, instance);
                WriteVerbose($"Resetting the SSL Certificate Configuration for the Instance {instance}.");
                Operation result = request.Execute();
                DatabaseInstance updated = Service.Instances.Get(project, instance).Execute();
                WriteObject(updated);
            }
        }
    }
}
