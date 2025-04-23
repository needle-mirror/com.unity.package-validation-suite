using System;
using System.Collections.Generic;
using System.Linq;

namespace PvpXray
{
    class ManifestBaselinesVerifier : Verifier.IChecker
    {
        public static string[] Checks { get; } = {
            "PVP-160-1", // Direct dependencies must have been promoted, if not built-in (and listed the editor manifest for the package's declared Unity min-version), or in the publish set.
            "PVP-160-2",
            "PVP-161-1", // The editor min-version of recursive dependencies may not be higher than package's own min-version (ignoring built-in or missing packages).
            "PVP-162-1", // The recursive dependency chain of the package may not contain cycles (ignoring built-in or missing packages).
            "PVP-164-1", // Manifest's Unity min-version must exist
        };
        public static int PassCount => 0;
        static readonly string[] k_Pvp160 = { "PVP-160-1", "PVP-160-2" };

        internal struct UnityVersionRequirement
        {
            public readonly string Major;
            public readonly string Minor;

            public bool IsAlphaBetaOr0F1 => Minor == null || Minor == "0f1" || Minor.StartsWithOrdinal("0a") || Minor.StartsWithOrdinal("0b");
            public bool IsAny => Major == null;
            public string RequiresSuchAndSuch => IsAny ? "does not specify a minimum Unity version" : $"requires Unity {Expand(null)}";

            public UnityVersionRequirement(Json manifest)
            {
                var major = manifest["unity"];
                var minor = manifest["unityRelease"];

                if (major.IsPresent)
                {
                    Major = major.String;
                    Minor = minor.IfPresent?.String;
                }
                else
                {
                    Major = Minor = null;
                }
            }

            public string Expand(string appendIfNoMinor) => Major == null ? null : Minor == null ? Major + appendIfNoMinor : $"{Major}.{Minor}";

            public bool Equals(UnityVersionRequirement other) => Major == other.Major && Minor == other.Minor;
            public override bool Equals(object obj) => obj is UnityVersionRequirement other && Equals(other);
            public override int GetHashCode() => unchecked(((Major?.GetHashCode() ?? 0) * 397) ^ (Minor?.GetHashCode() ?? 0));
            public static bool operator ==(UnityVersionRequirement left, UnityVersionRequirement right) => left.Equals(right);
            public static bool operator !=(UnityVersionRequirement left, UnityVersionRequirement right) => !left.Equals(right);

            public bool IsHigherThan(UnityVersionRequirement other, PackageId thisPackage, PackageId otherPackage, out string error, bool useLegacyMessageFormat = false)
            {
                error = null;
                if (this.IsAny) return false;

                if (other.IsAny)
                {
                    error = $"{thisPackage} {RequiresSuchAndSuch}, but {otherPackage} {other.RequiresSuchAndSuch}";
                    return true;
                }

                if (NaturalCompare(this.Expand(".0f1"), other.Expand(".0f1")) > 0)
                {
                    error = useLegacyMessageFormat
                        ? $"{thisPackage} {RequiresSuchAndSuch}, but {otherPackage} only requires {other.Expand(null)}"
                        : $"{thisPackage} {RequiresSuchAndSuch}, but {otherPackage} {other.RequiresSuchAndSuch}";
                    return true;
                }

                return false;
            }
        }

        static void NaturalCompareReadComponent(string str, int startIndex, out int endIndex, out bool isNumber)
        {
            isNumber = startIndex < str.Length && Net7Compat.IsAsciiDigit(str[startIndex]);
            for (endIndex = startIndex; endIndex < str.Length && Net7Compat.IsAsciiDigit(str[endIndex]) == isNumber; ++endIndex)
            {
            }
        }

        internal static int NaturalCompare(string a, string b)
        {
            var ai = 0;
            var bi = 0;
            while (ai < a.Length && bi < b.Length)
            {
                NaturalCompareReadComponent(a, ai, out var aj, out var aIsNumber);
                NaturalCompareReadComponent(b, bi, out var bj, out var bIsNumber);
                var aLen = aj - ai;
                var bLen = bj - bi;
                if (aIsNumber && bIsNumber)
                {
                    // Skip leading '0'.
                    while (aLen > 0 && a[ai] == '0') { --aLen; ++ai; }
                    while (bLen > 0 && b[bi] == '0') { --bLen; ++bi; }

                    // If the numbers have different length, that determines the order.
                    // Otherwise, we can simply fall back to a string compare.
                    if (aLen != bLen) return aLen - bLen;
                }

                var result = string.CompareOrdinal(a, ai, b, bi, Math.Min(aLen, bLen));
                if (result != 0) return result;
                if (aLen != bLen) return aLen - bLen;

                ai = aj;
                bi = bj;
            }

            var aRemaining = a.Length - ai;
            var bRemaining = b.Length - bi;
            return aRemaining - bRemaining;
        }

