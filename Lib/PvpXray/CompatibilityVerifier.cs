using System;
using System.Collections.Generic;
using System.Linq;

namespace PvpXray
{
    class CompatibilityVerifier : Verifier.IChecker
    {
        public static string[] Checks { get; } = {
            "PVP-171-1", // Unity min-version patch release compatibility
            "PVP-171-2", // (ditto, permitting dropping support for unsupported releases)
            "PVP-171-3", // (ditto, but only disallow changes to major version)
            "PVP-173-1", // Dependency version patch release compatibility
            "PVP-181-1", // Unity min-version minor release compatibility
            "PVP-181-2", // (ditto, permitting dropping support for unsupported releases)
        };
        static readonly string[] k_171_12 = { "PVP-171-1", "PVP-171-2" };
        static readonly string[] k_181 = { "PVP-181-1", "PVP-181-2" };
        static readonly string[] k_181_2 = { null, "PVP-181-2" };

        public static int PassCount => 0;

        class ManifestData
        {
            readonly bool m_Present;
            readonly Dictionary<string, string> m_Dependencies;
            readonly string m_DependenciesError;
            readonly ManifestBaselinesVerifier.UnityVersionRequirement m_UnityMinVersion;
            readonly UnityMajor m_UnityMinVersionMajor;
            readonly string m_UnityMinVersionError;
            readonly string m_UnityMinVersionErrorLegacy;
            public readonly string UnityMinVersionV2Error;
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
                    m_DependenciesError = $"{PackageId}: {e.LegacyMessage}";
                }

                try
                {
                    m_UnityMinVersion = new ManifestBaselinesVerifier.UnityVersionRequirement(manifest);
                    try
                    {
                        m_UnityMinVersionMajor = m_UnityMinVersion.Major == null ? UnityMajor.Any : new UnityMajor(m_UnityMinVersion.Major);
                    }
                    catch (ArgumentException)
                    {
                        UnityMinVersionV2Error = $"{PackageId}: .unity: invalid Unity release string: {Yaml.Encode(m_UnityMinVersion.Major)}";
                    }
                }
                catch (SimpleJsonException e)
                {
                    m_UnityMinVersionError = $"{PackageId}: {e.Message}";
                    m_UnityMinVersionErrorLegacy = $"{PackageId}: {e.LegacyMessage}";
                }
            }

            public void CheckUnityMinVersionUpwards(ManifestData next, Verifier.Context context, string[] checkIds, bool allowDecrease)
            {
                if (checkIds.Length != 2 || checkIds[1] == null) throw new Net7Compat.UnreachableException();
                if (checkIds[0] != null)
                {
                    if (this.m_UnityMinVersionErrorLegacy != null) context.AddError(checkIds[0], this.m_UnityMinVersionErrorLegacy);
                    if (next.m_UnityMinVersionErrorLegacy != null) context.AddError(checkIds[0], next.m_UnityMinVersionErrorLegacy);
                }
                if (this.m_UnityMinVersionError != null) context.AddError(checkIds[1], this.m_UnityMinVersionError);
                if (next.m_UnityMinVersionError != null) context.AddError(checkIds[1], next.m_UnityMinVersionError);

                // Don't emit the v2 errors if the min-version is malformed.
                var emitV2 = this.UnityMinVersionV2Error == null && next.UnityMinVersionV2Error == null;

                if (!m_Present || !next.m_Present || m_UnityMinVersionError != null || next.m_UnityMinVersionError != null) return;

                if (m_UnityMinVersion == next.m_UnityMinVersion) return; // unchanged is always OK

                if (next.m_UnityMinVersion.IsHigherThan(m_UnityMinVersion, next.PackageId, PackageId, out var error))
                {
                    // It is an error to raise the min-version in patch and minor versions both (PVP-171, PVP-181)
                    // except PVP-*-2, which permits dropping support for unsupported editor releases and alphas/betas.

                    if (emitV2)
                    {
                        var nextEffective = next.m_UnityMinVersionMajor;
                        // If next minor is larger than .0f1, add 1 to the effective major. For example,
                        // if prev is 2019.1 and next is 2019.1.8f, the next effective major is 2019.2,
                        // causing a check for supported editors in the range 2019.1 <= V < 2019.2.
                        if (!next.m_UnityMinVersion.IsAlphaBetaOr0F1) nextEffective = nextEffective.Next;

                        try
                        {
                            // Don't emit the v2 errors if it's a permitted increase.
                            emitV2 = !m_UnityMinVersionMajor.Equals(nextEffective)
                                && context.HasSupportedEditorsInRange(m_UnityMinVersionMajor, nextEffective);
                        }
                        catch (Verifier.SkipAllException e)
                        {
                            emitV2 = false;
                            context.Skip(checkIds[1], e.Message);
                        }
                    }
                }
                else if (!allowDecrease)
                {
                    // It is an error to lower the min-version in patch versions (PVP-171-{1,2}).
                    error = $"{next.PackageId} {next.m_UnityMinVersion.RequiresSuchAndSuch}, but {PackageId} {m_UnityMinVersion.RequiresSuchAndSuch}";
                }

                if (error != null)
                {
                    foreach (var checkId in checkIds)
                    {
                        if (checkId != null) context.AddError(checkId, error);
                        if (!emitV2) break; // Only add the PVP-*-1 error.
                    }
                }
            }

