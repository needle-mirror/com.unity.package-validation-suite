using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using PureFileValidationPvp;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    [UsedImplicitly]
    class PureFileValidation : BaseValidation
    {
        static readonly Dictionary<string, string> k_ChecksAppliedInLegacyPVS = new Dictionary<string, string>
        {
            ["PVP-26-1"] = "Asset files and .meta files must correspond",
            ["PVP-62-1"] = "index.md filename must be spelled in lowercase",
            ["PVP-107-1"] = "Manifest contains only permitted properties",
        };

        static PureFileValidation()
        {
            if (k_ChecksAppliedInLegacyPVS.Keys.Except(Validator.Checks).Count() != 0)
            {
                throw new InvalidOperationException("Trying to enforce non-existing PFV check in legacy PVS");
            }
        }

        // Indirection to support PVS's own test suite.
        readonly Dictionary<string, string> m_AppliedChecks;
        readonly Validator m_Validator = new Validator();

        public PureFileValidation()
        {
            TestName = "Pure File Validations";
            TestDescription = "Assorted requirements on package file contents.";
            TestCategory = TestCategory.ContentScan;

            // Don't apply these new PFV checks in VerifiedSet (APV) context, for compatibility with old vendored PVS versions.
            SupportedValidations = new[] { ValidationType.CI, ValidationType.LocalDevelopment, ValidationType.LocalDevelopmentInternal, ValidationType.Promotion };

            m_AppliedChecks = k_ChecksAppliedInLegacyPVS;
        }

        internal PureFileValidation(params string[] appliedChecks)
            : this()
        {
            m_AppliedChecks = new Dictionary<string, string>();
            foreach (var check in appliedChecks)
            {
                m_AppliedChecks[check] = "error";
            }
        }

        protected override void Run()
        {
            TestState = TestState.Succeeded;

            var manifestPath = Context.ProjectPackageInfo.path;
            // Sometimes in PVS, this is the path to package.json, while at
            // other times, it is the path to the package DIRECTORY.
            if (manifestPath.EndsWith("package.json"))
            {
                manifestPath = manifestPath.Substring(0, manifestPath.Length - "package.json".Length);
            }
            var package = new FileSystemPackage(manifestPath);

            m_Validator.Validate(package, (checkId, error) =>
            {
                if (m_AppliedChecks.TryGetValue(checkId, out var messagePrefix))
                {
                    AddError($"{checkId}: {messagePrefix}: {error}");
                }
            });
        }

        public override string ToString()
        {
            return m_AppliedChecks == k_ChecksAppliedInLegacyPVS
                ? base.ToString()
                : $"{nameof(PureFileValidation)}: {string.Join(", ", m_AppliedChecks.Keys)}";
        }
    }
}
