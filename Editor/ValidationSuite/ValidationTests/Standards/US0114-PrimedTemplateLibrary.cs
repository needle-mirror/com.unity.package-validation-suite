using System.IO;
using System.Linq;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests.Standards
{
    internal class PrimedTemplateLibraryUS0114 : BaseStandardChecker
    {
        public override string StandardCode => "US-0114";

        public override StandardVersion Version => new StandardVersion(1, 0, 0);

        static readonly string k_DocumentationLink =
            ErrorDocumentation.GetLinkMessage("primed_library_validation_error.html", "template-is-missing-primed-library-path");

        public void Check(string path)
        {
            // Check that Library directory of template contains primed paths
            RequireExactlyOne(path, "ArtifactDB");
            RequireExactlyOne(path, "Artifacts", "DataStore");
            RequireExactlyOne(path, "SourceAssetDB");
        }

        void RequireExactlyOne(string packageRoot, params string[] mutexPrimedLibraryPaths)
        {
            var packageRelativePaths = mutexPrimedLibraryPaths.Select(path => "ProjectData~/Library/" + path).ToList();
            var existingPaths = packageRelativePaths.Where(path =>
            {
                var fullPath = $"{packageRoot}/{path}";
                return File.Exists(fullPath) || Directory.Exists(fullPath);
            }).ToList();

            if (existingPaths.Count == 0)
            {
                AddError($"Template is missing primed library path at {string.Join(" or ", packageRelativePaths)}. " +
                    $"It should have been added automatically in the CI packing process. {k_DocumentationLink}");
            }
            else if (existingPaths.Count != 1)
            {
                AddError($"Template has mutually exclusive primed library paths at {string.Join(" and ", existingPaths)}.");
            }
        }
    }
}
