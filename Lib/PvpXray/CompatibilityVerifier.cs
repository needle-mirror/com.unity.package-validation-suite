using System;
using System.Collections.Generic;
using System.Linq;

namespace PvpXray
{
    class CompatibilityVerifier : Verifier.IChecker
    {
        public static string[] Checks => new[]
        {
            "PVP-171-1", // Unity min-version patch release compatibility
            "PVP-173-1", // Dependency version patch release compatibility
            "PVP-181-1", // Unity min-version minor release compatibility
        };

        public static int PassCount => 0;

        class ManifestData
        {
            readonly bool m_Present;
            readonly Dictionary<string, string> m_Dependencies;
            readonly string m_DependenciesError;
            readonly ManifestBaselinesVerifier.UnityVersionRequirement m_UnityMinVersion;
            readonly string m_UnityMinVersionError;
            public readonly PackageId PackageId;

            public ManifestData(Json manifest)
            {
                if (manifest == null) return;

                m_Present = true;
                PackageId = new PackageId(manifest);

                try
                {
                    m_Dependencies = manifest["dependencies"].MembersIfPresent
                        .ToDictionary(keySelector: m => m.Key, elementSelector: m => m.String);
                }
                catch (SimpleJsonException e)
                {
                    m_DependenciesError = $"{PackageId}: {e.Message}";
                }

                try
                {
                    m_UnityMinVersion = new ManifestBaselinesVerifier.UnityVersionRequirement(manifest);
                }
                catch (SimpleJsonException e)
                {
                    m_UnityMinVersionError = $"{PackageId}: {e.Message}";
                }
            }

            public void CheckUnityMinVersionUpwards(ManifestData next, Verifier.Context context, string checkId, bool allowDecrease)
            {
                if (this.m_UnityMinVersionError != null) context.AddError(checkId, this.m_UnityMinVersionError);
                if (next.m_UnityMinVersionError != null) context.AddError(checkId, next.m_UnityMinVersionError);
                if (!m_Present || !next.m_Present || m_UnityMinVersionError != null || next.m_UnityMinVersionError != null) return;

                if (m_UnityMinVersion == next.m_UnityMinVersion) return; // unchanged is always OK

                if (!allowDecrease)
                {
                    // In patch releases, the version must be unchanged.
                    context.AddError(checkId, $"{next.PackageId} {next.m_UnityMinVersion.RequiresSuchAndSuch}, but {PackageId} {m_UnityMinVersion.RequiresSuchAndSuch}");
                }
                else
                {
                    // In minor releases, next min-version may be less than or equal to this min-version.
                    if (next.m_UnityMinVersion.IsHigherThan(m_UnityMinVersion, next.PackageId, PackageId, out var error))
                        context.AddError(checkId, error);
                }
            }

            public void CheckDependenciesPatchCompat(ManifestData other, Verifier.Context context, string checkId)
            {
                // 'this' is the package under test
                if (m_DependenciesError != null) context.AddError(checkId, m_DependenciesError);
                if (other.m_DependenciesError != null) context.AddError(checkId, other.m_DependenciesError);
                if (!m_Present || !other.m_Present || m_DependenciesError != null || other.m_DependenciesError != null) return;

                foreach (var kv in other.m_Dependencies)
                {
                    var dep = kv.Key;
                    var otherVersion = kv.Value;
                    if (!m_Dependencies.TryGetValue(dep, out var thisVersion))
                    {
                        context.AddError(checkId, $"{PackageId}: removes dependency {dep}@{otherVersion} present in {other.PackageId}");
                        continue;
                    }

                    if (thisVersion == otherVersion) continue;

                    var thisIsValidTriple = VersionTriple.TryParseIgnoringPrereleaseAndBuildInfo(thisVersion, out var thisTriple);
                    var otherIsValidTriple = VersionTriple.TryParseIgnoringPrereleaseAndBuildInfo(otherVersion, out var otherTriple);
                    if (!thisIsValidTriple) context.AddError(checkId, $"{PackageId}: non-SemVer dependency {dep}@{thisVersion}");
                    if (!otherIsValidTriple) context.AddError(checkId, $"{other.PackageId}: non-SemVer dependency {dep}@{otherVersion}");
                    if (!thisIsValidTriple || !otherIsValidTriple) continue;

                    if (thisTriple.Major == otherTriple.Major && thisTriple.Minor == otherTriple.Minor) continue;

                    context.AddError(checkId, $"{PackageId}: has dependency {dep}@{thisVersion}, but {other.PackageId} has {dep}@{otherVersion}");
                }

                foreach (var kv in m_Dependencies)
                {
                    if (!other.m_Dependencies.ContainsKey(kv.Key))
                    {
                        context.AddError(checkId, $"{PackageId}: adds dependency {kv.Key}@{kv.Value} not present in {other.PackageId}");
                    }
                }
            }
        }

        public CompatibilityVerifier(Verifier.Context context)
        {
            _ = context.HttpClient; // Bail early if running offline.

            var versionJson = context.Manifest["version"];
            if (!VersionTriple.TryParseIgnoringPrereleaseAndBuildInfo(versionJson.String, out var triple)
                || triple.Major == uint.MaxValue
                || triple.Minor == uint.MaxValue
                || triple.Patch == uint.MaxValue)
                throw versionJson.GetException("not an acceptable SemVer version string");

            // No compatibility requirements for a 0.x package.
            if (triple.Major == 0) return;

            var underTestData = new ManifestData(context.Manifest);
            var previousQuery = Query(SemVerQuery.Op.LessThan, triple);
            var prevPatchData = GetData(previousQuery, requireSameMinor: true);
            var prevMinorData = GetData(previousQuery);
            var nextPatchData = GetData(Query(SemVerQuery.Op.GreaterThanOrEqual, triple.NextPatch), requireSameMinor: true);
            var nextMinorData = GetData(Query(SemVerQuery.Op.GreaterThanOrEqual, triple.NextMinor));

            // The Unity min-version checks are directional.
            prevPatchData.CheckUnityMinVersionUpwards(underTestData, context, "PVP-171-1", allowDecrease: false);
            underTestData.CheckUnityMinVersionUpwards(nextPatchData, context, "PVP-171-1", allowDecrease: false);

            prevMinorData.CheckUnityMinVersionUpwards(underTestData, context, "PVP-181-1", allowDecrease: true);
            underTestData.CheckUnityMinVersionUpwards(nextMinorData, context, "PVP-181-1", allowDecrease: true);

            // The dependency version patch checks are symmetric.
            underTestData.CheckDependenciesPatchCompat(prevPatchData, context, "PVP-173-1");
            underTestData.CheckDependenciesPatchCompat(nextPatchData, context, "PVP-173-1");

            return;

            SemVerQuery Query(SemVerQuery.Op op, VersionTriple refVersion)
            {
                var query = new SemVerQuery(op, refVersion);
                context.QuerySemVerProduction(underTestData.PackageId.Name, ref query);
                return query;
            }

            ManifestData GetData(SemVerQuery query, bool requireSameMinor = false)
            {
                if (query.BestVersion != null
                    && query.BestTriple.Major == triple.Major
                    && (query.BestTriple.Minor == triple.Minor || !requireSameMinor)
                    && context.TryFetchPackageBaseline(new PackageId(underTestData.PackageId.Name, query.BestVersion), out var baseline))
                    return new ManifestData(baseline.Manifest);

                return new ManifestData(null);
            }
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex) => throw new NotImplementedException();
        public void Finish() { }
    }
}
