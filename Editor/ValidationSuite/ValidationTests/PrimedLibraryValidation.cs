using System.IO;
using UnityEngine;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    class PrimedLibraryValidation : BaseValidation
    {
        static readonly string  k_DocsFilePath = "primed_library_validation_error.html";
        static readonly string k_LibraryPath = Path.Combine("ProjectData~", "Library");
        static readonly string[] k_PrimedLibraryPaths =
        {
            "ArtifactDB",
            "Artifacts",
            "SourceAssetDB",
        };

        public PrimedLibraryValidation()
        {
            TestName = "Primed Library Validation";
            TestDescription = "Validate that the Library directory of a template contains primed paths";
            TestCategory = TestCategory.DataValidation;
            SupportedValidations = new[] { ValidationType.CI, ValidationType.Promotion };
            SupportedPackageTypes = new[] { PackageType.Template };
            CanUseValidationExceptions = true;
            CanUseCompleteTestExceptions = true;
        }

        protected override void Run()
        {
            // Start by declaring victory
            TestState = TestState.Succeeded;

            ValidatePrimedLibrary();
        }

        void ValidatePrimedLibrary()
        {
            // Check that Library directory of template contains primed paths
            foreach (var primedLibraryPath in k_PrimedLibraryPaths)
            {
                var packageRelativePath = Path.Combine(k_LibraryPath, primedLibraryPath);
                var fullPath = Path.Combine(Context.PublishPackageInfo.path, packageRelativePath);

                if (!(File.Exists(fullPath) || Directory.Exists(fullPath)))
                {
                    var documentationLink = ErrorDocumentation.GetLinkMessage(
                        k_DocsFilePath, "template-is-missing-primed-library-path");
                    AddError($"Template is missing primed library path at {packageRelativePath}. " +
                        $"It should have been added automatically in the CI packing process. {documentationLink}");
                }
            }
        }
    }
}
