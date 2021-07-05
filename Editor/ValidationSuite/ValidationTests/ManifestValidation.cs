using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Semver;
using UnityEditor.PackageManager.ValidationSuite.ValidationTests.Standards;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    internal class ManifestValidation : BaseValidation
    {
        readonly SemVerCheckUS0005 semVerCheckUS0005 = new SemVerCheckUS0005();
        readonly PackageNamingConventionUS0006 packageNamingConventionUs0006 = new PackageNamingConventionUS0006();


        internal override List<IStandardChecker> ImplementedStandardsList =>
            new List<IStandardChecker>
        {
            semVerCheckUS0005,
            packageNamingConventionUs0006,
        };


        private const string UnityRegex = @"^[0-9]{4}\.[0-9]+$";
        private const string UnityReleaseRegex = @"^[0-9]+[a|b|f]{1}[0-9]+$";
        private static readonly int MinDescriptionSize = 50;
        internal static readonly int MaxDisplayNameLength = 50;
        internal static readonly string k_DocsFilePath = "manifest_validation_error.html";

        public ManifestValidation()
        {
            TestName = "Manifest Validation";
            TestDescription = "Validate that the information found in the manifest is well formatted.";
            TestCategory = TestCategory.DataValidation;
            SupportedValidations = new[] { ValidationType.CI, ValidationType.LocalDevelopment, ValidationType.LocalDevelopmentInternal, ValidationType.Promotion, ValidationType.VerifiedSet };
            //TODO: this validation contains many standards check, not sure if all of them should be run against FeatureSet
            //We might need to somehow split the checks into their own classes so we can filter on that.
            SupportedPackageTypes = new[] {PackageType.Template, PackageType.Tooling, PackageType.FeatureSet};
        }

        protected override void Run()
        {
            // Start by declaring victory
            TestState = TestState.Succeeded;

            var manifestData = Context.ProjectPackageInfo;
            if (manifestData == null)
            {
                AddError("Manifest not available. Not validating manifest contents.");
                return;
            }

            ValidateManifestMarshalling(manifestData);
            ValidateManifestData(manifestData);
            packageNamingConventionUs0006.Check(manifestData.name, manifestData.displayName);
            ValidateAuthor(manifestData);
            semVerCheckUS0005.Check(manifestData.version);
            ValidateDependencies();
        }

        private void ValidateManifestMarshalling(ManifestData manifestData)
        {
            foreach (var marshallError in manifestData.decodingErrors)
            {
                AddError(marshallError.Message);
            }
        }

        private void ValidateDependencies()
        {
            // Check if there are dependencies to check and exit early
            if (Context.ProjectPackageInfo.dependencies.Count == 0)
                return;

            var isFeature = Context.ProjectPackageInfo.PackageType == PackageType.FeatureSet;

            // Make sure all dependencies are already published in production.
            foreach (var dependency in Context.ProjectPackageInfo.dependencies)
            {
                if (isFeature && dependency.Value != "default")
                {
                    AddError(@"In package.json for a feature, dependency ""{0}"" : ""{1}"" needs to be set to ""default""", dependency.Key, dependency.Value);
                    continue;
                }

                // Check if the dependency semver is valid before doing anything else
                SemVersion depVersion;
                if (!isFeature && !SemVersion.TryParse(dependency.Value, out depVersion))
                {
                    AddError(@"In package.json, dependency ""{0}"" : ""{1}"" needs to be a valid ""Semver"". {2}", dependency.Key, dependency.Value, ErrorDocumentation.GetLinkMessage(ManifestValidation.k_DocsFilePath, "dependency_needs_to_be_a_valid_Semver"));
                    continue;
                }

                var dependencyInfo = Utilities.UpmListOffline(dependency.Key).FirstOrDefault();

                // Built in packages are shipped with the editor, and will therefore never be published to production.
                if (dependencyInfo != null && dependencyInfo.source == PackageSource.BuiltIn)
                {
                    continue;
                }

                // Check if this package's dependencies are in production. That is a requirement for promotion.
                // make sure to check the version actually resolved by upm and not the one necessarily listed by the package
                // If the packageId is included in Context.packageIdsForPromotion then we can skip this check, as the package
                // is expected to be promoted by another process
                var version = dependencyInfo != null ? dependencyInfo.version : dependency.Value;
                var packageId = Utilities.CreatePackageId(dependency.Key, version);

                if (Context.ValidationType != ValidationType.VerifiedSet && (Context.packageIdsForPromotion == null || Context.packageIdsForPromotion.Length < 1 || !Context.packageIdsForPromotion.Contains(packageId)) && !Utilities.PackageExistsOnProduction(packageId))
                {
                    // ignore if  package is part of the context already
                    if (Context.ValidationType == ValidationType.Promotion || Context.ValidationType == ValidationType.AssetStore)
                        AddError("Package dependency {0} is not promoted in production. {1}", packageId, ErrorDocumentation.GetLinkMessage(ManifestValidation.k_DocsFilePath,  "package-dependency-[packageID]-is-not-published-in-production"));
                    else
                        AddWarning("Package dependency {0} must be promoted to production before this package is promoted to production. (Except for core packages)", packageId);
                }

                // only check this in CI or internal local development
                // Make sure the dependencies I ask for that exist in the project have the good version
                if (Context.ValidationType == ValidationType.CI || Context.ValidationType == ValidationType.LocalDevelopmentInternal)
                {
                    PackageInfo packageInfo = Utilities.UpmListOffline(dependency.Key).FirstOrDefault();
                    if (packageInfo != null && packageInfo.version != dependency.Value)
                    {
                        AddWarning("Package {2} depends on {0}, which is found locally but with another version. To remove this warning, in the package.json file of {2}, change the dependency of {0}@{1} to {0}@{3}.", dependency.Key, dependency.Value, Context.ProjectPackageInfo.name, packageInfo.version);
                    }
                }
            }

            // TODO: Validate the Package dependencies meet the minimum editor requirement (eg: 2018.3 minimum for package A is 2, make sure I don't use 1)
        }

        private void ValidateManifestData(ManifestData manifestData)
        {
            // Check Description, make sure it's there, and not too short.
            if (manifestData.description.Length < MinDescriptionSize)
            {
                AddError("In package.json, \"description\" is too short. Minimum Length = {0}. Current Length = {1}. {2}", MinDescriptionSize, manifestData.description.Length, ErrorDocumentation.GetLinkMessage(ManifestValidation.k_DocsFilePath,  "description-is-too-short"));
            }

            // check unity field, if it's there
            if (!string.IsNullOrEmpty(manifestData.unity) && (manifestData.unity.Length > 6 || !Regex.Match(manifestData.unity, UnityRegex).Success))
            {
                AddError($"In package.json, \"unity\" is invalid. It should only be <MAJOR>.<MINOR> (e.g. 2018.4). Current unity = {manifestData.unity}. {ErrorDocumentation.GetLinkMessage(ManifestValidation.k_DocsFilePath,  "unity-is-invalid")}");
            }

            // check unityRelease field, if it's there
            if (!string.IsNullOrEmpty(manifestData.unityRelease))
            {
                // it should be valid
                if (!Regex.Match(manifestData.unityRelease, UnityReleaseRegex).Success)
                {
                    AddError(
                        $"In package.json, \"unityRelease\" is invalid. Current unityRelease = {manifestData.unityRelease}. {ErrorDocumentation.GetLinkMessage(ManifestValidation.k_DocsFilePath, "unityrelease-is-invalid")}");
                }

                // it should be accompanied of a unity field
                if (string.IsNullOrEmpty(manifestData.unity))
                {
                    AddError(
                        $"In package.json, \"unityRelease\" needs a \"unity\" field to be used. {ErrorDocumentation.GetLinkMessage(ManifestValidation.k_DocsFilePath, "unityrelease-without-unity")}");
                }
            }

            // check documentation url field
            if (Context.ValidationType == ValidationType.Promotion || Context.ValidationType == ValidationType.CI)
            {
                if (!string.IsNullOrWhiteSpace(manifestData.documentationUrl))
                {
                    AddError("In package.json, \"documentationUrl\" can't be used for Unity packages.  It is a features reserved for enterprise customers.  The Unity documentation team will ensure the package's documentation is published in the appropriate fashion");
                }

                // Check if `repository.url` and `repository.revision` exist and the content is valid
                string value;
                if (!manifestData.repository.TryGetValue("url", out value) || string.IsNullOrEmpty(value))
                    AddError("In package.json for a published package, there must be a \"repository.url\" field. {0}", ErrorDocumentation.GetLinkMessage(ManifestValidation.k_DocsFilePath,  "for_a_published_package_there_must_be_a_repository.url_field"));
                if (!manifestData.repository.TryGetValue("revision", out value) || string.IsNullOrEmpty(value))
                    AddError("In package.json for a published package, there must be a \"repository.revision\" field. {0}", ErrorDocumentation.GetLinkMessage(ManifestValidation.k_DocsFilePath,  "for_a_published_package_there_must_be_a_repository.revision_field"));
            }
            else
            {
                AddInformation("Skipping Git tags check as this is a package in development.");
            }
        }

        private void ValidateAuthor(ManifestData manifestData)
        {
            if (manifestData.IsAuthoredByUnity())
            {
                ValidateUnityAuthor(manifestData);
            }
            else
            {
                ValidateNonUnityAuthor(manifestData);
            }
        }

        private void ValidateUnityAuthor(ManifestData manifestData)
        {
            if (manifestData.author != null)
            {
                // make sure it is not present in order to have a unified presentation of the author for all of our packages
                AddError("A Unity package must not have an author field. Please remove the field. {0}",
                    ErrorDocumentation.GetLinkMessage(ManifestValidation.k_DocsFilePath,
                        "a_unity_package_must_not_have_an_author_field"));
            }
        }

        // the author field is required for non-unity packages
        private void ValidateNonUnityAuthor(ManifestData manifestData)
        {
            // if authordetails is set, then author == ""
            if (String.IsNullOrEmpty(manifestData.author) && manifestData.authorDetails == null)
            {
                AddError(
                    "The `author` field is mandatory. Please add an `author` field in your package.json file",
                    ErrorDocumentation.GetLinkMessage(ManifestValidation.k_DocsFilePath, "author_is_mandatory"));
                return;
            }

            if (!String.IsNullOrEmpty(manifestData.author) && manifestData.authorDetails == null)
            {
                return; // valid
            }

            // non unity packages should have either a string or AuthorDetails { name: ""*, email: "", url: ""}
            if (String.IsNullOrEmpty(manifestData.authorDetails.name))
            {
                AddError(
                    "Invalid `author` field. The `author` field in your package.json file can be a string or an object ( name, email, url ), where `name` is mandatory. {0}",
                    ErrorDocumentation.GetLinkMessage(ManifestValidation.k_DocsFilePath, "author_is_invalid"));
            }
        }
    }
}
