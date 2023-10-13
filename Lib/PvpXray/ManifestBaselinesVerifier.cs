using System;
using System.Collections.Generic;
using System.Linq;

namespace PvpXray
{
    class ManifestBaselinesVerifier : Verifier.IChecker
    {
        public static string[] Checks => new[]
        {
            "PVP-161-1", // The editor min-version of recursive dependencies may not be higher than package's own min-version (ignoring built-in or missing packages).
            "PVP-162-1", // The recursive dependency chain of the package may not contain cycles (ignoring built-in or missing packages).
        };
        public static int PassCount => 0;

        internal struct UnityVersionRequirement
        {
            public readonly string Major;
            public readonly string Minor;

            public bool IsAny => Major == null;

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
        }

        // .NET 7 built-in
        static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

        static void NaturalCompareReadComponent(string str, int startIndex, out int endIndex, out bool isNumber)
        {
            isNumber = startIndex < str.Length && IsAsciiDigit(str[startIndex]);
            for (endIndex = startIndex; endIndex < str.Length && IsAsciiDigit(str[endIndex]) == isNumber; ++endIndex)
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

        public ManifestBaselinesVerifier(Verifier.IContext context)
        {
            _ = context.HttpClient; // Bail early if running offline.

            var alreadyProcessed = new HashSet<PackageId>();
            var packageUnderTest = new PackageId(context.Manifest);
            var packageUnderTestMinUnity = new UnityVersionRequirement(context.Manifest);
            var path = new List<PackageId>();

            void AddErrorsForAll(string message)
            {
                foreach (var check in Checks)
                {
                    context.AddError(check, message);
                }
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
                    if (package == packageUnderTest) manifest = context.Manifest;
                    else if (context.TryFetchPackageBaseline(package, out var baseline)) manifest = baseline.Manifest;
                    else return;

                    var minUnity = new UnityVersionRequirement(manifest);
                    if (!minUnity.IsAny)
                    {
                        if (packageUnderTestMinUnity.IsAny)
                        {
                            context.AddError("PVP-161-1", $"{package} requires Unity {minUnity.Expand(null)}, but {packageUnderTest} does not specify a minimum Unity version");
                        }
                        else if (NaturalCompare(minUnity.Expand(".0f1"), packageUnderTestMinUnity.Expand(".0f1")) > 0)
                        {
                            context.AddError("PVP-161-1", $"{package} requires Unity {minUnity.Expand(null)}, but {packageUnderTest} only requires {packageUnderTestMinUnity.Expand(null)}");
                        }
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
                    AddErrorsForAll($"{package}: {e.Message}");
                }
                finally
                {
                    path.RemoveAt(path.Count - 1);
                }
            }

            WalkDependencies(packageUnderTest);
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex) => throw new NotImplementedException();
        public void Finish() { }
    }
}
