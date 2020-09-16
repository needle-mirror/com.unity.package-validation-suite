using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    internal class TemplateProjectManifestValidation : BaseValidation
    {
        private readonly string[] _allowedFields =
        {
            "dependencies"
        };
        
        internal static readonly string docsFilePath = "template_project_manifest_validation_errors.html";

        public TemplateProjectManifestValidation()
        {
            TestName = "Template Project Manifest Validation";
            TestDescription = "Validate that the project manifest of a template package follows standards";
            TestCategory = TestCategory.DataValidation;
            SupportedPackageTypes = new[] { PackageType.Template };
        }

        protected override void Run()
        {
            TestState = TestState.Succeeded;
            
            ValidateFields();
        }

        // Generate a standard error message for project manifest field checks. This is also used during tests
        internal string CreateFieldErrorMessage(string fieldName)
        {
            string docsLink = ErrorDocumentation.GetLinkMessage(docsFilePath,
                "The-{fieldName}-field-in-the-project-manifest-is-not-a-valid-field-for-template-packages");
            
            return
                $"The `{fieldName}` field in the project manifest is not a valid field for template packages. Please remove this field from {Context.ProjectManifestPath}. {docsLink}";
        }

        private void ValidateFields()
        {
            foreach (string fieldName in Context.ProjectManifestKeys)
            {
                if (_allowedFields.Contains(fieldName))
                    continue;
                
                AddError(CreateFieldErrorMessage(fieldName));
            }
        }
    }
}