            public void CheckUnityMinVersionMajorUpwards(ManifestData next, Verifier.Context context, string checkId)
            {
                if (this.m_UnityMinVersionError != null) context.AddError(checkId, this.m_UnityMinVersionError);
                if (next.m_UnityMinVersionError != null) context.AddError(checkId, next.m_UnityMinVersionError);

                if (!m_Present || !next.m_Present) return;

                if (m_UnityMinVersion.Major != next.m_UnityMinVersion.Major)
                {
                    // It is an error to change the min-version major in patch versions (PVP-171-3).
                    context.AddError(checkId, $"{next.PackageId} {next.m_UnityMinVersion.RequiresSuchAndSuch}, but {PackageId} {m_UnityMinVersion.RequiresSuchAndSuch}");
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
            context.IsLegacyCheckerEmittingLegacyJsonErrors = true;
            _ = context.HttpClient; // Bail early if running offline.

            var versionJson = context.ManifestPermitInvalidJson["version"];
            if (!VersionTriple.TryParseIgnoringPrereleaseAndBuildInfo(versionJson.String, out var triple)
                || triple.Major == uint.MaxValue
                || triple.Minor == uint.MaxValue
                || triple.Patch == uint.MaxValue)
                throw versionJson.GetException("not an acceptable SemVer version string");

            // No compatibility requirements for a 0.x package.
            if (triple.Major == 0) return;

            var underTestData = new ManifestData(context.ManifestPermitInvalidJson);
            var previousQuery = Query(SemVerQuery.Op.LessThan, triple);
            var prevPatchData = GetData(previousQuery, requireSameMinor: true);
            var prevMinorData = GetData(previousQuery);
            var nextPatchData = GetData(Query(SemVerQuery.Op.GreaterThanOrEqual, triple.NextPatch), requireSameMinor: true);
            var nextMinorData = GetData(Query(SemVerQuery.Op.GreaterThanOrEqual, triple.NextMinor));

            // If editor min-version of package under test is malformed, emit PVP-{171,181}-2 errors for that
            // (and nothing else). If min-version of prev/next package is malformed, that package is ignored.
            var underTestBadMinVersion = underTestData.UnityMinVersionV2Error != null;
            if (underTestBadMinVersion)
            {
                context.AddError("PVP-171-2", underTestData.UnityMinVersionV2Error);
                context.AddError("PVP-181-2", underTestData.UnityMinVersionV2Error);
            }

            // The Unity min-version checks are directional.
            // Note that 'prevPatch' and 'prevMinor' may be the same version.
            prevPatchData.CheckUnityMinVersionUpwards(underTestData, context, k_171_12, allowDecrease: false);
            underTestData.CheckUnityMinVersionUpwards(nextPatchData, context, k_171_12, allowDecrease: false);

            // The Unity min-version major check is directional.
            prevPatchData.CheckUnityMinVersionMajorUpwards(underTestData, context, "PVP-171-3");
            underTestData.CheckUnityMinVersionMajorUpwards(nextPatchData, context, "PVP-171-3");

            // For consistency with the backwards looking check, also check PVP-181-2 for nextPatchData.
            prevMinorData.CheckUnityMinVersionUpwards(underTestData, context, k_181, allowDecrease: true);
            underTestData.CheckUnityMinVersionUpwards(nextPatchData, context, k_181_2, allowDecrease: true);
            underTestData.CheckUnityMinVersionUpwards(nextMinorData, context, k_181, allowDecrease: true);

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
