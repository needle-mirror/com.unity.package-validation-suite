using System;
using System.Collections.Generic;
using System.Linq;
using Semver;
using UnityEditor.PackageManager.ValidationSuite.Utils;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    internal class LifecycleValidation : BaseValidation
    {
        internal static readonly string docsFilePath = "lifecycle_validation_error.html";

        public LifecycleValidation()
        {
            TestName = "Package Lifecycle Validation";
            TestDescription = "Validate that the package respects the lifecycle transition guidelines.";
            TestCategory = TestCategory.DataValidation;
            SupportedValidations = new[] { ValidationType.CI, ValidationType.LocalDevelopment, ValidationType.LocalDevelopmentInternal, ValidationType.Promotion, ValidationType.VerifiedSet };
        }

        protected override void Run()
        {
            TestState = TestState.Succeeded;

            if (Context.PublishPackageInfo.lifecycle == 1.0)
            {
                ValidateVersion(Context.PublishPackageInfo, LifecycleV1VersionValidator);
            } else {
                AddError(@"2021.1 Packages are not supported yet! We are working on transitioning to the package lifecycle version 2 in 2021.1, and the minimum required parts aren't ready.  Until further notice, please ensure the unity field in your package's package.json file is less than 2021.1.");

                ValidateVersion(Context.PublishPackageInfo, LifecycleV2VersionValidator);
                ValidateDependenciesLifecyclePhase(Context.ProjectPackageInfo.dependencies);
            }
        }

        private void ValidateVersion(ManifestData manifestData, Action<SemVersion, VersionTag> lifecycleVersionValidator)
        {
            // Check package version, make sure it's a valid SemVer string.
            SemVersion packageVersionNumber;
            if (!SemVersion.TryParse(manifestData.version, out packageVersionNumber))
            {
                AddError("In package.json, \"version\" needs to be a valid \"Semver\". {0}", ErrorDocumentation.GetLinkMessage(docsFilePath,  "version-needs-to-be-a-valid-semver"));
                return;
            }

            VersionTag versionTag;

            try
            {
                versionTag = VersionTag.Parse(packageVersionNumber.Prerelease);
            }
            catch (ArgumentException e)
            {
                AddError("In package.json, \"version\" doesn't follow our lifecycle rules. {0}. {1}", e.Message, ErrorDocumentation.GetLinkMessage(docsFilePath, "version-is-invalid-tag-must-follow-lifecycle-rules"));
                return;
            }

            lifecycleVersionValidator(packageVersionNumber, versionTag);
        }

        private void LifecycleV1VersionValidator(SemVersion packageVersionNumber, VersionTag versionTag)
        {
            if (Context.IsCore && (!versionTag.IsEmpty() || packageVersionNumber.Major < 1))
            {
                AddError("Core packages cannot be preview. " + ErrorDocumentation.GetLinkMessage(docsFilePath, "core-packages-cannot-be-preview"));
                return;
            }

            if (packageVersionNumber.Major < 1 && (versionTag.IsEmpty() || versionTag.Tag != "preview"))
            {
                AddError("In package.json, \"version\" < 1, please tag the package as " + packageVersionNumber.VersionOnly() + "-preview. " + ErrorDocumentation.GetLinkMessage(docsFilePath, "version-1-please-tag-the-package-as-xxx-preview"));
                return;
            }

            // The only pre-release tag we support is -preview
            if (!versionTag.IsEmpty() && !(versionTag.Tag == "preview" && versionTag.Feature == "" &&
                                              versionTag.Iteration <= 999))
            {

                AddError(
                    "In package.json, \"version\": the only pre-release filter supported is \"-preview.[num < 999]\". " + ErrorDocumentation.GetLinkMessage(docsFilePath, "version-the-only-pre-release-filter-supported-is--preview-num-999"));
            }
        }

        private void LifecycleV2VersionValidator(SemVersion packageVersionNumber, VersionTag versionTag)
        {
            if (versionTag.IsEmpty()) return;

            if (versionTag.Tag == "preview")
            {
                AddError("In package.json, \"version\" cannot be tagged \"preview\" in lifecycle v2, please use \"exp\". " + ErrorDocumentation.GetLinkMessage(ErrorTypes.InvalidLifecycleV2));
                return;
            }

            if (packageVersionNumber.Major < 1)
            {
                AddError("In package.json, \"version\" cannot be tagged \"" + packageVersionNumber.Prerelease + "\" while the major version less than 1. " + ErrorDocumentation.GetLinkMessage(ErrorTypes.InvalidLifecycleV2));
                return;
            }

            if (versionTag.Tag != "exp" && versionTag.Tag != "pre" && versionTag.Tag != "rc")
            {
                AddError("In package.json, \"version\" must be a valid tag. \"" + versionTag.Tag + "\" is invalid, try either \"pre\", \"rc\" or \"exp\". " + ErrorDocumentation.GetLinkMessage(ErrorTypes.InvalidLifecycleV2));
                return;
            }

            if (versionTag.Tag != "exp" && versionTag.Feature != "")
            {
                AddError("In package.json, \"version\" must be a valid tag. Custom tag \"" + versionTag.Feature + "\" only allowed with \"exp\". " + ErrorDocumentation.GetLinkMessage(ErrorTypes.InvalidLifecycleV2));
                return;
            }

            if (versionTag.Tag == "exp" && versionTag.Feature.Length > 10)
            {
                AddError("In package.json, \"version\" must be a valid tag. Custom tag \"" + versionTag.Feature + "\" is too long, must be 10 characters long or less. " + ErrorDocumentation.GetLinkMessage(ErrorTypes.InvalidLifecycleV2));
                return;
            }

            if (versionTag.Iteration < 1)
            {
                AddError("In package.json, \"version\" must be a valid tag. Iteration is required to be 1 or greater. " + ErrorDocumentation.GetLinkMessage(ErrorTypes.InvalidLifecycleV2));
                return;
            }

        }

        private void ValidateDependenciesLifecyclePhase(Dictionary<string, string> dependencies)
        {
            // No dependencies, exit early
            if (!dependencies.Any()) return;

            // Extract the current track, since otherwise we'd be potentially parsing the version
            // multiple times
            var currentTrack = Context.ProjectPackageInfo.LifecyclePhase;

            var supportedVersions = PackageLifecyclePhase.GetPhaseSupportedVersions(currentTrack);

            // Check each dependency against supported versions
            foreach (var dependency in dependencies)
            {
                // Skip invalid dependencies from this check
                SemVersion depVersion;
                if (!SemVersion.TryParse(dependency.Value, out depVersion)) continue;

                LifecyclePhase dependencyTrack = PackageLifecyclePhase.GetLifecyclePhase(dependency.Value.ToLower());
                var depId = Utilities.CreatePackageId(dependency.Key, dependency.Value);
                if (!supportedVersions.HasFlag(dependencyTrack))
                    AddError("Package {0} depends on package {1} which is in an invalid track for release purposes. {2} versions can only depend on {3} versions. {4}", Context.ProjectPackageInfo.Id, depId, currentTrack, supportedVersions.ToString(), ErrorDocumentation.GetLinkMessage(docsFilePath, "package_depends_on_a_package_which_is_in_an_invalid_track_for_release_purposes"));
            }
        }
    }
}
