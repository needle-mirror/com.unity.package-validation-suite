using System;
using System.IO;

using JetBrains.Annotations;
using PvpXray;

namespace UnityEditor.PackageManager.ValidationSuite
{
    [UsedImplicitly]
    class BundledPackagesChecker : IPvpChecker
    {
        public string[] Checks { get; } = { "PVP-163-1" }; // Dependencies of bundled packages should also be bundled

        static readonly string k_EditorManifestPath = EditorApplication.applicationContentsPath + "/Resources/PackageManager/Editor/manifest.json";

        internal Func<string, byte[]> ReadAllBytes { get; set; } = File.ReadAllBytes;

        public void Run(in PvpRunner.Input input, PvpRunner.Output output)
        {
            try
            {
                var editorManifest = new Json(XrayUtils.DecodeUtf8Lax(ReadAllBytes(k_EditorManifestPath)), null);

                var schemaVersion = (int)(editorManifest["schemaVersion"].IfPresent?.Number ?? 1.0);
                if (schemaVersion < 4) return;

                var packageUnderTest = input.Package.name;
                if (!MustBeBundled(packageUnderTest)) return;

                foreach (var entry in input.Package.dependencies)
                {
                    var dependency = entry.Key;
                    if (!MustBeBundled(dependency))
                    {
                        output.Error("PVP-163-1", $"bundled package {packageUnderTest} has unbundled dependency {dependency}");
                    }
                }

                return;

                bool MustBeBundled(string packageName)
                {
                    var entry = editorManifest["packages"][packageName].IfPresent;
                    if (entry == null) return false;

                    return entry["mustBeBundled"].IfPresent?.Boolean ?? entry["version"].IsPresent;
                }
            }
            catch (IOException e) when (e.IsNotFoundError())
            {
                output.Error("PVP-163-1", "editor manifest not found");
            }
            catch (SimpleJsonException)
            {
                output.Error("PVP-163-1", "invalid editor manifest");
            }
        }
    }
}
