using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PvpXray;
using Semver;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Assertions;
using UnityEditor.Compilation;
using UnityEditor.PackageManager.ValidationSuite.ValidationTests;
using UnityEditor.PackageManager.ValidationSuite.Utils;
using UnityEditor.PackageManager.ValidationSuite.ValidationTests.Standards;
using Assembly = UnityEditor.Compilation.Assembly;

namespace UnityEditor.PackageManager.ValidationSuite
{
    public static class Utilities
    {
        internal const string PackageJsonFilename = "package.json";
        internal const string ChangeLogFilename = "CHANGELOG.md";
        internal const string EditorAssemblyDefintionSuffix = ".Editor.asmdef";
        internal const string EditorTestsAssemblyDefintionSuffix = ".EditorTests.asmdef";
        internal const string RuntimeAssemblyDefintionSuffix = ".Runtime.asmdef";
        internal const string RuntimeTestsAssemblyDefintionSuffix = ".RuntimeTests.asmdef";
        internal const string ThirdPartyNoticeFile = "Third-Party Notices.md";
        internal const string LicenseFile = "LICENSE.md";
        internal const string VSuiteName = "com.unity.package-validation-suite";

        public static bool NetworkNotReachable { get { return Application.internetReachability == NetworkReachability.NotReachable; } }

