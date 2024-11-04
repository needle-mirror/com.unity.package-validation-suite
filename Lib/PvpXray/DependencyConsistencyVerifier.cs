using System;
using System.Collections.Generic;
using System.Text;

namespace PvpXray
{
    // This implements PVP-165-1 x-ray check, and also contains the core logic
    // of the PVP-163-2 non-x-ray check, due to overlap in both logic and tests.
    //
    // The PVP-163-2 check firstly parses the editor manifest of the running
    // Editor, and fails early if there's a problem:
    //
    // PVP-163-2: editor manifest not found
    // PVP-163-2: invalid editor manifest
    //
    // Next, both checks may emit standard JSON structural errors for the
    // package manifest .version, .dependencies, and (PVP-163-2 only) .name.
    //
    // The checks emit an error if the version number lifecycle phase for the
    // package under test (per its package manifest), or one of its dependencies
    // (per the editor manifest or package manifest), cannot be determined due
    // to an invalid version number:
    //
    // PVP-163-2: package.json: .version: invalid version lifecycle phase
    // PVP-165-1: package.json: .version: invalid version lifecycle phase
    // PVP-163-2: package has dependency DEPNAME resolved to invalid version by editor manifest
    // PVP-165-1: package has dependency DEPNAME with invalid declared version
    //
    // Otherwise, the checks emit an error if the version number lifecycle phase
    // of the package under test is later than that of a dependency:
    //
    // PVP-163-2: package with PHASE version has dependency DEPNAME resolved to DEPPHASE version by editor manifest
    // PVP-165-1: package with PHASE version has dependency DEPNAME with DEPPHASE declared version
    //
    // Finally, PVP-163-2 checks that packages in the editor manifest only
    // depend on other editor manifest packages, and that bundled packages only
    // depend on other bundled packages (this is checked using the state of
    // each package in the actual editor manifest):
    //
    // PVP-163-2: STATE package PKGNAME has STATE dependency DEPNAME
    //
    // The check also looks for inconsistencies between the package manifest
    // and editor manifest:
    //
    // PVP-163-2: dependency NAME resolved by editor manifest to earlier version than the declared
    // PVP-163-2: package.json: .dependencies.DEPNAME: 'default' version dependency on package not in the editor manifest
    // PVP-163-2: package.json: .dependencies.DEPNAME: 'default' version dependency on package with no version in the editor manifest
    //
    // Note: If any packages in the PVP publish set (i.e. packages being tested
    // at the same time) are listed in the editor manifest, PVP-163-2 does not
    // use the editor manifest verbatim, but rather a virtual editor manifest
    // updated with the version numbers of the publish set packages, under the
    // assumption that the entire publish set will enter the editor manifest
    // at the same time. Without doing this, a package foo depending on bar
    // (where both foo and bar are in the publish set) would trigger spurious
    // "dependency resolved to earlier version" and "PHASE package has resolved
    // dependency with PHASE version" errors based on the version number of bar
    // in the actual editor manifest, instead of the version in the publish set.
    class DependencyConsistencyVerifier : Verifier.IChecker
    {
        public static string[] Checks { get; } = {
            "PVP-165-1", // no dependency versions in an earlier lifecycle phase
        };
        public static int PassCount => 0;

        public DependencyConsistencyVerifier(Verifier.Context context)
        {
            var manifest = context.Manifest;
            var pkgPhase = GetVersionPhaseFromManifest(manifest, e => context.AddError("PVP-165-1", e));

            foreach (var depVersion in manifest["dependencies"].MembersIfPresent)
            {
                try
                {
                    var depVersionStr = depVersion.String;

                    // Nothing for PVP-165 to check for "default" version dependencies (found in feature packages/U7).
                    if (depVersionStr == "default") continue;

                    var error = GetDependencyPhaseError(depVersion.Key, pkgPhase, depVersionStr, isResolvedVersion: false);
                    if (error != null) context.AddError("PVP-165-1", error);
                }
                catch (SimpleJsonException e)
                {
                    context.AddError("PVP-165-1", e.FullMessage);
                }
            }
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex) => throw new NotImplementedException();
        public void Finish() { }

        static string GetDependencyPhaseError(string depName, VersionPhase pkgPhase, string depVersionStr, bool isResolvedVersion)
        {
            var depPhase = GetVersionPhase(depVersionStr);
            if (depPhase >= pkgPhase && depPhase != VersionPhase.Invalid) return null;

            var sb = new StringBuilder("package ", capacity: 128);
            // the version phase of the package under test is not relevant if the dependency phase is invalid.
            if (depPhase != VersionPhase.Invalid) sb.Append($"with {VersionPhaseNames[(int)pkgPhase]} version ");
            sb.Append($"has dependency {depName}");

            var depPhaseName = VersionPhaseNames[(int)depPhase];
            if (isResolvedVersion) sb.Append($" resolved to {depPhaseName} version by editor manifest");
            else sb.Append($" with {depPhaseName} declared version");

            return sb.ToString();
        }

        public enum VersionPhase
        {
            Invalid = 0,        // (unrecognized pre-release string)
            Experimental = 1,   // -exp, -preview
            PreRelease = 2,     // -pre
            Released = 3,       // (no pre-release tag, including 0.x.y versions)
        }

