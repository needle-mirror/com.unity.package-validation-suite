using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager.ValidationSuite;
using UnityEditor.PackageManager.ValidationSuite.ValidationTests;
using UnityEngine;
using PvpXray;

static class Pvp
{
    // This is an entrypoint intended to be run using:
    //     unity -batchmode -executeMethod Pvp.RunTests
    [UsedImplicitly]
    static void RunTests()
    {
#if UNITY_2019_2_OR_NEWER
        try
        {
            Utilities.EnsureDirectoryExists("Library/pvp");

            var pvp = new PvpRunner();
            var packages = pvp.GetProjectDirectPackageDependencies();
            foreach (var packageId in packages)
            {
                var path = $"Library/pvp/{packageId.Name}.result.json";

                // Don't check PVS unless PVS is the only package here.
                if (packages.Count != 1 && packageId.Name == Utilities.VSuiteName)
                {
                    continue;
                }

                Debug.Log($"Running PVP checks for {packageId}; results will be saved to {path}.");
                var results = pvp.Run(packageId.Name, null);
                File.WriteAllText(path, results.ToJson());
            }
        }
        catch (Exception e)
        {
            Console.Write($"Pvp.RunTests failed with exception: {e}");
            EditorApplication.Exit(1);
        }
        EditorApplication.Exit(0);
#else
        Console.Write("Pvp.RunTests requires Unity 2019.2 or later.");
        EditorApplication.Exit(2);
#endif
    }
}

namespace UnityEditor.PackageManager.ValidationSuite
{
    class InternalTestErrorException : Exception
    {
        public InternalTestErrorException() { }
        public InternalTestErrorException(string message) : base(message) { }
        public InternalTestErrorException(string message, Exception innerException) : base(message, innerException) { }
    }

    // PVP check implementations must implement this interface, and also
    // define a parameterless constructor.
    interface IPvpChecker
    {
        // The list of PV check IDs that this checker is responsible for.
        string[] Checks { get; }

        void Run(in PvpRunner.Input input, PvpRunner.Output output);
    }

    [UsedImplicitly]
    class XrayValidationChecker : IPvpChecker
    {
        public string[] Checks { get; } = Validator.Checks.ToArray();

        readonly Validator m_Validator = new Validator();

        public void Run(in PvpRunner.Input input, PvpRunner.Output output)
        {
            var package = new FileSystemPackage(input.Package.path);
            m_Validator.Validate(package, output.Error);
        }
    }

    class PvpRunner
    {
        public struct Input
        {
            public AssemblyInfo[] AssemblyInfo;
            public ManifestData Package;
        }

        public readonly struct Output
        {
            readonly Dictionary<string, List<string>> m_Checks;

            internal Output(Dictionary<string, List<string>> checks)
            {
                m_Checks = checks;
            }

            public void Error(string checkId, string message)
            {
                if (!m_Checks.TryGetValue(checkId, out var errors))
                {
                    ValidateCheckId(checkId);
                    throw new ArgumentException($"IPvpChecker added result for undeclared check {checkId}");
                }
                errors.Add(message);
            }
        }

        public class Results
        {
            readonly Dictionary<string, List<string>> m_Checks;
            readonly string m_Implementation;

            static readonly string[] k_ContextEnvVars = new[] {
                "GIT_BRANCH",
                "GIT_REPOSITORY_URL",
                "GIT_REVISION",
                "GIT_TAG",
                "YAMATO_JOBDEFINITION_NAME",
                "YAMATO_JOB_ID",
                "YAMATO_OWNER_EMAIL",
                "YAMATO_PROJECT_ID",
                "YAMATO_PROJECT_NAME",
            };

            public Dictionary<string, object> Context { get; } = new Dictionary<string, object>();
            public Dictionary<string, object> Target { get; } = new Dictionary<string, object>();

            public Results(Dictionary<string, List<string>> checks, string implementation)
            {
                m_Checks = checks;
                m_Implementation = implementation;
                foreach (var name in k_ContextEnvVars)
                {
                    var value = Environment.GetEnvironmentVariable(name);
                    if (value != null)
                    {
                        Context[name] = value;
                    }
                }
            }

            public string ToJson()
            {
                var sb = new StringBuilder();
                var results = new Dictionary<string, object>();
                var obj = new Dictionary<string, object>
                {
                    ["context"] = Context,
                    ["implementation"] = m_Implementation,
                    ["results"] = results,
                    ["target"] = Target,
                };

                var empty = new Dictionary<string, object>();
                foreach (var item in m_Checks)
                {
                    results[item.Key] = item.Value.Count != 0
                        ? new Dictionary<string, object> { ["errors"] = item.Value }
                        : empty;
                }

                SimpleJsonWriter.EmitGeneric(sb, null, obj, 0, emitComma: false);
                return sb.ToString();
            }
        }

        static readonly Regex k_Regex = new Regex("^PVP-[1-9][0-9]{1,3}-[1-9][0-9]{0,3}$");

        internal static void ValidateCheckId(string id)
        {
            if (!k_Regex.IsMatch(id))
            {
                throw new ArgumentException("invalid PVP check ID: " + id);
            }
        }

