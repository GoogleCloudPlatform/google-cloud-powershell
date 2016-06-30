using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using NodaTime;
using NodaTime.Text;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets information about google compute engine images.
    /// </para>
    /// <para type="description">
    /// Gets information about google compute engine images.
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
        /// The name of either the image to get, or the image family to get the latest of.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByFamily, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// When set, gets the latest image of a family.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByFamily, Mandatory = true)]
        public SwitchParameter Family { get; set; }

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
                    WriteObject(Service.Images.GetFromFamily(Project, Name).Execute());
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

    [Cmdlet(VerbsCommon.Add, "GceImage")]
    public class AddGceImageCmdlet : GceConcurrentCmdlet
    {
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        public Disk SourceDisk { get; set; }

        [Parameter]
        public string Name { get; set; }

        [Parameter]
        public string Family { get; set; }

        [Parameter]
        public string Description { get; set; }

        protected override void ProcessRecord()
        {
            Image body = new Image
            {
                SourceDisk = SourceDisk.SelfLink,
                Licenses = SourceDisk.Licenses,
                DiskSizeGb = SourceDisk.SizeGb,
                Name = Name ?? SourceDisk.Name,
                Description = Description ?? SourceDisk.Description,
                Family = Family
            };
            Operation operation = Service.Images.Insert(body, Project).Execute();
            AddGlobalOperation(Project, operation);
        }
    }

    [Cmdlet(VerbsCommon.Remove, "GceImage")]
    public class RemoveGceImageCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }


        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Image Object { get; set; }

        protected override void ProcessRecord()
        {
            if (ParameterSetName == ParameterSetNames.ByName)
            {
                Operation operation = Service.Images.Delete(Project, Name).Execute();
                AddGlobalOperation(Project, operation);
            }
            else if (ParameterSetName == ParameterSetNames.ByObject)
            {
                string project = GetProjectNameFromUri(Object.SelfLink);
                string imageName = Object.Name;
                Operation operation = Service.Images.Delete(project, imageName).Execute();
                AddGlobalOperation(project, operation);
            }
        }
    }

    [Cmdlet(VerbsLifecycle.Disable, "GceImage")]
    public class SetGceImageCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        public enum StateEnum
        {
            DEPRECATED,
            OBSOLETE,
            DELETED
        }

        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }
        
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public Image Object { get; set; }

        [Parameter]
        public Image Replacement { get; set; }

        [Parameter]
        public StateEnum? State { get; set; }

        [Parameter]
        public string ReplacementUrl { get; set; }

        [Parameter]
        public LocalDateTime? ObsoleteOn { get; set; }

        [Parameter]
        public Duration? ObsoleteIn { get; set; }

        [Parameter]
        public LocalDateTime? DeprecateOn { get; set; }

        [Parameter]
        public Duration? DeprecateIn { get; set; }

        [Parameter]
        public LocalDateTime? DeleteOn { get; set; }

        [Parameter]
        public Duration? DeleteIn { get; set; }

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
                State = State?.ToString(),
                Deprecated = GetDateString(DeprecateOn, DeprecateIn, "Deprecate"),
                Obsolete = GetDateString(ObsoleteOn, ObsoleteIn, "Obsolete"),
                Deleted = GetDateString(DeleteOn, DeleteIn, "Delete")
            };

            Operation operation = Service.Images.Deprecate(body, project, name).Execute();
            AddGlobalOperation(project, operation);
        }

        private static string GetDateString(LocalDateTime? onDateTime, Duration? inDuration, string type)
        {
            if (onDateTime != null && inDuration != null)
            {
                throw new PSInvalidOperationException($"May not specify both {type}On and {type}In");
            }
            else if (onDateTime != null)
            {
                return InstantPattern.GeneralPattern.Format(onDateTime.Value.InUtc().ToInstant());
            }
            else if (inDuration != null)
            {
                Instant deleteOn = SystemClock.Instance.Now + inDuration.Value;
                return InstantPattern.GeneralPattern.Format(deleteOn);
            }
            else
            {
                return null;
            }
        }

        private string GetReplacementUrl()
        {
            if (ReplacementUrl == null && Replacement == null)
            {
                throw new PSInvalidOperationException("Must specify either Replacement or ReplacementUri.");
            }
            else if (ReplacementUrl != null && Replacement != null)
            {
                throw new PSInvalidOperationException("May not specify both Replacement and ReplacementUri");
            }
            else if (Replacement != null)
            {
                return Replacement.SelfLink;
            }
            else
            {
                return ReplacementUrl;
            }
        }
    }
}
