using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests.Standards
{
    class APIDocumentationIncludedUS0041 : BaseStandardChecker, IPvpChecker
    {
        public override string StandardCode => "US-0041";
        public override StandardVersion Version => new StandardVersion(2, 1, 1);

        public void Check(string packagePath, AssemblyInfo[] assemblyInfo, ValidationAssemblyInformation vai)
        {
            var filterYamlParameter = "";
            var filterYamlPath = Path.Combine(packagePath, "Documentation~", "filter.yml");
            if (Utilities.FileExists(filterYamlPath))
            {
                filterYamlParameter = $@"--path-to-filter-yaml=""{filterYamlPath}""";
            }

            try
            {
                var stdoutLines = CheckV1Impl(packagePath, filterYamlParameter, assemblyInfo, vai);
                if (stdoutLines.Length > 0)
                {
                    var errorMessage = FormatErrorMessage(stdoutLines);
                    AddWarning(errorMessage);
                }
            }
            catch (InternalTestErrorException e)
            {
                AddError(e.Message);
            }
        }

        // Subject to PVP stability guarantee.
        static string[] CheckV1Impl(string packagePath, string filterYamlParameter, AssemblyInfo[] assemblyInfo, ValidationAssemblyInformation vai)
        {
            var monopath = Utilities.GetMonoPath();
            var exePath = "packages/com.unity.package-validation-suite/Bin~/FindMissingDocs/FindMissingDocs.exe";
#if UNITY_2021_2_OR_NEWER
            exePath = FileUtil.GetPhysicalPath(exePath);
#else
            exePath = Path.GetFullPath(exePath);
#endif

            List<string> excludePaths = new List<string>();
            excludePaths.AddRange(Directory.GetDirectories(packagePath, "*~", SearchOption.AllDirectories));
            excludePaths.AddRange(Directory.GetDirectories(packagePath, ".*", SearchOption.AllDirectories));
            excludePaths.AddRange(Directory.GetDirectories(packagePath, "Tests", SearchOption.AllDirectories));
            foreach (var assembly in assemblyInfo)
            {
                //exclude sources from test assemblies explicitly. Do not exclude entire directories, as there may be nested public asmdefs
                if (vai.IsTestAssembly(assembly) && assembly.assemblyKind == AssemblyInfo.AssemblyKind.Asmdef)
                    excludePaths.AddRange(assembly.assembly.sourceFiles);
            }
            string responseFileParameter = string.Empty;
            string responseFilePath = null;
            if (excludePaths.Count > 0)
            {
                responseFilePath = Utilities.CreateTempFile($"--excluded-paths=\"{string.Join(",", excludePaths.Select(Path.GetFullPath))}\"");
                responseFileParameter = $@"--response-file=""{responseFilePath}""";
            }

            var command = $@"""{exePath}"" --root-path=""{packagePath}"" {filterYamlParameter} {responseFileParameter}";
            var startInfo = new ProcessStartInfo(monopath, command)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var process = Process.Start(startInfo);

            var stdout = new ProcessOutputStreamReader(process, process.StandardOutput);
            var stderr = new ProcessOutputStreamReader(process, process.StandardError);
            process.WaitForExit();
            var stdoutLines = stdout.GetOutput();
            var stderrLines = stderr.GetOutput();

            if (responseFilePath != null)
                File.Delete(responseFilePath);

            if (process.ExitCode != 0 || stderrLines.Length != 0)
            {
                // If FindMissingDocs fails and returns a non-zero exit code (like an unhandled exception) it means that
                // we couldn't validate the XmdDocValidation because the result is inconclusive. For that reason, we
                // should add it as an error to be addressed by the developer. If there's any bug with the tool itself
                // then that will need to be addressed in the XmlDoc repo and rebuild the binaries from PVS.
                throw new InternalTestErrorException($"XmlDocValidation test is inconclusive: FindMissingDocs.exe exited with status {process.ExitCode}.\n{monopath} {command}\n{string.Join("\n", stderrLines)}");
            }
            return stdoutLines;
        }

        public static string FormatErrorMessage(IEnumerable<string> expectedMessages)
        {
            return $@"The following APIs are missing documentation: {string.Join(Environment.NewLine, expectedMessages)}";
        }

        string[] IPvpChecker.Checks => new[] { "PVP-20-1" };

        void IPvpChecker.Run(in PvpRunner.Input input, PvpRunner.Output output)
        {
            var filterYamlParameter = ""; // such filtering intentionally not supported in PVP mode
            foreach (var line in CheckV1Impl(input.Package.path, filterYamlParameter, input.AssemblyInfo, new ValidationAssemblyInformation()))
            {
                output.Error("PVP-20-1", line);
            }
        }
    }
}
