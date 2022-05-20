using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    internal class DiffEvaluation : BaseValidation
    {
        public DiffEvaluation()
        {
            TestName = "Package Diff Evaluation";
            TestDescription = "Produces a report of what's been changed in this version of the package.";
            TestCategory = TestCategory.DataValidation;
            SupportedValidations = new[] { ValidationType.AssetStore, ValidationType.LocalDevelopment, ValidationType.LocalDevelopmentInternal, ValidationType.Promotion };
        }

        internal class PackageCompareData
        {
            public List<string> Added { get; set; }
            public List<string> Removed { get; set; }
            public List<string> Modified { get; set; }
            public StringBuilder TreeOutput { get; set; }

            internal PackageCompareData()
            {
                Added = new List<string>();
                Removed = new List<string>();
                Modified = new List<string>();

                TreeOutput = new StringBuilder();
            }
        }

        protected override void Run()
        {
            // no previous package was found.
            if (Context.PreviousPackageInfo == null)
            {
                AddInformation("No previous package version. Skipping diff evaluation.");
                TestState = TestState.NotRun;
                return;
            }

            // Ensure results directory exists before trying to write to it
            Directory.CreateDirectory(ValidationSuiteReport.ResultsPath);

            // Flag certain file types are requiring special attention.
            // Asmdef - can cause breaks on client's updates to packages.
            // package.json - Will change information in UI
            //      - Diff actual file, report what changed...
            // Meta files - if all meta files have changed, that's a red flag
            // if there are no common files, all files have changed,
            GenerateReport(ValidationSuiteReport.ResultsPath, Context.PreviousPackageInfo, Context.PublishPackageInfo);

            TestState = TestState.Succeeded;
        }

        public void GenerateReport(string outputPath, ManifestData previousPackageManifestData, ManifestData newPackageManifestData)
        {
            // no previous package was found.
            if (Context.PreviousPackageInfo == null)
            {
                TestState = TestState.NotRun;
                return;
            }

            var compareData = new PackageCompareData();

            compareData.TreeOutput.Append("<" + newPackageManifestData.name + ">\n");
            Compare(compareData, previousPackageManifestData.path, newPackageManifestData.path);

            string fileName = Path.Combine(outputPath, newPackageManifestData.name + "@" + newPackageManifestData.version) + ".delta";
            StringBuilder Outout = new StringBuilder();
            Outout.Append("Package Update Delta Evaluation\n");
            Outout.Append("-------------------------------\n");
            Outout.Append("\n");
            Outout.Append("Package Name: " + newPackageManifestData.name + "\n");
            Outout.Append("Package Version: " + newPackageManifestData.version + "\n");
            Outout.Append("Compared to Version: " + previousPackageManifestData.version + "\n");
            Outout.Append("\n");
            if (compareData.Added.Any())
            {
                Outout.Append("New in package:\n");
                foreach (var addedFile in compareData.Added)
                {
                    Outout.Append("    " + addedFile + "\n");
                }

                Outout.Append("\n");
            }

            if (compareData.Removed.Any())
            {
                Outout.Append("Removed from package:\n");
                foreach (var removedFile in compareData.Removed)
                {
                    Outout.Append("    " + removedFile + "\n");
                }

                Outout.Append("\n");
            }

            if (compareData.Modified.Any())
            {
                Outout.Append("Modified:\n");
                foreach (var modifiedFile in compareData.Modified)
                {
                    Outout.Append("    " + modifiedFile + "\n");
                }

                Outout.Append("\n");
            }

            Outout.Append("\n");
            Outout.Append("Package Tree\n");
            Outout.Append("------------\n");
            Outout.Append("\n");
            Outout.Append(compareData.TreeOutput);

            File.WriteAllText(fileName, Outout.ToString());
        }

        void Compare(PackageCompareData compareData, string path1, string path2)
        {
            Compare(compareData, path1, path1.Length + 1, path2, path2.Length + 1, 1);
        }

        void Compare(PackageCompareData compareData, string path1, int path1PrefixLength, string path2, int path2PrefixLength, int depth)
        {
            var AddedTag = "  ++ADDED++";
            var RemovedTag = "  --REMOVED--";
            var ModifiedTag = "  (MODIFIED)";
            var linePrefix = string.Empty;
            for (int i = 0; i < (depth * 4); i++)
                linePrefix += " ";

            // Take a snapshot of the file system.
            List<String> files1 = string.IsNullOrEmpty(path1) ? new List<string>() : Directory.GetFiles(path1).Select(d => d.Substring(path1.Length + 1)).ToList();
            List<String> files2 = string.IsNullOrEmpty(path2) ? new List<string>() : Directory.GetFiles(path2).Select(d => d.Substring(path2.Length + 1)).ToList();

            foreach (var file in files1)
            {
                if (files2.Contains(file))
                {
                    var file1 = new FileInfo(Path.Combine(path1, file));
                    var file2 = new FileInfo(Path.Combine(path2, file));
                    if (file1.Length == file2.Length)
                    {
                        compareData.TreeOutput.Append(linePrefix + file + "\n");
                    }
                    else
                    {
                        compareData.TreeOutput.Append(linePrefix + file + ModifiedTag + "\n");
                        compareData.Modified.Add(Path.Combine(path1, file).Replace("\\", "/").Substring(path1PrefixLength));

                    }
                }
                else
                {
                    compareData.TreeOutput.Append(linePrefix + file + RemovedTag + "\n");
                    compareData.Removed.Add(Path.Combine(path1, file).Replace("\\", "/").Substring(path1PrefixLength));
                }
            }

            foreach (var file in files2)
            {
                if (!files1.Contains(file))
                {
                    compareData.TreeOutput.Append(linePrefix + file + AddedTag + "\n");
                    compareData.Added.Add(Path.Combine(path2, file).Replace("\\", "/").Substring(path2PrefixLength));
                }
            }

            // Start by comparing directories
            List<String> dirs1 = string.IsNullOrEmpty(path1) ? new List<string>() : Directory.GetDirectories(path1).Select(d => d.Substring(path1.Length + 1)).ToList();
            List<String> dirs2 = string.IsNullOrEmpty(path2) ? new List<string>() : Directory.GetDirectories(path2).Select(d => d.Substring(path2.Length + 1)).ToList();
            depth++;

            foreach (var directory in dirs1)
            {
                if (dirs2.Contains(directory))
                {
                    compareData.TreeOutput.Append(linePrefix + "<" + directory + ">\n");
                    Compare(compareData, Path.Combine(path1, directory), path1PrefixLength, Path.Combine(path2, directory), path2PrefixLength, depth);
                }
                else
                {
                    compareData.TreeOutput.Append(linePrefix + "<" + directory + ">" + RemovedTag + "\n");
                    Compare(compareData, Path.Combine(path1, directory), path1PrefixLength, null, 0, depth);
                }
            }

            foreach (var directory in dirs2)
            {
                if (!dirs1.Contains(directory))
                {
                    compareData.TreeOutput.Append(linePrefix + "<" + directory + ">" + AddedTag + "\n");
                    Compare(compareData, null, 0, Path.Combine(path2, directory), path2PrefixLength, depth);
                }
            }
        }
    }
}
