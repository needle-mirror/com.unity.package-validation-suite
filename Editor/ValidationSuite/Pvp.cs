using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Debug = UnityEngine.Debug;

static class Pvp
{
    // This is an entrypoint intended to be run using:
    //     unity -batchmode -executeMethod Pvp.RunTests
    [UsedImplicitly]
    static void RunTests()
    {
        try
        {
            Utilities.EnsureDirectoryExists("Library/pvp");

            var pvp = new PvpRunner();
            foreach (var packageId in pvp.VerificationSetIds)
            {
                var path = $"Library/pvp/{packageId.Name}.result.json";
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
        public string[] Checks { get; } = Verifier.CheckerSet.PvsCheckers.Checks;

        public void Run(in PvpRunner.Input input, PvpRunner.Output output)
        {
            var package = new FileSystemPackage(input.Package.path, input.Manifest);
            var resultFileStub = Verifier.OneShot(new VerifierContext(package)
            {
                HttpClient = new PvpHttpClient(Utilities.VSuiteName, cache: true),
                VerificationSet = input.VerificationSet,
            }, Verifier.CheckerSet.PvsCheckers, package);

            foreach (var entry in resultFileStub.Results)
            {
                var checkId = entry.Key;
                var checkResult = entry.Value;
                if (checkResult.SkipReason == null)
                {
                    foreach (var message in checkResult.Errors)
                    {
                        output.Error(checkId, message);
                    }
                }
                else
                {
                    output.Skip(checkId, checkResult.SkipReason);
                }
            }

            foreach (var entry in resultFileStub.Baselines)
            {
                output.Baseline(entry.Key, entry.Value);
            }
        }
    }

    class PvpRunner
    {
        public struct Input
        {
            public AssemblyInfo[] AssemblyInfo;
            public byte[] Manifest;
            public ManifestData Package;
            public List<VerifierContext.VerificationSetPackage> VerificationSet;
        }

        public readonly struct Output
        {
            readonly Dictionary<string, CheckResult> m_Checks;
            readonly Dictionary<string, string> m_Baselines;

            internal Output(Dictionary<string, CheckResult> checks, Dictionary<string, string> baselines)
            {
                m_Checks = checks;
                m_Baselines = baselines;
            }

            public void Error(string checkId, string message)
            {
                if (!m_Checks.TryGetValue(checkId, out var checkResult))
                {
                    ValidateCheckId(checkId);
                    throw new ArgumentException($"IPvpChecker added error for undeclared check {checkId}");
                }
                checkResult.Errors.Add(message);
            }

            public void Skip(string checkId, string reason)
            {
                if (!m_Checks.TryGetValue(checkId, out var checkResult))
                {
                    ValidateCheckId(checkId);
                    throw new ArgumentException($"IPvpChecker added skip reason for undeclared check {checkId}");
                }
                if (checkResult.SkipReason != null)
                {
                    throw new InvalidOperationException($"IPvpChecker added skip reason for check {checkId} more than once; previous skip reason {checkResult.SkipReason}, new skip reason: {reason}");
                }
                checkResult.SkipReason = reason;
            }

            public void Baseline(string name, string hash)
            {
                m_Baselines[name] = hash;
            }
        }

        public class CheckResult
        {
            public List<string> Errors = new List<string>();
            public string SkipReason;
        }

        public class Results
        {
            readonly Dictionary<string, CheckResult> m_Checks;
            readonly Dictionary<string, string> m_Baselines;
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

            public Results(Dictionary<string, CheckResult> checks, Dictionary<string, string> baselines, string implementation)
            {
                m_Checks = checks;
                m_Baselines = baselines;
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
                    var checkId = item.Key;
                    var checkResult = item.Value;
                    if (checkResult.SkipReason != null)
                    {
                        if (checkResult.Errors.Count != 0)
                        {
                            throw new InvalidOperationException("Errors must be empty if SkipReason is not null");
                        }

                        results[checkId] = new Dictionary<string, object> { ["skip_reason"] = checkResult.SkipReason };
                    }
                    else
                    {
                        results[checkId] = checkResult.Errors.Count != 0
                            ? new Dictionary<string, object> { ["errors"] = checkResult.Errors }
                            : empty;
                    }
                }

                var sb = new StringBuilder();
                sb.Append('{');
                SimpleJsonWriter.EmitGenericItems(sb, obj.OrderBy(kv => kv.Key, StringComparer.Ordinal), 0);

                if (m_Baselines.Count != 0)
                {
                    sb.Length -= 1; // remove '\n'
                    sb.Append(",\n  \"baselines\": {\n");
                    var first = true;
                    foreach (var baseline in m_Baselines.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    {
                        if (!first)
                        {
                            sb.Append(",\n");
                        }
                        first = false;

                        SimpleJsonWriter.EmitName(sb, baseline.Key, 2);
                        sb.Append(baseline.Value);
                    }

                    sb.Append("\n  }\n");
                }

                sb.Append("}\n");
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
        readonly List<VerifierContext.VerificationSetPackage> m_VerificationSet;
        internal readonly IReadOnlyList<PackageId> VerificationSetIds;

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

            var verificationSetIds = new List<PackageId>(m_PackageInfos.Length);
            VerificationSetIds = verificationSetIds;
            m_VerificationSet = new List<VerifierContext.VerificationSetPackage>();
            foreach (var pkg in m_PackageInfos)
            {
                var tarballPath = GetLocalTarballPath(pkg);
                if (tarballPath == null) continue;
                if (!File.Exists(tarballPath)) throw new InternalTestErrorException("Cannot locate package tarball: " + pkg.packageId);

                // Don't check PVS unless PVS is the only local tarball package here.
                if (VerificationSetIds.Count != 0 && pkg.name == Utilities.VSuiteName) continue;
                if (VerificationSetIds.Count == 1 && VerificationSetIds[0].Name == Utilities.VSuiteName)
                {
                    // Remove PVS again now we've found another package.
                    verificationSetIds.RemoveAt(0);
                    m_VerificationSet.RemoveAt(0);
                }

                verificationSetIds.Add(new PackageId(pkg.packageId));
                m_VerificationSet.Add(new VerifierContext.VerificationSetPackage(
                    manifest: ReadTarFile(tarballPath, "package/package.json"),
                    sha1: XrayUtils.Sha1(File.Open(tarballPath, FileMode.Open, FileAccess.Read))
                ));
            }
        }

#if UNITY_2019_3_OR_NEWER
        // 2019.3.0a3: PackageSource.LocalTarball added.
        static string GetLocalTarballPath(PackageInfo pkg)
        {
            if (pkg.source != PackageSource.LocalTarball) return null;

            var i = pkg.packageId.IndexOfOrdinal("@file:");
            if (i == -1) throw new InternalTestErrorException("Expected @file in: " + pkg.packageId);

            // Resolve path relative to Packages folder.
            return Path.Combine("Packages", pkg.packageId.Substring(i + 6));
        }
#else
        static string GetLocalTarballPath(PackageInfo pkg)
        {
            var i = pkg.packageId.IndexOfOrdinal("@file:");
            if (i == -1) return null;

            // Resolve path relative to Packages folder.
            var tarballPath = Path.Combine("Packages", pkg.packageId.Substring(i + 6));
            if (Directory.Exists(tarballPath)) return null; // directory, not tarball
            return tarballPath;
        }
#endif

        static byte[] ReadTarFile(string tarballPath, string nestedPath)
        {
            var (status, stdout) = RunTarCommand("xOf", tarballPath, nestedPath);
            if (status != 0)
            {
                throw new InternalTestErrorException($"'tar' could not extract file from tarball: {tarballPath}");
            }
            return stdout;
        }

        static (int, byte[]) RunTarCommand(params string[] arguments)
        {
            // This relies on 'tar' being available locally, which is the case by
            // default on both Linux, macOS and Windows (as circa 2018).
            var psi = new ProcessStartInfo
            {
                Arguments = $"\"{string.Join("\" \"", arguments)}\"",
                CreateNoWindow = true,
                FileName = "tar",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };

            var p = Process.Start(psi) ?? throw new InternalTestErrorException("failed to start 'tar' command");
            p.StandardInput.Close();

            using (var buf = new MemoryStream())
            {
                using (var stdout = p.StandardOutput.BaseStream)
                {
                    stdout.CopyTo(buf);
                }
                p.WaitForExit();
                return (p.ExitCode, buf.ToArray());
            }
        }

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

            var checks = new Dictionary<string, CheckResult>();
            var baselines = new Dictionary<string, string>();
            var package = VettingContext.GetManifest(GetPackageInfo(packageName).resolvedPath);

            // Workaround for Packman rewriting the package manifest on disk in Unity 2023.2+.
            byte[] manifest = null;
            for (var i = 0; i < VerificationSetIds.Count; i++)
            {
                if (VerificationSetIds[i].Name == packageName)
                {
                    manifest = m_VerificationSet[i].Manifest;
                    break;
                }
            }

            var input = new Input
            {
                AssemblyInfo = GetRelevantAssemblyInfo(package.path),
                Manifest = manifest,
                Package = package,
                VerificationSet = m_VerificationSet,
            };
            var output = new Output(checks, baselines);

            var results = new Results(checks, baselines, implementation: m_PvsPackageInfo.packageId)
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
                    checks[checkId] = new CheckResult();
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
