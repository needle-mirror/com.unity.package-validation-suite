using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Threading;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    internal class TestsValidation : BaseValidation
    {
        public TestsValidation()
        {
            TestName = "Tests Validation";
            TestDescription = "Verify that the package has tests, and that test coverage is good.";
            TestCategory = TestCategory.DataValidation;
            SupportedValidations = new[] { ValidationType.CI, ValidationType.LocalDevelopment, ValidationType.Publishing };
        }

        protected override void Run()
        {
            // Start by declaring victory
            TestState = TestState.Succeeded;

            if (!PackageHasTests(Context.PublishPackageInfo.path) && !Context.relatedPackages.Any())
            {
                AddMissingTestsErrors();
                return;
            }
           

            //Check if there are relatedPackages that may contain the tests
            foreach (var relatedPackage in Context.relatedPackages)
            {
                if (!Directory.Exists(relatedPackage.Path))
                {
                    if (Context.ValidationType == ValidationType.Publishing ||
                        Context.ValidationType == ValidationType.VerifiedSet)
                    {
                        Error("All Packages must include tests.  The specified test package (package.json->relatedPackages) is missing in " + relatedPackage.Path);
                    }
                    else
                    {
                        Warning(string.Format("All Packages must include tests.  The specified test package (package.json->relatedPackages) is missing in {0}", relatedPackage.Path));
                    }
                    return;
                }

                if (!PackageHasTests(relatedPackage.Path))
                {
                    Error("All Packages must include tests.  The specified test package (package.json->relatedPackages) contains no tests.");
                    return;
                }
            }

            // TODO: Go through files, make sure they have actual tests.

            // TODO: Can we evaluate coverage imperically for now, until we have code coverage numbers?
        }

        bool PackageHasTests(string packagePath)
        {
            
            foreach (var dir in Directory.GetDirectories(packagePath))
            {
                if (!dir.EndsWith("Tests"))
                    continue;
                // If the package has c# files, it should have tests.
                var matchingFiles = new List<string>();
                DirectorySearch(dir, "*.cs", ref matchingFiles);
                if (!matchingFiles.Any())
                    continue;
                return true;
            }

            return false;
        }

        private void AddMissingTestsErrors()
        {
            Error("All Packages must include tests for automated testing.  No tests were found for this package.");
        }
    }
}
