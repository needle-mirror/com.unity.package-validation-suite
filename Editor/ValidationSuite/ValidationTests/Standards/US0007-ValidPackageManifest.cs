using System.Text.RegularExpressions;
using Semver;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests.Standards
{
    class ValidPackageManifestUS0007 : BaseStandardChecker
    {
        public override string StandardCode => "US-0007";
        public override StandardVersion Version => new StandardVersion(1, 0, 2);

        const string UnityRegex = @"^[0-9]{4}\.[0-9]+$";
        const string UnityReleaseRegex = @"^[0-9]+[a|b|f]{1}[0-9]+$";
        const int MinDescriptionSize = 50;

        public void Check(ManifestData manifestData, ValidationType validationType)
        {
            // Check Description, make sure it's there, and not too short.
            if (manifestData.description.Length < MinDescriptionSize)
            {
                AddError($"In package.json, \"description\" is too short. Minimum Length = {MinDescriptionSize}. Current Length = {manifestData.description.Length}. {ErrorDocumentation.GetLinkMessage(ManifestValidation.k_DocsFilePath, "description-is-too-short")}");
            }

            // check unity field, if it's there
            if (!string.IsNullOrEmpty(manifestData.unity) && (manifestData.unity.Length > 6 || !Regex.Match(manifestData.unity, UnityRegex).Success))
            {
                AddError($"In package.json, \"unity\" is invalid. It should only be <MAJOR>.<MINOR> (e.g. 2018.4). Current unity = {manifestData.unity}. {ErrorDocumentation.GetLinkMessage(ManifestValidation.k_DocsFilePath, "unity-is-invalid")}");
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
            if (validationType == ValidationType.Promotion || validationType == ValidationType.CI)
            {
                if (!SemVersion.TryParse(manifestData.version, out var parsedVersion))
                {
                    AddError("Failed to extract major and minor version from \"version\" property in package.json.");
                    return;
                }
                var allowedDocumentationUrl = $"https://docs.unity3d.com/Packages/{manifestData.name}@{parsedVersion.Major}.{parsedVersion.Minor}/manual/index.html";

                // TODO: The standard says "The field documentationUrl is empty" but not "when running tests on CI, or during promotion"
                // Allow documentation URL to be set explicitly (PVS-101). Note that this is not currently allowed by the standard.
                if (!string.IsNullOrWhiteSpace(manifestData.documentationUrl) && manifestData.documentationUrl != allowedDocumentationUrl)
                {
                    AddError("In package.json, \"documentationUrl\" can't be used for Unity packages.  It is a features reserved for enterprise customers.  The Unity documentation team will ensure the package's documentation is published in the appropriate fashion");
                }
            }
            else
            {
                AddInformation("Skipping Git tags check as this is a package in development.");
            }
        }
    }
}
