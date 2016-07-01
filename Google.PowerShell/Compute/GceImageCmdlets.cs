using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets information about Google Compute Engine disk images.
    /// </para>
    /// <para type="description">
    /// Gets information about Google Compute Engine disk images. These images can be used to as the inital
    /// state of a disk, whether created manually, as part of a new instance, or from an instance tempalte.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceImage", DefaultParameterSetName = ParameterSetNames.OfProject)]
    public class GetGceImageCmdlets : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string ByName = "ByName";
            public const string ByFamily = "ByFamily";
        }

        /// <summary>
        /// <para type="description">
        /// The name of the image to get. e.g. "windows-server-2012-r2-dc-v20160623".
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the image family to get the latest image of. e.g. "windows-2012-r2".
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByFamily, Mandatory = true)]
        public string Family { get; set; }

        /// <summary>
        /// <para type="description">
        /// The project that owns the image. This defaults to the gcloud config project, but very likely should
        /// be something else, such as "debian-cloud", or "windows-cloud".
        /// </para>
        /// </summary>
        [Parameter(Position = 1)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.OfProject:
                    WriteObject(GetAllProjectImages(), true);
                    break;
                case ParameterSetNames.ByName:
                    WriteObject(Service.Images.Get(Project, Name).Execute());
                    break;
                case ParameterSetNames.ByFamily:
                    WriteObject(Service.Images.GetFromFamily(Project, Family).Execute());
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private IEnumerable<Image> GetAllProjectImages()
        {
            ImagesResource.ListRequest request = Service.Images.List(Project);
            do
            {
                ImageList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (Image image in response.Items)
                    {
                        yield return image;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (request.PageToken != null && !Stopping);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a Google Compute Engine image.
    /// </para>
    /// <para type="description">
    /// Creates a Google Compute Engine image from the given disk.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceImage")]
    public class AddGceImageCmdlet : GceConcurrentCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project that will own the image. This defaults to the gcloud config project.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Disk object that describes the disk to build the image from. You can get this from Get-GceDisk.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        public Disk SourceDisk { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the image to create. This defaults to the name of the disk the image is being created
        /// from.
        /// </para>
        /// </summary>
        [Parameter]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The family this image is part of.
        /// </para>
        /// </summary>
        [Parameter]
        public string Family { get; set; }

        /// <summary>
        /// <para type="description">
        /// Human readable description of the image.
        /// </para>
        /// </summary>
        [Parameter]
        public string Description { get; set; }

        protected override void ProcessRecord()
        {
            Image body = new Image
            {
                SourceDisk = SourceDisk.SelfLink,
                DiskSizeGb = SourceDisk.SizeGb,
                Name = Name ?? SourceDisk.Name,
                Description = Description ?? SourceDisk.Description,
                Family = Family
            };

            Operation operation = Service.Images.Insert(body, Project).Execute();

            string project = Project;
            string name = body.Name;

            AddGlobalOperation(Project, operation, () =>
            {
                WriteObject(Service.Images.Get(project, name).Execute());
            });
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes a Google Compute Engine disk image.
    /// </para>
    /// <para type="description">
    /// Removes a Google Compute Engine disk image.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceImage", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.ByName)]
    public class RemoveGceImageCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the image. Defaults to the gcloud config project.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the image to delete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Image object that describes the image to delete.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Image Object { get; set; }

        protected override void ProcessRecord()
        {
            if (ParameterSetName == ParameterSetNames.ByName)
            {
                if (ShouldProcess($"{Project}/{Name}", "Delete Image"))
                {
                    Operation operation = Service.Images.Delete(Project, Name).Execute();
                    AddGlobalOperation(Project, operation);
                }
            }
            else if (ParameterSetName == ParameterSetNames.ByObject)
            {
                string project = GetProjectNameFromUri(Object.SelfLink);
                string imageName = Object.Name;

                if (ShouldProcess($"{project}/{imageName}", "Delete Image"))
                {
                    Operation operation = Service.Images.Delete(project, imageName).Execute();
                    AddGlobalOperation(project, operation);
                }
            }
            else
            {
                throw UnknownParameterSetException;
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Marks an image or schedules an image to be marked as DEPRECATED, OBSOLETE, or DELETED.
    /// </para>
    /// <para type="description">
    /// Marks an image or schedules an image to be marked as DEPRECATED, OBSOLETE, or DELETED.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Disable, "GceImage")]
    public class SetGceImageCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// Enum of available disabled states for instances.
        /// </summary>
        public enum ImageDisableState
        {
            /// <summary>
            /// Operations using a DEPRECATED image will return with a warning.
            /// </summary>
            DEPRECATED,

            /// <summary>
            /// Operations using an OBSOLETE image will result in an error.
            /// </summary>
            OBSOLETE,

            /// <summary>
            /// Operations using a DELETED image will result in an error. Deleted images are only marked
            /// deleted, and still require a request to remove them.
            /// </summary>
            DELETED
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the image to disable. Defaults to the gcloud config project.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the image to disable.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Image object that describes the image to disable.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Image Object { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Image object that is the suggested replacement for the image being disabled.
        /// </para>
        /// </summary>
        [Parameter]
        public Image Replacement { get; set; }

        /// <summary>
        /// <para type="description">
        /// The url of the image that is the suggested replacement for the image being disabled.
        /// </para>
        /// </summary>
        [Parameter]
        public string ReplacementUrl { get; set; }

        /// <summary>
        /// <para type="description">
        /// The new state of the image.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public ImageDisableState State { get; set; }

        /// <summary>
        /// <para type="description">
        /// The date to mark the image as deprecated.
        /// </para>
        /// </summary>
        [Parameter]
        public DateTimeOffset? DeprecateOn { get; set; }

        /// <summary>
        /// <para type="description">
        /// The date to mark the image as obsolete.
        /// </para>
        /// </summary>
        [Parameter]
        public DateTimeOffset? ObsoleteOn { get; set; }

        /// <summary>
        /// <para type="description">
        /// The date to mark the image as deleted. The image will only be marked, and not actually destroyed
        /// until a request is made to remove it.
        /// </para>
        /// </summary>
        [Parameter]
        public DateTimeOffset? DeleteOn { get; set; }

        protected override void ProcessRecord()
        {
            string project;
            string name;
            if (ParameterSetName == ParameterSetNames.ByName)
            {
                project = Project;
                name = Name;
            }
            else if (ParameterSetName == ParameterSetNames.ByObject)
            {
                project = GetProjectNameFromUri(Object.SelfLink);
                name = Object.Name;
            }
            else
            {
                throw UnknownParameterSetException;
            }

            DeprecationStatus body = new DeprecationStatus
            {
                Replacement = GetReplacementUrl(),
                State = State.ToString()
            };

            Operation operation = Service.Images.Deprecate(body, project, name).Execute();
            AddGlobalOperation(project, operation, () =>
            {
                WriteObject(Service.Images.Get(project, name).Execute());
            });
        }

        private string GetReplacementUrl()
        {
            if (ReplacementUrl != null && Replacement != null)
            {
                throw new PSInvalidOperationException("May not specify both Replacement and ReplacementUri.");
            }
            else if (Replacement != null)
            {
                return Replacement.SelfLink;
            }
            else if (ReplacementUrl != null)
            {
                return ReplacementUrl;
            }
            else
            {
                return null;
            }
        }
    }
}