        public static readonly string[] VersionPhaseNames = {
            "invalid",
            "experimental",
            "pre-release",
            "released",
        };

        /// Examines the first "word" of the pre-release tag of the given version string to
        /// determine version lifecycle. Does not validate SemVer nor Unity version syntax.
        public static VersionPhase GetVersionPhase(string versionString)
        {
            var i = 0;
            while (true)
            {
                if (i >= versionString.Length || versionString[i] == '+') return VersionPhase.Released;
                if (versionString[i] == '-') break;
                ++i;
            }

            var s = versionString.Slice(i + 1);
            if (s.StartsWith("pre") && (s.Length == 3 || !Net7Compat.IsAsciiLetterOrDigit(s[3]))) return VersionPhase.PreRelease;
            if (s.StartsWith("exp") && (s.Length == 3 || !Net7Compat.IsAsciiLetterOrDigit(s[3]))) return VersionPhase.Experimental;
            if (s.StartsWith("preview") && (s.Length == 7 || !Net7Compat.IsAsciiLetterOrDigit(s[7]))) return VersionPhase.Experimental;
            return VersionPhase.Invalid;
        }

        static VersionPhase GetVersionPhaseFromManifest(Json packageManifest, Action<string> addError)
        {
            try
            {
                var version = packageManifest["version"];
                var phase = GetVersionPhase(version.String);
                if (phase == VersionPhase.Invalid) addError(version.GetException("invalid version lifecycle phase").FullMessage);
                return phase;
            }
            catch (SimpleJsonException e)
            {
                addError(e.FullMessage);
                return VersionPhase.Invalid;
            }
        }

        public struct EditorManifest
        {
            public enum State
            {
                NonManifest = 0,
                InManifest = 1,
                BundledOrBuiltIn = 2,
            }

            public struct Entry
            {
                public string Version;
                public State State;

                public override string ToString() => $"{(Version == null ? "null" : $"'{Version}'")} ({State})";

                public static implicit operator Entry((string, bool) value)
                    => new Entry { Version = value.Item1, State = value.Item2 ? State.BundledOrBuiltIn : State.InManifest };
            }

            public static readonly string[] PkgStateNames = { "non-manifest", "manifest", "bundled" };
            public static readonly string[] DepStateNames = { "non-manifest", "unbundled" }; // intentionally short

            public Dictionary<string, Entry> Entries;

            public static EditorManifest Empty() => new EditorManifest { Entries = new Dictionary<string, Entry>() };

            public static EditorManifest Parse(Json editorManifest)
            {
                var em = Empty();

                // We support only manifest schema versions 3 (2021.1+), 4 (2022.2+), or later.
                var schemaVersion = editorManifest["schemaVersion"].IfPresent?.Number ?? 1.0;
                if (schemaVersion < 3.0) return em;

                foreach (var entry in editorManifest["packages"].Members)
                {
                    var version = entry["version"].IfPresent?.String;
                    em.Entries[entry.Key] = new Entry
                    {
                        Version = version,
                        State = entry["mustBeBundled"].IfPresent?.Boolean ?? version != null ? State.BundledOrBuiltIn : State.InManifest,
                    };
                }
                return em;
            }
        }

        public static void EditorManifestCheck(Json packageManifest, EditorManifest editorManifest, Action<string> addError)
        {
            var pkgPhase = GetVersionPhaseFromManifest(packageManifest, addError);

            var pkgEntry = default(EditorManifest.Entry);
            string pkgName = null;
            try
            {
                pkgName = packageManifest["name"].String;
                pkgEntry = editorManifest.Entries.GetValueOrDefault(pkgName);
            }
            catch (SimpleJsonException e)
            {
                addError(e.FullMessage);
            }

            try
            {
                foreach (var depVersion in packageManifest["dependencies"].MembersIfPresent) // throws if 'dependencies' is not an object
                {
                    var depName = depVersion.Key;
                    var depEntry = editorManifest.Entries.GetValueOrDefault(depName);
                    if (pkgEntry.State > depEntry.State)
                    {
                        // 'pkgName' cannot be null here, or pkgEntry.State would be 0 too
                        addError($"{EditorManifest.PkgStateNames[(int)pkgEntry.State]} package {pkgName} has {EditorManifest.DepStateNames[(int)depEntry.State]} dependency {depName}");
                    }

                    try
                    {
                        var depVersionStr = depVersion.String;

                        if (depEntry.Version == null)
                        {
                            if (depVersionStr == "default")
                            {
                                addError(depVersion.GetException(
                                    depEntry.State == EditorManifest.State.NonManifest
                                        ? "'default' version dependency on package not in the editor manifest"
                                        : "'default' version dependency on package with no version in the editor manifest"
                                ).FullMessage);
                            }
                            continue;
                        }

                        if (depVersionStr != "default" && SemVerCompare.Compare(depVersionStr, depEntry.Version) > 0)
                        {
                            addError($"dependency {depName} resolved by editor manifest to earlier version than the declared");
                        }

                        var error = GetDependencyPhaseError(depName, pkgPhase, depEntry.Version, isResolvedVersion: true);
                        if (error != null) addError(error);
                    }
                    catch (SimpleJsonException e)
                    {
                        addError(e.FullMessage);
                    }
                }
            }
            catch (SimpleJsonException e)
            {
                addError(e.FullMessage);
            }
        }
    }
}
