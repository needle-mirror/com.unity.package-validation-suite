using System.Linq;
using UnityEditor.PackageManager.ValidationSuite.Utils;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    internal class PackageManagerSettingsValidation
    {
        public bool m_EnablePreviewPackages;
        public bool m_EnablePreReleasePackages;
    }
    
    internal class TemplateProjectValidation : BaseValidation
    {
        private readonly string[] _allowedFields =
        {
            "dependencies"
        };

        internal static readonly string k_DocsFilePath = "template_project_validation_errors.html";
        
        public TemplateProjectValidation()
        {
            TestName = "Template Project Manifest Validation";
            TestDescription = "Validate that the project manifest of a template package follows standards";
            TestCategory = TestCategory.DataValidation;
            SupportedPackageTypes = new[] { PackageType.Template };
            CanUseValidationExceptions = true;
        }

        protected override void Run()
        {
            TestState = TestState.Succeeded;
            
            ValidateProjectManifest();
            ValidatePackageManagerSettings();
        }

        /// <summary>
        /// For templates, we want to make sure that Show Preview Packages or Show Pre-Release packages
        /// are not enabled.
        /// For <=2019.4 this is saved on the profile preferences and is not bound to be
        /// pre-enabled. For >2019.4 this is saved in ProjectSettings/PackageManagerSettings.asset
        /// </summary>
        private void ValidatePackageManagerSettings()
        {
            if (Context.ProjectInfo.PackageManagerSettingsValidation == null)
                return;

            if (Context.ProjectInfo.PackageManagerSettingsValidation.m_EnablePreviewPackages)
                AddError($"Preview packages are not allowed to be enabled on template packages. Please disable the `Enable Preview Packages` option in ProjectSettings > PackageManager > Advanced Settings. {ErrorDocumentation.GetLinkMessage(k_DocsFilePath, "preview|prerelease-packages-are-not-allowed-to-be-enabled-on-template-packages")}");
    
            if (Context.ProjectInfo.PackageManagerSettingsValidation.m_EnablePreReleasePackages)
                AddError($"PreRelease packages are not allowed to be enabled on template packages. Please disable the `Enable PreRelease Packages` option in ProjectSettings > PackageManager > Advanced Settings. {ErrorDocumentation.GetLinkMessage(k_DocsFilePath, "preview|prerelease-packages-are-not-allowed-to-be-enabled-on-template-packages")}");
        }

        // Generate a standard error message for project manifest field checks. This is also used during tests
        internal string CreateFieldErrorMessage(string fieldName)
        {
            string docsLink = ErrorDocumentation.GetLinkMessage(k_DocsFilePath,
                "The-{fieldName}-field-in-the-project-manifest-is-not-a-valid-field-for-template-packages");
            
            return $"The `{fieldName}` field in the project manifest is not a valid field for template packages. Please remove this field from {Context.ProjectInfo.ManifestPath}. {docsLink}";
        }

        private void ValidateProjectManifest()
        {
            foreach (string fieldName in Context.ProjectInfo.ProjectManifestKeys)
            {
                if (_allowedFields.Contains(fieldName))
                    continue;
                
                AddError(CreateFieldErrorMessage(fieldName));
            }
        }
    }
}
