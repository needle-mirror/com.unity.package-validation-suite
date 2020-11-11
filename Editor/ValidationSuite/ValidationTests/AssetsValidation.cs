using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    internal class AssetsValidation : BaseValidation
    {
        public AssetsValidation()
        {
            TestName = "Assets Validation";
            TestDescription = "Make sure assets included with the package meet Unity standards.";
            TestCategory = TestCategory.ContentScan;
        }

        protected override void Run()
        {
            TestState = TestState.Succeeded;

            // Validate that all images our users will interact with meet quality standards
            ValidateImageQuality();
        }

        private void ValidateImageQuality()
        { 
            foreach (var fileType in restrictedImageFileList)
            {
                List<string> matchingFiles = new List<string>();
                DirectorySearch(Context.PublishPackageInfo.path, fileType, ref matchingFiles);

                if (matchingFiles.Any())
                {
                    foreach (var file in matchingFiles)
                    {
                        var cleanRelativePath = Utilities.GetOSAgnosticPath(Utilities.GetPathFromRoot(file, Context.PublishPackageInfo.path));

                        if (!FileInExceptionPath(cleanRelativePath))
                        {
                            AddError(cleanRelativePath + " cannot be included in a package. All images we share with users must use the png format. This is an expectation shared with asset store packages as well, and will help us provide high quality images for our users.");
                        }
                    }
                }
            }
        }

        private bool FileInExceptionPath(string filePath)
        {
            // break up path into parts.
            var pathParts = filePath.ToLower().Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var pathPart in pathParts)
            {
                if (imageQualityExceptionPaths.Contains(pathPart))
                {
                    // If any part of the path is in the exception list, return immediately.
                    return true;
                }
            }

            return false;
        }

        private readonly string[] restrictedImageFileList =
        {
            "*.jpg",
            "*.jpeg",
        };

        private readonly HashSet<string> imageQualityExceptionPaths = new HashSet<string>
        {
            "documentation~",
            "tests",
        };
    }
}
