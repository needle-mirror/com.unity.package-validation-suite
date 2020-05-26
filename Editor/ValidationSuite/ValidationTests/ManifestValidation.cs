using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Semver;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    internal class ManifestValidation : BaseValidation
    {
        private string[] PackageNamePrefixList = { "com.unity.", "com.autodesk.", "com.havok.", "com.ptc." };
        private const string UpmRegex = @"^[a-z0-9][a-z0-9-._]{0,213}$";
        private const string UpmDisplayRegex = @"^[a-zA-Z0-9 ]+$";
        private const string UnityRegex = @"^[0-9]{4}\.[0-9]+$";
        private const string UnityReleaseRegex = @"^[0-9]+[a|b|f]{1}[0-9]+$";
        internal static readonly int MinDescriptionSize = 50;
        internal static readonly int MaxDisplayNameLength = 50;
        internal static readonly string docsFilePath = "manifest_validation_error.html";

        public ManifestValidation()
        {
            TestName = "Manifest Validation";
            TestDescription = "Validate that the information found in the manifest is well formatted.";
            TestCategory = TestCategory.DataValidation;
            SupportedValidations = new[] { ValidationType.CI, ValidationType.LocalDevelopment, ValidationType.LocalDevelopmentInternal, ValidationType.Promotion, ValidationType.VerifiedSet };
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

            ValidateManifestData(manifestData);
            ValidateVersion(manifestData);

            ValidateDependencies();
        }

        private void ValidateVersion(ManifestData manifestData)
        {
            SemVersion version;
            // Check package version, make sure it's a valid SemVer string.
            if (!SemVersion.TryParse(manifestData.version, out version))
            {
                AddError("In package.json, \"version\" needs to be a valid \"Semver\". {0}", ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "version-needs-to-be-a-valid-semver"));
            }
        }

        private void ValidateDependencies()
        {
            // Check if there are dependencies to check and exit early
            if (Context.ProjectPackageInfo.dependencies.Count == 0)
                return;

            // Make sure all dependencies are already published in production.
            foreach (var dependency in Context.ProjectPackageInfo.dependencies)
            {
                // Check if the dependency semver is valid before doing anything else
                SemVersion depVersion;
                if (!SemVersion.TryParse(dependency.Value, out depVersion)) {
                    AddError(@"In package.json, dependency ""{0}"" : ""{1}"" needs to be a valid ""Semver"". {2}", dependency.Key, dependency.Value, ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "dependency_needs_to_be_a_valid_Semver"));
                    continue;
                }

                var packageId = Utilities.CreatePackageId(dependency.Key, dependency.Value);

                var dependencyInfo = Utilities.UpmListOffline(dependency.Key).FirstOrDefault();

                // Built in packages are shipped with the editor, and will therefore never by published to production.
                if (dependencyInfo != null && dependencyInfo.source == PackageSource.BuiltIn)
                {
                    continue;
                }

                // Check if this package's dependencies are in production. That is a requirement for promotion.
                if (Context.ValidationType != ValidationType.VerifiedSet && !Utilities.PackageExistsOnProduction(packageId))
                {
                    if (Context.ValidationType == ValidationType.Promotion || Context.ValidationType == ValidationType.AssetStore)
                        AddError("Package dependency {0} is not published in production. {1}", packageId, ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "package-dependency-[packageID]-is-not-published-in-production"));
                    else
                        AddWarning("Package dependency {0} must be published to production before this package is published to production. (Except for core packages)", packageId);
                }

                // only check this in CI or internal local development
                // Make sure the dependencies I ask for that exist in the project have the good version
                if (Context.ValidationType == ValidationType.CI || Context.ValidationType == ValidationType.LocalDevelopmentInternal) {
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
            // Check the package Name, which needs to start with one of the approved company names.
            // This should probably be executed only in internal development, CI and Promotion contexts
            if (!PackageNamePrefixList.Any(namePrefix => (manifestData.name.StartsWith(namePrefix) && manifestData.name.Length > namePrefix.Length)))
            {
                AddError("In package.json, \"name\" needs to start with one of these approved company names: {0}. {1}", string.Join(", ", PackageNamePrefixList), ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "name-needs-to-start-with-one-of-these-approved-company-names"));
            }

            // There cannot be any capital letters in package names.
            if (manifestData.name.ToLower(CultureInfo.InvariantCulture) != manifestData.name)
            {
                AddError("In package.json, \"name\" cannot contain capital letters. {0}", ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "name-cannot-contain-capital-letters"));
            }

            // Check name against our regex.
            Match match = Regex.Match(manifestData.name, UpmRegex);
            if (!match.Success)
            {
                AddError("In package.json, \"name\" is not a valid name. {0}", ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "name-is-not-a-valid-name"));
            }

			// Package name cannot end with .framework, .plugin or .bundle.
            String[] strings = { ".framework", ".bundle", ".plugin" };
			foreach (var value in strings) {
 				if (manifestData.name.EndsWith(value)) {
					AddError("In package.json, \"name\" cannot end with .plugin, .bundle or .framework. {0}", ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "name-cannot-end-with"));
				}
			} 

            if (string.IsNullOrEmpty(manifestData.displayName))
            {
                AddError("In package.json, \"displayName\" must be set. {0}", ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "displayName-must-be-set"));
            }
            else if (manifestData.displayName.Length > MaxDisplayNameLength)
            {
                AddError("In package.json, \"displayName\" is too long. Max Length = {0}. Current Length = {1}. {2}", MaxDisplayNameLength, manifestData.displayName.Length, ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "displayName-is-too-long"));
            }
            else if (!Regex.Match(manifestData.displayName, UpmDisplayRegex).Success)
            {
                AddError("In package.json, \"displayName\" cannot have any special characters. {0}", ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "displayName-cannot-have-any-special-characters"));
            }

            // Check Description, make sure it's there, and not too short.
            if (manifestData.description.Length < MinDescriptionSize)
            {
                AddError("In package.json, \"description\" is too short. Minimum Length = {0}. Current Length = {1}. {2}", MinDescriptionSize, manifestData.description.Length, ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "description-is-too-short"));
            }
            
            //check unity field, if it's there
            if (!string.IsNullOrEmpty(manifestData.unity) && (manifestData.unity.Length > 6 || !Regex.Match(manifestData.unity, UnityRegex).Success))
            {
                AddError($"In package.json, \"unity\" is invalid. It should only be <MAJOR>.<MINOR> (e.g. 2018.4). Current unity = {manifestData.unity}. {ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "unity-is-invalid")}");
            }
            
            //check unityRelease field, if it's there
            if (!string.IsNullOrEmpty(manifestData.unityRelease))
            {
                // it should be valid
                if (!Regex.Match(manifestData.unityRelease, UnityReleaseRegex).Success)
                {
                    AddError(
                        $"In package.json, \"unityRelease\" is invalid. Current unityRelease = {manifestData.unityRelease}. {ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath, "unityrelease-is-invalid")}");
                }
                
                // it should be accompanied of a unity field
                if (string.IsNullOrEmpty(manifestData.unity))
                {
                    AddError(
                        $"In package.json, \"unityRelease\" needs a \"unity\" field to be used. {ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath, "unityrelease-without-unity")}");
                }
            }


            if (Context.ValidationType == ValidationType.Promotion || Context.ValidationType == ValidationType.CI)
            {
                if (!string.IsNullOrWhiteSpace(manifestData.documentationUrl))
                {
                    AddError("In package.json, \"documentationUrl\" can't be used for Unity packages.  It is a features reserved for enterprise customers.  The Unity documentation team will ensure the package's documentation is published in the appropriate fashion");
                }

                // Check if `repository.url` and `repository.revision` exist and the content is valid
                string value;
                if (!manifestData.repository.TryGetValue("url", out value) || string.IsNullOrEmpty(value))
                    AddError("In package.json for a published package, there must be a \"repository.url\" field. {0}", ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "for_a_published_package_there_must_be_a_repository.url_field"));
                if (!manifestData.repository.TryGetValue("revision", out value) || string.IsNullOrEmpty(value))
                    AddError("In package.json for a published package, there must be a \"repository.revision\" field. {0}", ErrorDocumentation.GetLinkMessage(ManifestValidation.docsFilePath,  "for_a_published_package_there_must_be_a_repository.revision_field"));
            }
            else
            {
                AddInformation("Skipping Git tags check as this is a package in development.");
            }
        }
    }
}
