using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PvpXray
{
    static class ManifestValidations
    {
        const RegexOptions k_IgnoreCase = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant; // IgnoreCase MUST be used with CultureInvariant.

        public struct Requirement
        {
            public string Message;
            public Func<Json, bool> Func;

            // Allow implicit conversion from Regex, which (unlike a helper method) gets us proper syntax highlighting.
            public static implicit operator Requirement(Regex regex)
                => new Requirement
                {
                    Message = regex.Options.HasFlag(RegexOptions.IgnoreCase) ? $"must match {regex} (case insensitive)" : $"must match {regex}",
                    Func = json => regex.IsMatch(json.String),
                };
        }

        static readonly Regex k_ValidPackageName = new Regex("^[a-z0-9][-._a-z0-9]{0,213}$");

        public static readonly Regex SemVer = new Regex(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$");

        static Requirement Fail(string message) => new Requirement { Message = message, Func = _ => false };

        static Json Unless(this Json elm, bool condition) => condition ? null : elm;

        static IEnumerable<Json> Unless(this IEnumerable<Json> elm, bool condition) => condition ? null : elm;

        static bool IsUnityPackage(Json manifest) => manifest["name"].String.StartsWithOrdinal("com.unity.");

        static readonly Requirement k_NonEmpty = new Requirement { Message = "must be a non-empty string", Func = json => json.String != "" };

        static readonly Requirement k_ValidSHA1Hash = new Regex("^[0-9a-f]{40}$");

        static readonly Requirement k_ValidCompany = new Regex(@"^(com\.unity\.|com\.autodesk\.|com\.havok\.|com\.ptc\.)");

        static Requirement Literal(string literal) => new Requirement { Message = $"must be the literal string \"{literal}\"", Func = json => json.String == literal };

        // REMEMBER: Checks must not be changed once added. Any modifications must be implemented as a NEW check.
        // These checks first selects one or more locations in the manifest, then applies a requirement to these locations.
        // This split allows for better and more consistent error reporting.
        static readonly (string, Func<Json, object>, Requirement)[] k_LocationChecks =
        {
            ("PVP-101-1", m => m["name"], new Regex(@"$(?<!\.plugin|\.framework|\.bundle)", k_IgnoreCase)), // Unity technical restriction
            ("PVP-101-1", m => m["name"], k_ValidPackageName),
            ("PVP-101-1", m => m["version"], SemVer),

            ("PVP-102-1", m => m["displayName"], new Regex("^[ a-zA-Z0-9]{1,50}$")),

            ("PVP-104-1", m => m["displayName"], new Regex(@"^(?!unity).*$", k_IgnoreCase)),

            ("PVP-102-1", m => m["unity"].IfPresent, new Regex(@"^[0-9]{4}\.[1-9][0-9]*$")),
            ("PVP-102-1", m => m["unityRelease"].IfPresent, new Regex(@"^[0-9]+[abf][1-9][0-9]*$")),
            ("PVP-102-1", m => m["unityRelease"].IfPresent.Unless(m["unity"].IsPresent), Fail("requires that the 'unity' key is present")),
            ("PVP-102-1", m => m["dependencies"].MembersIfPresent.Where(e => !k_ValidPackageName.IsMatch(e.Key)), Fail($"key must match {k_ValidPackageName}")),
            ("PVP-102-1", m => m["dependencies"].MembersIfPresent, SemVer),

            ("PVP-102-2", m => m["displayName"], new Regex("^[ a-zA-Z0-9]{1,50}$")),
            ("PVP-102-2", m => m["unity"].IfPresent, new Regex(@"^[0-9]{4}\.[1-9]$")),
            ("PVP-102-2", m => m["unityRelease"].IfPresent, new Regex(@"^[0-9]+[abf][1-9][0-9]*$")),
            ("PVP-102-2", m => m["unityRelease"].IfPresent.Unless(m["unity"].IsPresent), Fail("requires that the 'unity' key is present")),
            ("PVP-102-2", m => m["dependencies"].MembersIfPresent.Where(e => !k_ValidPackageName.IsMatch(e.Key)), Fail($"key must match {k_ValidPackageName}")),
            ("PVP-102-2", m => m["dependencies"].MembersIfPresent.Unless(m["type"].IsPresent && m["type"].String == "feature"), SemVer),

            ("PVP-108-1", m => m["description"], new Regex("(?s)^.{50,}")),

            ("PVP-103-1", m => m.Unless(!IsUnityPackage(m))?["author"].IfPresent, Fail("must not be specified in Unity packages")),
            ("PVP-103-1", m => m.Unless(IsUnityPackage(m))?["author"]["name"], k_NonEmpty),

            ("PVP-105-1", m => m["name"], k_ValidCompany),

            ("PVP-111-1", m => m["repository"]["url"], k_NonEmpty),
            ("PVP-111-1", m => m["repository"]["revision"], k_ValidSHA1Hash),

            ("PVP-112-1", m => m["dependencies"].MembersIfPresent.Unless(m["type"].IfPresent?.String != "feature"), Literal("default")),
        };

        public static readonly string[] Checks = k_LocationChecks.Select(v => v.Item1).Distinct().ToArray();

        public static void Run(Validator.Context context)
        {
            var manifest = context.Manifest;

            var arrayOfOne = new Json[1];
            foreach (var (checkId, locationFunc, requirement) in k_LocationChecks)
            {
                try
                {
                    var locations = locationFunc(manifest);

                    var enumerable = locations as IEnumerable<Json>;
                    if (enumerable == null)
                    {
                        // If locations is not an IEnumerable, it should be a single Json element (or null).
                        arrayOfOne[0] = (Json)locations;
                        enumerable = arrayOfOne;
                    }

                    foreach (var location in enumerable)
                    {
                        if (location == null) continue;
                        if (!requirement.Func(location))
                        {
                            context.AddError(checkId, $"{location.Path}: {requirement.Message}");
                        }
                    }
                }
                catch (JsonException e)
                {
                    context.AddError(checkId, e.Message);
                }
            }
        }
    }
}