        public static string CreatePackageId(string name, string version)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version))
                throw new ArgumentNullException("Both name and version must be specified.");

            return string.Format("{0}@{1}", name, version);
        }

        public static bool IsPreviewVersion(string version)
        {
            var semVer = SemVersion.Parse(version);
            VersionTag pre = VersionTag.Parse(semVer.Prerelease);
            return PackageLifecyclePhase.IsPreviewVersion(semVer, pre);
        }

        internal static T GetDataFromJson<T>(string jsonFile)
        {
            return JsonUtility.FromJson<T>(File.ReadAllText(jsonFile));
        }

        internal static void EnsureDirectoryExists(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (IOException) when (Directory.Exists(path))
            {
            }
        }

        internal static string CreatePackage(string path, string workingDirectory)
        {
            //No Need to delete the file, npm pack always overwrite: https://docs.npmjs.com/cli/pack
            var packagePath = Path.Combine(Path.Combine(Application.dataPath, ".."), path);

            var launcher = new NodeLauncher();
            launcher.WorkingDirectory = workingDirectory;
            launcher.NpmPack(packagePath);

            var packageName = launcher.OutputLog.ToString().Trim();
            return packageName;
        }

        internal static PackageInfo[] UpmSearch(string packageIdOrName = null, bool throwOnRequestFailure = false)
        {
            Profiler.BeginSample("UpmSearch");
            var request = string.IsNullOrEmpty(packageIdOrName) ? Client.SearchAll() : Client.Search(packageIdOrName);
            while (!request.IsCompleted)
            {
                if (Utilities.NetworkNotReachable)
                    throw new Exception("Failed to fetch package infomation: network not reachable");
                Thread.Sleep(100);
            }
            if (throwOnRequestFailure && request.Status == StatusCode.Failure)
                throw new Exception("Failed to fetch package infomation.  Error details: " + request.Error.errorCode + " " + request.Error.message);
            Profiler.EndSample();
            return request.Result;
        }

        internal static PackageInfo[] UpmListOffline(string packageIdOrName = null)
        {
            Profiler.BeginSample("UpmListOffline");

#if UNITY_2019_2_OR_NEWER
            var request = Client.List(true, true);
#else
            var request = Client.List(true);
#endif

            while (!request.IsCompleted)
                Thread.Sleep(100);
            var result = new List<PackageInfo>();
            foreach (var upmPackage in request.Result)
            {
                if (!string.IsNullOrEmpty(packageIdOrName) && !(upmPackage.name == packageIdOrName || upmPackage.packageId == packageIdOrName))
                    continue;
                result.Add(upmPackage);
            }

            Profiler.EndSample();

            return result.ToArray();
        }

        internal static string DownloadPackage(PackageId package, string workingDirectory)
        {
            //No Need to delete the file, npm pack always overwrite: https://docs.npmjs.com/cli/pack
            var launcher = new NodeLauncher();
            launcher.WorkingDirectory = workingDirectory;
            launcher.NpmRegistry = NodeLauncher.ProductionRepositoryUrl;

            try
            {
                launcher.NpmPack(package.Id);
            }
            catch (ApplicationException exception)
            {
                exception.Data["code"] = "fetchFailed";
                throw exception;
            }

            var packageName = launcher.OutputLog.ToString().Trim();
            return packageName;
        }

        static readonly PvpHttpClient k_HttpClient = new PvpHttpClient(VSuiteName);

        public static List<string> GetPackageVersionsOnProduction(string packageName)
        {
            var url = NodeLauncher.ProductionRepositoryUrl + packageName;
            var response = k_HttpClient.GetString(url, out var status);

            if (status == 404)
            {
                return null;
            }

            if (status == 200)
            {
                var metadata = SimpleJsonReader.ReadObject(response);
                if (metadata != null &&
                    metadata.TryGetValue("versions", out var versions) &&
                    versions is Dictionary<string, object> versionDict)
                {
                    return versionDict.Keys.ToList();
                }
                throw new Exception($"NPM registry did not provide valid package metadata: {url}");
            }

            throw new Exception($"Got HTTP status {status} for URL: {url}");
        }

        internal static bool PackageExistsOnProduction(PackageId package)
        {
            return GetPackageVersionsOnProduction(package.Name)?.Contains(package.Version) ?? false;
        }

        /// <summary>
        /// Determine if this IOException indicates that a path doesn't exist.
        /// </summary>
        internal static bool IsNotFoundError(this IOException e)
            => e is FileNotFoundException || e is DirectoryNotFoundException || e is DriveNotFoundException;

        internal static void IORetry(string operation, Action func, bool allowNotFound = false)
        {
            var attempts = 0;
            while (true)
            {
                try
                {
                    func();
                    return;
                }
                catch (IOException e) when (allowNotFound && IsNotFoundError(e))
                {
                    return;
                }
                catch (IOException e) when (++attempts < 4)
                {
                    Debug.LogError($"IO error when {operation} (attempt #{attempts}, will retry): {e}");
                    Thread.Sleep(1000 * attempts);
                }
            }
        }

        public static string ExtractPackage(string fullPackagePath, string workingPath, string outputDirectory, string packageName, bool deleteOutputDir = true)
        {
            Profiler.BeginSample("ExtractPackage");

            //verify if package exists
            if (!fullPackagePath.EndsWith(".tgz"))
                throw new ArgumentException("Package should be a .tgz file");

            if (!File.Exists(fullPackagePath))
                throw new FileNotFoundException(fullPackagePath + " was not found.");

            if (deleteOutputDir)
            {
                try
                {
                    IORetry($"deleting ExtractPackage output dir {outputDirectory}",
                        () => Directory.Delete(outputDirectory, true),
                        allowNotFound: true);

                    Directory.CreateDirectory(outputDirectory);
                }
                catch (IOException e)
                {
                    if (e.Message.ToLowerInvariant().Contains("1921"))
                        throw new ApplicationException("Failed to remove previous module in " + outputDirectory + ". Directory might be in use.");

                    throw;
                }
            }

            var tarPath = fullPackagePath.Replace(".tgz", ".tar");
            if (File.Exists(tarPath))
            {
                File.Delete(tarPath);
            }

            //Unpack the tgz into temp. This should leave us a .tar file
            PackageBinaryZipping.Unzip(fullPackagePath, workingPath);

            //See if the tar exists and unzip that
            var tgzFileName = Path.GetFileName(fullPackagePath);
            var targetTarPath = Path.Combine(workingPath, packageName + "-tar");
            if (Directory.Exists(targetTarPath))
            {
                Directory.Delete(targetTarPath, true);
            }

            if (File.Exists(tarPath))
            {
                PackageBinaryZipping.Unzip(tarPath, targetTarPath);
            }

            //Move the contents of the tar file into outputDirectory
            var packageFolderPath = Path.Combine(targetTarPath, "package");
            if (Directory.Exists(packageFolderPath))
            {
                //Move directories and meta files
                foreach (var dir in Directory.GetDirectories(packageFolderPath))
                {
                    var dirName = Path.GetFileName(dir);
                    if (dirName != null) // ??? GetFileName should only ever return null if its input is null.
                    {
                        var dest = Path.Combine(outputDirectory, dirName);
                        IORetry($"moving package dir from {dir} to {dest}",
                            () => Directory.Move(dir, dest));
                    }
                }

                foreach (var file in Directory.GetFiles(packageFolderPath))
                {
                    if (file.Contains("package.json") &&
                        !fullPackagePath.Contains(".tests") &&
                        !fullPackagePath.Contains(".samples") ||
                        !file.Contains("package.json"))
                    {
                        var dest = Path.Combine(outputDirectory, Path.GetFileName(file));
                        IORetry($"moving package file from {file} to {dest}",
                            () => File.Move(file, dest));
                    }
                }
            }

            //Remove the .tgz and .tar artifacts from temp
            List<string> cleanupPaths = new List<string>();
            cleanupPaths.Add(fullPackagePath);
            cleanupPaths.Add(tarPath);
            cleanupPaths.Add(targetTarPath);

            foreach (var p in cleanupPaths)
            {
                try
                {
                    FileAttributes attr = File.GetAttributes(p);
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        // This is a directory
                        Directory.Delete(targetTarPath, true);
                        continue;
                    }

                    File.Delete(p);
                }
                catch (DirectoryNotFoundException)
                {
                    //Pass since there is nothing to delete
                }
            }

            Profiler.EndSample();
            return outputDirectory;
        }

        public static string GetMonoPath()
        {
            var monoPath = Path.Combine(EditorApplication.applicationContentsPath, "MonoBleedingEdge/bin", Application.platform == RuntimePlatform.WindowsEditor ? "mono.exe" : "mono");
            return monoPath;
        }

        public static string GetOSAgnosticPath(string filePath)
        {
            return filePath.Replace("\\", "/");
        }

        public static string GetPathFromRoot(string filePath, string root)
        {
            return filePath.Remove(0, root.Length);
        }

        public static bool IsTestAssembly(Assembly assembly)
        {
            // see https://unity.slack.com/archives/C26EP4SUQ/p1555485851157200?thread_ts=1555441110.131100&cid=C26EP4SUQ for details about how this is verified
            if (assembly.allReferences.Contains("TestAssemblies"))
            {
                return true;
            }

            // Marking an assembly with UNITY_INCLUDE_TESTS means:
            // Include this assembly in the Unity project only if that package is in a testable state.
            // Otherwise, the assembly is ignored
            //
            // for now, we must read the test assembly file directly
            // because the defineConstraints field is not available on the assembly object
            AssemblyInfo assemblyInfo = Utilities.AssemblyInfoFromAssembly(assembly);
            AssemblyDefinition assemblyDefinition = Utilities.GetDataFromJson<AssemblyDefinition>(assemblyInfo.asmdefPath);
            return assemblyDefinition.defineConstraints.Contains("UNITY_INCLUDE_TESTS");
        }

        /// <summary>
        /// Returns the Assembly instances which contain one or more scripts in a package, given the list of files in the package.
        /// </summary>
        public static IEnumerable<Assembly> AssembliesForPackage(string packageRootPath)
        {
            var filesInPackage = Directory.GetFiles(packageRootPath, "*", SearchOption.AllDirectories);
            filesInPackage = filesInPackage.Select(p => p.Replace('\\', '/')).ToArray();

            var projectAssemblies = CompilationPipeline.GetAssemblies();
            var assemblyHash = new HashSet<Assembly>();

            foreach (var path in filesInPackage)
            {
                if (!string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                var assembly = GetAssemblyFromScriptPath(projectAssemblies, path);
                if (assembly != null && !Utilities.IsTestAssembly(assembly))
                {
                    assemblyHash.Add(assembly);
                }
            }

            return assemblyHash;
        }

        private static Assembly GetAssemblyFromScriptPath(Assembly[] assemblies, string scriptPath)
        {
            var fullScriptPath = Path.GetFullPath(scriptPath);

            foreach (var assembly in assemblies)
            {
                foreach (var packageSourceFile in assembly.sourceFiles)
                {
                    var fullSourceFilePath = Path.GetFullPath(packageSourceFile);

                    if (fullSourceFilePath == fullScriptPath)
                    {
                        return assembly;
                    }
                }
            }

            return null;
        }

        // Return all types from an assembly that can be loaded
        internal static IEnumerable<Type> GetTypesSafe(System.Reflection.Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        internal static AssemblyInfo AssemblyInfoFromAssembly(Assembly assembly)
        {
            var path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
            if (string.IsNullOrEmpty(path))
                return null;

            return new AssemblyInfo(assembly, path);
        }

        internal static void RecursiveDirectorySearch(string path, string searchPattern, ref List<string> matches)
        {
            if (!Directory.Exists(path))
                return;

            var files = Directory.GetFiles(path, searchPattern);
            if (files.Any())
                matches.AddRange(files);

            foreach (string subDir in Directory.GetDirectories(path)) RecursiveDirectorySearch(subDir, searchPattern, ref matches);
        }

        // System.IO.FileExists will return false on ArgumentException/IOException/UnauthorizedAccessException exceptions
        // which can be very misleading since it hides the underlying error and pretends the file doesn't exist.
        // This alternative method will not catch these exceptions in order to surface the underlying issue.
        internal static bool FileExists(string path)
        {
            try
            {
                new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).Close();
                return true; // file exists
            }
            catch (IOException e) when (e is FileNotFoundException || e is DirectoryNotFoundException || e is DriveNotFoundException)
            {
                return false; // it does not exist
            }
            // other errors bubble up, e.g. PathTooLongException.
        }

        internal enum DirectoryItemType
        {
            File,
            Directory
        }
        internal struct DirectoryItem
        {
            internal string Path;
            internal DirectoryItemType Type;
            internal int Depth;
            internal int ChildCount;
        }

        internal static IEnumerable<DirectoryItem> GetDirectoryAndFilesIn(string path)
        {
            List<DirectoryItem> result = new List<DirectoryItem>();
            RecursiveDirectoryListing(path, path, 1, result);
            return result;
        }

        static void RecursiveDirectoryListing(string rootPath, string path, int currentDepth, List<DirectoryItem> items)
        {
            var assets = Directory.GetFiles(path);
            foreach (var asset in assets)
            {
                var relativePath = GetRelativePath(asset, rootPath);

                items.Add(new DirectoryItem()
                {
                    Path = relativePath,
                    Type = DirectoryItemType.File,
                    Depth = currentDepth,
                    ChildCount = 0,
                });
            }

            //No need to check the root folder itself
            if (path != rootPath)
            {
                var relativePath = GetRelativePath(path, rootPath);
                items.Add(new DirectoryItem()
                {
                    Path = relativePath,
                    Type = DirectoryItemType.Directory,
                    Depth = currentDepth,
                    ChildCount = assets.Length
                });
            }

            var directories = Directory.GetDirectories(path);
            foreach (var directory in directories)
            {
                RecursiveDirectoryListing(rootPath, directory, currentDepth + 1, items);
            }
        }

        static string GetRelativePath(string path, string directory)
        {
            Assert.IsNotNull(path);
            Assert.IsNotNull(directory);

            if (!directory.EndsWith(Path.DirectorySeparatorChar.ToString()) && !Path.HasExtension(directory))
            {
                directory += Path.DirectorySeparatorChar;
            }

            try
            {
                directory = Path.GetFullPath(directory);
                path = Path.GetFullPath(path);

                var folderUri = new Uri(directory);
                var pathUri = new Uri(path);

                return Uri.UnescapeDataString
                    (
                        folderUri.MakeRelativeUri(pathUri).ToString()
                            .Replace('/', Path.DirectorySeparatorChar)
                    );
            }
            catch (UriFormatException uriFormatException)
            {
                throw new UriFormatException($"Failed to get relative path.\nPath: {path}\nDirectory:{directory}\n{uriFormatException}");
            }
        }

        internal static void HandleWarnings(List<string> faultyPaths, string warningText, string informationText, Action<string> warnFunc, Action<string> infoFunc)
        {
            if (faultyPaths.Count > 0)
            {
                warnFunc($"{warningText} ");
                PrintInformationFor(faultyPaths, informationText, infoFunc);
            }
        }

        static void PrintInformationFor(List<string> faultyPaths, string warningText, Action<string> infoFunc) => faultyPaths.ForEach(s => infoFunc(warningText + s));

        /// <summary>
        /// Determine if this IOException is the type thrown when opening a
        /// file in <see cref="FileMode.CreateNew"/> mode, and the file already
        /// exists.
        /// </summary>
        /// <remark>
        /// Checks for "Win32 warning" ERROR_FILE_EXISTS (0x80070050), not to
        /// be confused with ERROR_ALREADY_EXISTS (0x800700B7) which is thrown
        /// when renaming a file and the target is an existing file, nor the
        /// related 0x80131620 (thrown if the target is an existing directory).
        /// </remark>
        static bool IsCannotCreateNewBecauseFileExistsError(IOException e)
        {
            // ReSharper disable once InconsistentNaming
            const int WARN_WIN32_FILE_EXISTS = unchecked((int)0x80070050); // also works on Mac/Linux
            return e.HResult == WARN_WIN32_FILE_EXISTS;
        }

        // Use this instead of Path.GetTempPath (the latter can be insecure
        // on multiuser systems if used carelessly – and it is).
        public const string UnityTempPath = "Temp";

        // Secure replacement for .NET's broken Path.GetTempFileName (insecure
        // on multiuser systems). Additionally, this version creates the temp
        // file in Unity's Temp directory instead of the system Temp directory.
        public static string CreateTempFile(string contents)
        {
            while (true)
            {
                var path = $"{UnityTempPath}/PVS-{Path.GetRandomFileName()}.tmp";
                try
                {
                    using (var file = new FileStream(path, FileMode.CreateNew))
                    {
                        var bytes = Encoding.UTF8.GetBytes(contents);
                        file.Write(bytes, 0, bytes.Length);
                    }

                    return path;
                }
                catch (IOException e) when (IsCannotCreateNewBecauseFileExistsError(e))
                {
                    // OK, try another filename
                }
            }
        }
    }
}
