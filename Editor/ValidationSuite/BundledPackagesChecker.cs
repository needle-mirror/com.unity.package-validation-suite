using System;
using System.IO;

using JetBrains.Annotations;
using PvpXray;

namespace UnityEditor.PackageManager.ValidationSuite
{
    [UsedImplicitly]
    class BundledPackagesChecker : IPvpChecker
    {
        internal static readonly string[] StaticChecks = {
            "PVP-163-1", // Dependencies of bundled packages should also be bundled
            "PVP-163-2", // Editor manifest consistency checks for dependencies
        };

        public string[] Checks => StaticChecks;

        static readonly string k_EditorManifestPath = EditorApplication.applicationContentsPath + "/Resources/PackageManager/Editor/manifest.json";

        internal Func<string, byte[]> ReadAllBytes { get; set; } = File.ReadAllBytes;

        public void Run(in PvpRunner.Input input, PvpRunner.Output output)
        {
            Json editorManifest;
            int schemaVersion;
            try
            {
                editorManifest = new Json(XrayUtils.DecodeUtf8Lax(ReadAllBytes(k_EditorManifestPath)), null);
                schemaVersion = (int)(editorManifest["schemaVersion"].IfPresent?.Number ?? 1.0);
            }
            catch (IOException e) when (e.IsNotFoundError())
            {
                foreach (var check in StaticChecks)
                    output.Error(check, "editor manifest not found");
                return;
            }
            catch (SimpleJsonException)
            {
                foreach (var check in StaticChecks)
                    output.Error(check, "invalid editor manifest");
                return;
            }

            if (schemaVersion >= 4) CheckV1(input, output, editorManifest);
            CheckV2(input, output, editorManifest);
        }

        static void CheckV1(in PvpRunner.Input input, PvpRunner.Output output, Json editorManifest)
        {
            try
            {
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
            catch (SimpleJsonException)
            {
                output.Error("PVP-163-1", "invalid editor manifest");
            }
        }

        static void CheckV2(in PvpRunner.Input input, PvpRunner.Output output, Json editorManifest)
        {
            DependencyConsistencyVerifier.EditorManifest virtualEditorManifest;
            try
            {
                virtualEditorManifest = DependencyConsistencyVerifier.EditorManifest.Parse(editorManifest);
            }
            catch (SimpleJsonException)
            {
                output.Error("PVP-163-2", "invalid editor manifest");
                return;
            }

            // Update the "virtual" editor manifest with information from the publish set.
            foreach (var p in input.PublishSet)
            {
                if (virtualEditorManifest.Entries.TryGetValue(p.Id.Name, out var entry))
                {
                    entry.Version = p.Id.Version;
                    virtualEditorManifest.Entries[p.Id.Name] = entry;
                }
            }

            try
            {
                var packageManifest = new Json(XrayUtils.DecodeUtf8Lax(input.Manifest), "package.json");
                DependencyConsistencyVerifier.EditorManifestCheck(packageManifest, virtualEditorManifest, e => output.Error("PVP-163-2", e));
            }
            catch (SimpleJsonException e)
            {
                output.Error("PVP-163-2", e.FullMessage);
            }
        }
    }
}
