using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using PvpXray;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    [UsedImplicitly]
    class LegacyXrayValidation : BaseValidation
    {
        static readonly Dictionary<string, string> k_ChecksAppliedInLegacyPVS = new Dictionary<string, string>
        {
            ["PVP-62-1"] = "index.md filename must be spelled in lowercase",
        };

        static LegacyXrayValidation()
        {
            if (k_ChecksAppliedInLegacyPVS.Keys.Except(Validator.Checks).Count() != 0)
            {
                throw new InvalidOperationException("Trying to enforce non-existing x-ray check in legacy PVS");
            }
        }

        // Indirection to support PVS's own test suite.
        readonly Dictionary<string, string> m_AppliedChecks;
        readonly Validator m_Validator = new Validator();

        public LegacyXrayValidation()
        {
            TestName = "X-ray Validations";
            TestDescription = "Assorted requirements on package file contents.";
            TestCategory = TestCategory.ContentScan;

            // Don't apply these new x-ray checks in VerifiedSet (APV) context, for compatibility with old vendored PVS versions.
            SupportedValidations = new[] { ValidationType.CI, ValidationType.LocalDevelopment, ValidationType.LocalDevelopmentInternal, ValidationType.Promotion };

            m_AppliedChecks = k_ChecksAppliedInLegacyPVS;
        }

        internal LegacyXrayValidation(params string[] appliedChecks)
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
                : $"{nameof(LegacyXrayValidation)}: {string.Join(", ", m_AppliedChecks.Keys)}";
        }
    }
}
