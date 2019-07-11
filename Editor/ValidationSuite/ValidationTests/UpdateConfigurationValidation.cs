#if UNITY_2019_1_OR_NEWER
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
    internal class UpdateConfigurationValidation : BaseAssemblyValidation
    {
        public UpdateConfigurationValidation()
        {
            this.TestName = "API Updater Configuration Validation";
        }

        protected override bool IncludePrecompiledAssemblies => true;
        protected override void Run(AssemblyInfo[] info)
        {
            this.TestState = TestState.Succeeded;
            if (Context.ProjectPackageInfo?.name == "com.unity.package-validation-suite")
            {
                Information("PackageValidationSuite update configurations tested by editor tests.");
                return;
            }
            
            if (info.Length == 0)
            {
                TestState = TestState.Succeeded;
                return;
            }

            var validatorPath = Path.Combine(EditorApplication.applicationContentsPath, "Tools/ScriptUpdater/APIUpdater.ConfigurationValidator.exe");
            if (!File.Exists(validatorPath))
            {
                Information("APIUpdater.ConfigurationValidator.exe is not present in this version of Unity. Not validating update configurations.");
                return;
            }

            var asmdefAssemblies = info.Where(i => i.assemblyKind == AssemblyInfo.AssemblyKind.Asmdef).ToArray();
            if (asmdefAssemblies.Length > 0)
            {
                var asmdefAssemblyPaths = asmdefAssemblies.Select(i => Path.GetFullPath(i.assembly.outputPath));
                var references = new HashSet<string>(asmdefAssemblies.SelectMany(i => i.assembly.allReferences).Select(Path.GetFullPath));
                RunValidator(references, validatorPath, asmdefAssemblyPaths);
            }

            var precompiledAssemlbyInfo = info.Where(i => i.assemblyKind == AssemblyInfo.AssemblyKind.PrecompiledAssembly).ToArray();
            if (precompiledAssemlbyInfo.Length > 0)
            {
                var precompiledDllPaths = precompiledAssemlbyInfo.Select(i => Path.GetFullPath(i.precompiledDllPath));
                var precompiledAssemblyPaths = CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.All);

                RunValidator(precompiledAssemblyPaths, validatorPath, precompiledDllPaths);
            }
        }

        private void RunValidator(IEnumerable<string> references, string validatorPath, IEnumerable<string> assemblyPaths)
        {
            var responseFilePath = Path.GetTempFileName();
            File.WriteAllLines(responseFilePath, references);

            var monoPath = Utilities.GetMonoPath();

            var processStartInfo =
                new ProcessStartInfo(monoPath, $@"""{validatorPath}"" ""{responseFilePath}"" -a {string.Join(",", assemblyPaths.Select(p => $"\"{Path.GetFullPath(p)}\""))}")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            var process = Process.Start(processStartInfo);
            var stderr = new ProcessOutputStreamReader(process, process.StandardError);
            var stdout = new ProcessOutputStreamReader(process, process.StandardOutput);
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                var stdContent = string.Join("\n", stderr.GetOutput().Concat(stdout.GetOutput()));
                if (ApiUpdaterConfigurationExemptions(stdContent))
                    Warning(stdContent);
                else
                    Error(stdContent);
            }

            bool ApiUpdaterConfigurationExemptions(string stdContent)
            {
                if (stdContent.Contains("Mono.Cecil.AssemblyResolutionException"))
                    return true;

                // This is a temporary workaround to unblock dots team.
                var requiredEntries =  new[] 
                {
                    //"Failed to resolve source type  '[*] Unity.Entities.SubtractiveComponent<T>' in configuration [*] Unity.Entities.SubtractiveComponent<T> -> Unity.Entities.ExcludeComponent<T>",
                    //"Config: [*] Unity.Entities.ComponentGroup [*] Unity.Entities.ComponentSystemBase::GetComponentGroup([*] Unity.Collections.NativeArray<Unity.Entities.ComponentType>) -> * Unity.Entities.ComponentSystemBase::GetEntityQuery(Unity.Collections.NativeArray<ComponentType>)",
                    "Target of update (method ComponentSystemBase.GetEntityQuery) is less accessible than original (Unity.Entities.ComponentGroup Unity.Entities.ComponentSystemBase::GetComponentGroup(Unity.Entities.ComponentType[])).",
                    "Target of update (method ComponentSystemBase.GetEntityQuery) is less accessible than original (Unity.Entities.ComponentGroup Unity.Entities.ComponentSystemBase::GetComponentGroup(Unity.Collections.NativeArray`1<Unity.Entities.ComponentType>)).",
                    "Target of update (method ComponentSystemBase.GetEntityQuery) is less accessible than original (Unity.Entities.ComponentGroup Unity.Entities.ComponentSystemBase::GetComponentGroup(Unity.Entities.EntityArchetypeQuery[])).",
                    // "Signature of target method (Unity.Entities.EntityQuery Unity.Entities.EntityManager::CreateEntityQuery(Unity.Entities.EntityQueryDesc[])) differs from original method signature (Unity.Entities.ComponentGroup Unity.Entities.EntityManager::CreateComponentGroup(Unity.Entities.EntityArchetypeQuery[])).",
                    //"Configuation Validation - 4 invalid configuration entries found:"
                };

                return requiredEntries.All(stdContent.Contains);
            }
        }
    }
}
#endif
