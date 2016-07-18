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
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlSslCert")]
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
        public string Project { get; set; }

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
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcSqlSslCert", DefaultParameterSetName = ParameterSetNames.ByName)]
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
        public string Project { get; set; }

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
        [Parameter(Mandatory = true, Position = 1)]
        public string CommonName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Instance we want to add an SSL Certificate to.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByInstance, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
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
    /// The change will not take effect until the instance is restarted for first-generation instances.
    /// </para>
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
        public string Project { get; set; }

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

            if (!ShouldProcess($"{project}/{instance}/{name}", "Delete SSL Certificate"))
            {
                return;
            }
            SslCertsResource.DeleteRequest request = Service.SslCerts.Delete(project, instance, finger);
            Operation result = request.Execute();
            WaitForSqlOperation(result);
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
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GcSqlSslEphemeral")]
    public class AddGcSqlSslEphemeralCmdlet : GcSqlCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

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
        /// Should be RSA or EC.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true)]
        public string PublicKey { get; set; }

        protected override void ProcessRecord()
        {
            SslCertsCreateEphemeralRequest body = new SslCertsCreateEphemeralRequest {
                PublicKey = PublicKey
            };
            SslCertsResource.CreateEphemeralRequest request = Service.SslCerts.CreateEphemeral(body, Project, Instance);
            SslCert result = request.Execute();
            WriteObject(result);
        }
    }
}
