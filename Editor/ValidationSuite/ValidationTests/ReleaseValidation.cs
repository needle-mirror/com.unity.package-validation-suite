using System;
using System.Collections.Generic;
using System.IO;
using Semver;
using UnityEngine;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    internal class ReleaseValidation : BaseValidation
    {
        public ReleaseValidation()
        {
            TestName = "Release Validation";
            TestDescription = "Check if this release is allowed to be published, relative to existing versions of this package in the registry.";
            TestCategory = TestCategory.DataValidation;
            SupportedValidations = new[] { ValidationType.CI, ValidationType.LocalDevelopmentInternal };
        }

        protected override void Run()
        {
            TestState = TestState.Succeeded;

            if (!SemVersion.TryParse(Context.PublishPackageInfo.version, out var thisVersion, true))
            {
                AddError("Failed to parse package version \"{0}\"", Context.PublishPackageInfo.version);
                return;
            }
            
            var lastFullReleaseVersion = SemVersion.Parse(Context.PreviousPackageInfo != null ? Context.PreviousPackageInfo.version : "0.0.0");
            
            if (lastFullReleaseVersion.Major >= 1 || thisVersion.Major - lastFullReleaseVersion.Major <= 1) {
                return;
            }
            
            var message = "Invalid major version " + thisVersion + " when publishing to production registry.";
            if (lastFullReleaseVersion == "0.0.0")
            {
                message += " There has never been a full release of this package. The major must be 0 or 1.";
            }
            else
            {
                message += "The next release cannot be more than 1 major above the latest full release (" +
                           lastFullReleaseVersion + ").";
            }

            AddError(message + " {0}", ErrorDocumentation.GetLinkMessage("release_validation_error.html",  "invalid-major-release"));
        }
    }
}