        public ManifestBaselinesVerifier(Verifier.Context context)
        {
            context.IsLegacyCheckerEmittingLegacyJsonErrors = true;
            _ = context.HttpClient; // Bail early if running offline.

            var alreadyProcessed = new HashSet<PackageId>();
            var packageUnderTest = new PackageId(context.ManifestPermitInvalidJson);
            var packageUnderTestMinUnity = new UnityVersionRequirement(context.ManifestPermitInvalidJson);
            var packageUnderTestMinUnityExpanded = packageUnderTestMinUnity.Expand(".0f1");
            var path = new List<PackageId>();

            Verifier.EditorManifest editorManifest = default;
            if (!packageUnderTestMinUnity.IsAny && !context.TryFetchEditorManifestBaseline(packageUnderTestMinUnityExpanded, out editorManifest))
            {
                var major = packageUnderTestMinUnity.Major;
                var i = major.IndexOf('.');
                context.AddError("PVP-164-1",
                    XrayUtils.TryParseUint(i == -1 ? major : major.SpanOrSubstring(0, i), out var number) &&
                    (number <= 5 || (number >= 2000 && number < 2018) || (number == 2018 && major != "2018.4"))
                        ? $"Unity Editor min-version {major} is too old; please require 2019.4 or later"
                        : $"Unity Editor min-version {packageUnderTestMinUnityExpanded} does not (yet?) exist");
            }

            WalkDependencies(packageUnderTest);
            return;

            void CheckThatDirectDependencyIsBuiltIn(PackageId dependency)
            {
                if (!packageUnderTestMinUnity.IsAny)
                {
                    if (!editorManifest.IsAvailable)
                    {
                        context.AddError(k_Pvp160, $"Editor manifest for Unity {packageUnderTestMinUnityExpanded} is unavailable; built-in packages cannot be determined");
                    }
                    else if (editorManifest.Packages.TryGetValue(dependency.Name, out var emVersion))
                    {
                        if (dependency.Version == emVersion) return; // exact match trivially satisfies PVP-160-{1,2} both

                        // PVP-160-1 requires exact matching version
                        context.AddError("PVP-160-1", dependency.ToString());

                        // PVP-160-2 requires compatible version
                        var i = emVersion.IndexOf('.');
                        var isSameMajor = i != -1 && dependency.Version.Length > i && string.CompareOrdinal(emVersion, 0, dependency.Version, 0, i + 1) == 0;
                        var isCompatible = isSameMajor && SemVerCompare.Compare(emVersion, dependency.Version) >= 0;
                        if (!isCompatible) context.AddError("PVP-160-2", $"{dependency} (Unity {packageUnderTestMinUnityExpanded} provides {dependency.Name}@{emVersion})");
                        return;
                    }
                }

                context.AddError(k_Pvp160, dependency.ToString());
            }

            void WalkDependencies(PackageId package)
            {
                path.Add(package);
                try
                {
                    for (var i = 0; i < path.Count - 1; ++i)
                    {
                        if (path[i].Name == package.Name)
                        {
                            context.AddError("PVP-162-1", string.Join(" -> ", path.Skip(i)));
                            return;
                        }
                    }

                    if (!alreadyProcessed.Add(package)) return;

                    Json manifest;
                    if (package == packageUnderTest) manifest = context.ManifestPermitInvalidJson;
                    else if (context.TryFetchPackageBaseline(package, out var baseline)) manifest = baseline.Manifest;
                    else
                    {
                        if (path.Count == 2) CheckThatDirectDependencyIsBuiltIn(package);
                        return;
                    }

                    var minUnity = new UnityVersionRequirement(manifest);
                    if (minUnity.IsHigherThan(packageUnderTestMinUnity, package, packageUnderTest, out var error, useLegacyMessageFormat: true))
                    {
                        context.AddError("PVP-161-1", error);
                    }

                    foreach (var m in manifest["dependencies"].MembersIfPresent)
                    {
                        PackageId dependency;
                        try
                        {
                            dependency = new PackageId(m.Key, m.String);
                        }
                        catch (ArgumentException)
                        {
                            // Ignore invalid dependencies.
                            continue;
                        }

                        WalkDependencies(dependency);
                    }
                }
                catch (SimpleJsonException e)
                {
                    var message = $"{package}: {e.LegacyMessage}";
                    if (path.Count == 1) context.AddError(k_Pvp160, message);
                    context.AddError("PVP-161-1", message);
                    context.AddError("PVP-162-1", message);
                }
                finally
                {
                    path.RemoveAt(path.Count - 1);
                }
            }
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex) => throw new NotImplementedException();
        public void Finish() { }
    }
}
