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
            // In the case where we are testing a template, let's not expect tests yet.
            if (Context.PublishPackageInfo.IsProjectTemplate)
            {
                TestState = TestState.NotRun;
                return;
            }

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
            var matchingFiles = Directory.GetFiles(packagePath, "*.cs", SearchOption.AllDirectories);
            //DirectorySearch(packagePath, "*.cs", ref matchingFiles);

            if (matchingFiles.Length == 0)
                return true;

            foreach (var dir in Directory.GetDirectories(packagePath))
            {
                if (!dir.EndsWith("Tests"))
                    continue;

                var testFiles = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);

                if (testFiles.Any())
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
