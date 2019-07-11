using System;
using System.IO;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    internal class PathLengthValidation : BaseValidation
    {
        public int MaxPathLength { get; set; } = 100;

        public PathLengthValidation()
        {
            TestName = "Path Length Validation";
            TestDescription = "Validate that all package files are below a minimum path threshold, to ensure that excessively long paths are not produced on Windows machines within user projects.";
            TestCategory = TestCategory.ContentScan;
            SupportedValidations = new[] { ValidationType.CI, ValidationType.LocalDevelopment, ValidationType.Publishing, ValidationType.VerifiedSet };
        }

        void CheckPathLengthInFolderRecursively(string folder, string basePath)
        {
            try
            {
                int baseLength = folder.Length - basePath.Length + 1;
                foreach (string entry in Directory.GetFileSystemEntries(folder))
                {
                    var name = Path.GetFileName(entry);
                    if (baseLength + name.Length > MaxPathLength)
                    {
                        var fullPath = Path.Combine(folder, name);
                        Error($"{fullPath} is {fullPath.Length} characters, which is longer than the limit of {MaxPathLength} characters. You must use shorter names.");
                    }
                }

                foreach (string dir in Directory.GetDirectories(folder))
                {
                    CheckPathLengthInFolderRecursively(dir, basePath);
                }
            }
            catch (Exception e)
            {
                Error("Exception " + e.Message);
            }
        }

        protected override void Run()
        {
            // Start by declaring victory
            TestState = TestState.Succeeded;

            //check if each file/folder has a sufficiently short path relative to the base
            CheckPathLengthInFolderRecursively(Context.PublishPackageInfo.path, Context.PublishPackageInfo.path);
        }
    }
}