        readonly PackageInfo[] m_PackageInfos;
        readonly PackageInfo m_PvsPackageInfo;
        readonly List<IPvpChecker> m_Validations;

        public PvpRunner()
        {
            m_PackageInfos = Utilities.UpmListOffline(); // perf: this could fairly easily run in parallel with below
            m_PvsPackageInfo = GetPackageInfo(Utilities.VSuiteName);

            m_Validations = new List<IPvpChecker>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                m_Validations.AddRange(
                    Utilities.GetTypesSafe(assembly)
                    .Where(t => typeof(IPvpChecker).IsAssignableFrom(t) && !t.IsAbstract)
                    .Select(t => (IPvpChecker)Activator.CreateInstance(t))
                );
            }
        }

#if UNITY_2019_2_OR_NEWER
        // 2019.2.0a7: isRootDependency renamed to isDirectDependency and made public
        internal List<PackageId> GetProjectDirectPackageDependencies()
        {
            return m_PackageInfos
                .Where(pkg => pkg.isDirectDependency && pkg.source != PackageSource.BuiltIn)
                .Select(pkg => new PackageId(pkg))
                .ToList();
        }
#endif

        PackageInfo GetPackageInfo(string packageName)
        {
            return m_PackageInfos.FirstOrDefault(pi => pi.name == packageName)
                ?? throw new InvalidOperationException($"could not find {packageName} in package list");
        }

        // Extracted from BaseAssemblyValidation.GetRelevantAssemblyInfo.
        static AssemblyInfo[] GetRelevantAssemblyInfo(string packagePath)
        {
            if (EditorUtility.scriptCompilationFailed)
            {
                throw new InvalidOperationException("Compilation failed. Please fix any compilation errors.");
            }

            if (EditorApplication.isCompiling)
            {
                throw new InvalidOperationException("Compilation in progress. Please wait for compilation to finish.");
            }

            var files = new HashSet<string>(Directory.GetFiles(packagePath, "*", SearchOption.AllDirectories));

            var allAssemblyInfo = CompilationPipeline.GetAssemblies().Select(Utilities.AssemblyInfoFromAssembly).Where(a => a != null).ToArray();

            var packagePathPrefix = Path.GetFullPath(packagePath) + Path.DirectorySeparatorChar;
            var assemblyInfoOutsidePackage = allAssemblyInfo.Where(a => !a.asmdefPath.StartsWithOrdinal(packagePathPrefix)).ToArray();
            var badFilePath = assemblyInfoOutsidePackage.SelectMany(a => a.assembly.sourceFiles).Where(files.Contains).FirstOrDefault();
            if (badFilePath != null)
            {
                throw new InvalidOperationException($"Script \"{badFilePath}\" is not included by any asmdefs in the package.");
            }

            return allAssemblyInfo.Where(a => a.asmdefPath.StartsWithOrdinal(packagePathPrefix)).ToArray();
        }

        public Results Run(string packageName, Action<int, int> onProgress = null)
        {
            var progressNow = 0;
            var progressMax = m_Validations.Count + 1;
            onProgress?.Invoke(progressNow, progressMax);

            var checks = new Dictionary<string, List<string>>();
            var package = VettingContext.GetManifest(GetPackageInfo(packageName).resolvedPath);
            var input = new Input
            {
                AssemblyInfo = GetRelevantAssemblyInfo(package.path),
                Package = package,
            };
            var output = new Output(checks);

            var results = new Results(checks, implementation: m_PvsPackageInfo.packageId)
            {
                Target = {
                    ["package"] = new Dictionary<string, object> {
                        ["id"] = package.Id,
                        ["sha1"] = null, // No sha1, as we're not testing a tarball.
                    },
                    ["unity"] = new Dictionary<string, object> {
                        ["os"] = Application.platform == RuntimePlatform.OSXEditor ? "macos"
                            : Application.platform == RuntimePlatform.WindowsEditor ? "windows"
                            : Application.platform == RuntimePlatform.LinuxEditor ? "linux"
                            : throw new ArgumentOutOfRangeException(),
#if UNITY_2020_1_OR_NEWER
                        ["revision"] = UnityEditorInternal.InternalEditorUtility.GetUnityBuildHash(),
#endif
                        ["version"] = Application.unityVersion,
                    },
                },
            };

            foreach (var validation in m_Validations)
            {
                foreach (var checkId in validation.Checks)
                {
                    if (checks.ContainsKey(checkId))
                        throw new InvalidOperationException($"Multiple IPvpCheckers registered for check {checkId}");
                    ValidateCheckId(checkId);
                    checks[checkId] = new List<string>();
                }
            }

            ++progressNow;
            onProgress?.Invoke(progressNow, progressMax);

            foreach (var validation in m_Validations)
            {
                try
                {
                    validation.Run(input, output);
                }
                catch (InternalTestErrorException e)
                {
                    foreach (var checkId in validation.Checks)
                    {
                        output.Error(checkId, $"internal error in test: {e.Message}");
                    }
                }

                ++progressNow;
                onProgress?.Invoke(progressNow, progressMax);
            }

            return results;
        }
    }
}
