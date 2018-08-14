// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Collections;

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
    /// <example>
    ///   <code>PS C:\> Get-GceImage</code>
    ///   <para>Lists all the standard up to date images.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceImage -Family "window-2012-r2"</code>
    ///   <para>Gets the latest windows 2012 r2 image from the windows-cloud project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceImage "windows-server-2008-r2-dc-v20160719"</code>
    ///   <para>Gets the image named windows-server-2008-r2-dc-v20160719 from the windows-cloud project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceImage "my-image" -Project "my-project"</code>
    ///   <para>Gets the custom image named "my-image" from the private project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceImage -Project "my-project" -IncludeDeprecated</code>
    ///   <para>Lists all images in project "my-project", including images marked as deprecated.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/images#resource)">
    /// [Image resource definition]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/images)">
    /// [Google Cloud Platform images]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceImage", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(Image))]
    public class GetGceImageCmdlets : GceCmdlet
    {
        private static readonly string[] s_defaultProjects = {
                "centos-cloud", "coreos-cloud", "debian-cloud", "debian-cloud", "rhel-cloud",
                "suse-cloud", "ubuntu-os-cloud", "windows-cloud", "windows-sql-cloud"
            };

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
        /// The project that owns the image. This defaults to a standard set of public image projects.
        /// </para>
        /// </summary>
        [Parameter(Position = 1)]
        public new string[] Project { get; set; } = s_defaultProjects;

        /// <summary>
        /// <para type="description">
        /// If set, deprecated images will be included.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfProject)]
        public SwitchParameter IncludeDeprecated { get; set; }

        protected override void ProcessRecord()
        {
            IEnumerable<Image> images;
            switch (ParameterSetName)
            {
                case ParameterSetNames.OfProject:
                    images = GetAllProjectImages();
                    break;
                case ParameterSetNames.ByName:
                    images = GetImagesByProject($"No image named {Name} was found.",
                        (project) => Service.Images.Get(project, Name).Execute());
                    break;
                case ParameterSetNames.ByFamily:
                    images = GetImagesByProject($"No image of family {Family} was found.",
                        (project) => Service.Images.GetFromFamily(project, Family).Execute());
                    break;
                default:
                    throw UnknownParameterSetException;
            }
            WriteObject(images, true);
        }

        private IEnumerable<Image> GetImagesByProject(string exceptionMessage, Func<string, Image> getImage)
        {
            var images = new List<Image>();
            var exceptions = new List<Exception>();
            foreach (string project in Project)
            {
                try
                {
                    images.Add(getImage(project));
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
            if (images.Count == 0)
            {
                if (exceptions.Count == 1)
                {
                    throw exceptions[0];
                }
                else
                {
                    throw new AggregateException(exceptionMessage, exceptions);
                }
            }
            else
            {
                foreach (Exception e in exceptions)
                {
                    WriteVerbose(e.Message);
                }
                return images;
            }
        }

        private IEnumerable<Image> GetAllProjectImages()
        {
            var exceptions = new List<Exception>();
            var unfilteredImages = Enumerable.Empty<Image>();
            foreach (string project in Project)
            {
                try
                {
                    ImagesResource.ListRequest request = Service.Images.List(project);
                    do
                    {
                        ImageList response = request.Execute();
                        if (response.Items != null)
                        {
                            unfilteredImages = unfilteredImages.Concat(response.Items);
                        }
                        request.PageToken = response.NextPageToken;
                    } while (request.PageToken != null && !Stopping);
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            var filteredImages = unfilteredImages.Where(i => i.Deprecated == null);
            var images = IncludeDeprecated ? unfilteredImages : filteredImages;

            foreach (Image image in images)
            {
                yield return image;
            }

            if (exceptions.Count > 1)
            {
                throw new AggregateException("Errors occured for multiple projects", exceptions);
            }
            else if (exceptions.Count == 1)
            {
                throw exceptions[0];
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a Google Compute Engine image.
    /// </para>
    /// <para type="description">
    /// Creates a Google Compute Engine image from the given disk.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GceDisk "my-disk" | Add-GceImage -Name "my-image" -Family "my-family"</code>
    ///   <para>Creates a new image named "my-image" of the family "my-family" in the default project.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/images#resource)">
    /// [Image resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceImage")]
    [OutputType(typeof(Image))]
    public class AddGceImageCmdlet : GceConcurrentCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project that will own the image. This defaults to the gcloud config project.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

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
        /// The map of labels (key/value pairs) to be applied to the image.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public virtual Hashtable Label { get; set; }

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
                Labels = ConvertToDictionary<string, string>(Label),
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
    /// <example>
    ///   <code>PS C:\> Remove-GceImage "my-image"</code>
    ///   <para>Removes the image named "my-image" in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceImage -Project "my-project" | Remove-GceImage</code>
    ///   <para>Removes all images from project "my-project".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/images#resource)">
    /// [Image resource definition]
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
        public override string Project { get; set; }

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
    /// <example>
    ///   <code>
    ///   PS C:\> $image2 = Get-GceImage "my-new-image" -Project "my-project"
    ///   PS C:\> Disable-GceImage "my-old-image" -State DEPRECATED -Replacement $image2
    ///   </code>
    ///   <para>Marks the image named "my-old-image" as deprecated, and sets "my-new-image" as its replacement.</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> $image1 = Get-GceImage "my-old-image" -Project "my-project"
    ///   PS C:\> $image2 = Get-GceImage "my-new-image" -Project "my-project"
    ///   PS C:\> Disable-GceImage $image1 -State OBSOLETE -Replacement $image2
    ///   </code>
    ///   <para>Marks the image named "my-old-image" as obsolete, and sets "my-new-image" as its replacement.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/images#resource)">
    /// [Image resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Disable, "GceImage")]
    [OutputType(typeof(Image))]
    public class DisableGceImageCmdlet : GceConcurrentCmdlet
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
        public override string Project { get; set; }

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
