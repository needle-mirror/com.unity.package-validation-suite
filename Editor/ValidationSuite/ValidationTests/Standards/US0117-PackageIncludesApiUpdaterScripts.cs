using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.Compilation;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests.Standards
{
    //  This check uses APIs that is only available in 2019 and later like `CompilationPipeline.GetPrecompiledAssemblyPaths`
#if UNITY_2019_1_OR_NEWER
    internal class PackageIncludesApiUpdaterScriptsUS0117 : BaseStandardChecker
    {
        public override string StandardCode => "US-0117";

        public override StandardVersion Version => new StandardVersion(1, 0, 0);

        public void Check(AssemblyInfo[] info, string packagePath, string packageName)
        {
            if (info.Length == 0)
            {
                return;
            }

            var validatorPath = Path.Combine(EditorApplication.applicationContentsPath, "Tools/ScriptUpdater/APIUpdater.ConfigurationValidator.exe");
            if (!File.Exists(validatorPath))
            {
                AddInformation("APIUpdater.ConfigurationValidator.exe is not present in this version of Unity. Not validating update configurations.");
                return;
            }

            var asmdefAssemblies = info.Where(i => i.assemblyKind == AssemblyInfo.AssemblyKind.Asmdef).ToArray();
            if (asmdefAssemblies.Length > 0)
            {
                var asmdefAssemblyPaths = asmdefAssemblies.Select(i => Path.GetFullPath(i.assembly.outputPath));
                var references = new HashSet<string>(asmdefAssemblies.SelectMany(i => i.assembly.allReferences).Select(Path.GetFullPath));
                RunValidator(references, validatorPath, asmdefAssemblyPaths, packageName, packagePath);
            }

            // precompiledAssemblyInfo should not include assemblies that are not going to be part of the final package.
            // so, to avoid false positives/negatives, ignore any files with a relative path containing `~/`.
            var precompiledAssemblyInfo = info.Where(i => i.assemblyKind == AssemblyInfo.AssemblyKind.PrecompiledAssembly && !i.precompiledDllPath.Substring(packagePath.Length).Contains("~/")).ToArray();
            if (precompiledAssemblyInfo.Length > 0)
            {
                var precompiledDllPaths = precompiledAssemblyInfo.Select(i => Path.GetFullPath(i.precompiledDllPath));
                var precompiledAssemblyPaths = CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.All);

                RunValidator(precompiledAssemblyPaths, validatorPath, precompiledDllPaths, packageName, packagePath);
            }
        }

        private void RunValidator(IEnumerable<string> references, string validatorPath, IEnumerable<string> assemblyPaths, string packageName, string packagePath)
        {
            var referencesResponseFilePath = Path.GetTempFileName();
            File.WriteAllLines(referencesResponseFilePath, references);

            var argumentsForValidator = ArgumentsForValidator();
            var responseFilePath = Path.Combine(ValidationSuiteReport.ResultsPath, $"{packageName}.updater.validation.arguments");

            // Ensure results directory exists before trying to write to it
            Directory.CreateDirectory(ValidationSuiteReport.ResultsPath);

            File.WriteAllText(responseFilePath, argumentsForValidator);
            ActivityLogger.Log($"APIUpdater.ConfigurationValidator.exe response file written to {Path.GetFullPath(responseFilePath)}");

            int exitCode = 0;
            string output = string.Empty;

#if UNITY_2022_1_OR_NEWER
            // Starting in 2022.1 ConfigurationValidator is compiled to NET 5.0
            using(var validator = new NetCoreProgram(validatorPath, argumentsForValidator, a => {}))
            {
                validator.Start();
                const int FiveMinutes = 1000 * 60 * 5;
                validator.WaitForExit(FiveMinutes);
                output = validator.GetAllOutput();
                exitCode = validator.ExitCode;
            }
#else
            var monoPath = Utilities.GetMonoPath();
            // The bundled mono executable only supports the --response parameter starting with Unity 2021.2
#if UNITY_2021_2_OR_NEWER
            var processStartInfo = new ProcessStartInfo(monoPath, $@"--response=""{responseFilePath}"" ""{validatorPath}""")
#else
            var processStartInfo = new ProcessStartInfo(monoPath, $@"""{validatorPath}"" {argumentsForValidator}")
#endif
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            var process = Process.Start(processStartInfo);
            var stderr = new ProcessOutputStreamReader(process, process.StandardError);
            var stdout = new ProcessOutputStreamReader(process, process.StandardOutput);
            process.WaitForExit();

            exitCode = process.ExitCode;
            output = string.Join("\n", stderr.GetOutput().Concat(stdout.GetOutput()));
#endif

            if (exitCode != 0)
            {
                if (ApiUpdaterConfigurationExemptions(output))
                    AddWarning(output);
                else
                    AddError(output);
            }

            bool ApiUpdaterConfigurationExemptions(string stdContent)
            {
#if UNITY_2019_3_OR_NEWER
                return false;
#else
                if (stdContent.Contains("Mono.Cecil.AssemblyResolutionException"))
                    return true;

                // This is a temporary workaround to unblock dots team.
                var requiredEntries =  new[]
                {
                    "Target of update (method ComponentSystemBase.GetEntityQuery) is less accessible than original (Unity.Entities.ComponentGroup Unity.Entities.ComponentSystemBase::GetComponentGroup(Unity.Entities.ComponentType[])).",
                    "Target of update (method ComponentSystemBase.GetEntityQuery) is less accessible than original (Unity.Entities.ComponentGroup Unity.Entities.ComponentSystemBase::GetComponentGroup(Unity.Collections.NativeArray`1<Unity.Entities.ComponentType>)).",
                    "Target of update (method ComponentSystemBase.GetEntityQuery) is less accessible than original (Unity.Entities.ComponentGroup Unity.Entities.ComponentSystemBase::GetComponentGroup(Unity.Entities.EntityArchetypeQuery[])).",
                };

                return requiredEntries.All(stdContent.Contains);
#endif
            }

            string ArgumentsForValidator()
            {
                var whitelistArg = string.Empty;

                // Resolves ValidationWhiteList.txt in folders
                //      - ApiUpdater~/{Editor Exact Version} (ex: 2019.3.0f1)
                //      - ApiUpdater~/{Editor Version Without Alpha/Beta/RC/Final info} (ex: 2019.3)
                //      - ApiUpdater~/
                // first one found will be used.
                var probingFolders = new[] {$"{UnityEngine.Application.unityVersion}", $"{Regex.Replace(UnityEngine.Application.unityVersion, @"(?<=20[1-5][0-9]\.\d{1,3})\.[0-9]{1,4}.*", string.Empty)}", "."};
                foreach (var path in probingFolders)
                {
                    var whitelistPath = Path.Combine(packagePath, $"ApiUpdater~/{path}/ValidationWhiteList.txt");
                    if (File.Exists(whitelistPath))
                    {
                        whitelistArg = $@" --whitelist ""{whitelistPath}""";
                        break;
                    }
                }

#if UNITY_2022_1_OR_NEWER
                // Starting with 2022.1 we started building UnityEngine/Editor against *netstandard2.1*
                // This changes requires all tools to be run under DotNet Core which changes the way assemblies
                // are resolved so we now pass a list of search paths where the Unity.APIValidation should look
                // for during assembly resolution.
                var searchPathResponseFilePath = Path.GetTempFileName();
                File.WriteAllText(searchPathResponseFilePath, string.Join(System.Environment.NewLine, AssemblySearchPaths()));
                var assemblySearchPathArg = $" -s \"{searchPathResponseFilePath}\"";
#else
                var assemblySearchPathArg = string.Empty;
#endif
                return $"\"{referencesResponseFilePath}\"{assemblySearchPathArg} -a {string.Join(",", assemblyPaths.Select(p => $"\"{Path.GetFullPath(p)}\""))} {whitelistArg}";
            }

#if UNITY_2022_1_OR_NEWER
            IEnumerable<string> AssemblySearchPaths()
            {
                return NetStandardSearchPaths().Concat(new []
                {
                    $"{EditorApplication.applicationContentsPath}/Managed",
                    $"{EditorApplication.applicationContentsPath}/Managed/UnityEngine",
                    "Library/ScriptAssemblies"
                });
            }

            IEnumerable<string> NetStandardSearchPaths()
            {
                yield return Path.Combine(GetNetStandardDir(), "ref", "2.1.0");
                yield return Path.Combine(GetNetStandardDir(), "compat", "2.1.0", "shims", "netfx");
                yield return Path.Combine(GetNetStandardDir(), "compat", "2.1.0", "shims", "netstandard");
            }

            string GetNetStandardDir() => Path.Combine(EditorApplication.applicationContentsPath, "NetStandard");
#endif
        }
    }
#endif
}
