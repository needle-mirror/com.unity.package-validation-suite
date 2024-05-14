using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PvpXray
{
    static class ManifestVerifierExtensions
    {
        public static Json Unless(this Json elm, bool condition) => condition ? null : elm;
        public static IEnumerable<Json> Unless(this IEnumerable<Json> elm, bool condition) => condition ? null : elm;
    }

    // These checks are special (because they're actually checked in the
    // Context), and must be handled in a separate checker in case the
    // main ManifestVerifier checker throws FailAll.
    class ManifestContextVerifier : Verifier.IChecker
    {
        public static string[] Checks => new[] { "PVP-100-1", "PVP-100-2" };
        public static int PassCount => 0;

        public ManifestContextVerifier(Verifier.Context context)
        {
            foreach (var (checkId, error) in context.ManifestContextErrors)
                context.AddError(checkId, error);
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex) { }
        public void Finish() { }
    }

    class ManifestVerifier : Verifier.IChecker
    {
        const RegexOptions k_IgnoreCase = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant; // IgnoreCase MUST be used with CultureInvariant.

        static bool IsUnityPackage(Json manifest) => manifest["name"].String.StartsWithOrdinal("com.unity.");

        public struct Requirement
        {
            public struct Ctx
            {
                public StringBuilder ErrorBuilder;
                public Json Target;

                public StringBuilder BeginError(string message)
                {
                    Target.AppendPathTo(ErrorBuilder);
                    return ErrorBuilder.Append(": ").Append(message);
                }
            }

            readonly Action<Ctx> m_EmitError;
            public Requirement(Action<Ctx> emitError) => m_EmitError = emitError;
            public Requirement(string error, Func<Json, bool> checkFunc)
            {
                m_EmitError = ctx =>
                {
                    if (!checkFunc(ctx.Target)) ctx.BeginError(error);
                };
            }

            // Allow implicit conversion from Regex, which (unlike a helper method) gets us proper syntax highlighting.
            public static implicit operator Requirement(Regex regex)
                => new Requirement(
                    regex.Options.HasFlag(RegexOptions.IgnoreCase) ? $"must match {regex} (case insensitive)" : $"must match {regex}",
                    json => regex.IsMatch(json.String)
                );

            public bool TryGetError(Json target, StringBuilder scratch, out string error)
            {
                scratch.Clear();
                m_EmitError(new Ctx { ErrorBuilder = scratch, Target = target });
                error = scratch.ToString();
                return error != "";
            }
        }

        static Requirement Fail(string message) => new Requirement(ctx => ctx.BeginError(message));

        static readonly Requirement k_NonEmpty = new Requirement("must be a non-empty string", json => json.String != "");

        static readonly Requirement k_ValidCompany = new Regex(@"^(com\.unity\.|com\.autodesk\.|com\.havok\.|com\.ptc\.)");

        static readonly string[] k_ValidRepositoryScpPrefixes = { "git@" };
        static readonly string[] k_ValidRepositoryUrlPrefixes = { "https://", "ssh://git@" };
        static readonly string[] k_ValidRepositoryOrigins = { "github.com", "github.cds.internal.unity3d.com" };
        static readonly string[] k_ValidRepositoryPathPrefixForOrigin = { "Unity-Technologies/", "unity/" };

        internal static readonly Requirement ValidRepositoryUrl = new Requirement(ctx =>
        {
            StringSlice prefix, origin, organization;
            string[] validUrlPrefixes;

            var url = ctx.Target.String;
            var i = url.IndexOf(':');
            if (i > 0 && i + 2 < url.Length && url[i + 1] == '/' && url[i + 2] == '/') // URL syntax (proto://user@host/path)
            {
                validUrlPrefixes = k_ValidRepositoryUrlPrefixes;
                origin = url.Slice(i + 3, url.Length);
                if (origin.TryIndexOf('/', out i)) origin.Length = i;
                if (origin.TryIndexOf('@', out i)) origin.Start += i + 1;

                prefix = url.Slice(0, origin.Start);

                organization = url.Slice(origin.End, url.Length);
                if (organization.Length != 0) organization.Start += 1; // skip '/'
                if (organization.TryIndexOf('/', out i)) organization.Length = i + 1;
            }
            else if (i > 0) // SCP syntax (user@host:path)
            {
                validUrlPrefixes = k_ValidRepositoryScpPrefixes;
                origin = url.Slice(0, i);
                if (origin.TryIndexOf('@', out i)) origin.Start += i + 1;

                prefix = url.Slice(0, origin.Start);

                organization = url.Slice(origin.End + 1, url.Length);
                if (organization.TryIndexOf('/', out i)) organization.Length = i + 1;
            }
            else
            {
                ctx.BeginError("invalid syntax in URL ").AppendAsJson(url);
                return;
            }

            if (!k_ValidRepositoryOrigins.TryIndexOf(origin, out var originIndex))
            {
                ctx.BeginError("invalid origin ").AppendAsJson(origin.ToString()).Append(" in URL ").AppendAsJson(url);
            }
            else if (!organization.Equals(k_ValidRepositoryPathPrefixForOrigin[originIndex]))
            {
                ctx.BeginError("invalid repository path ").AppendAsJson(organization.ToString()).Append(" in URL ").AppendAsJson(url);
            }
            else if (!validUrlPrefixes.TryIndexOf(prefix, out _))
            {
                ctx.BeginError("invalid URL prefix ").AppendAsJson(prefix.ToString()).Append(" in URL ").AppendAsJson(url);
            }
        });

        static readonly (string, string, string, string)[] k_NameAffixes = {
            (".tests", ".test", "tests", "test"),
            ("com.unity.feature.", "com.unity.features.", "feature", "features"),
            ("com.unity.modules.", "com.unity.module.", "module", "modules"),
            ("com.unity.template.", "com.unity.templates.", "template", "templates"),
        };
        static readonly Requirement k_ValidTypeForName = new Requirement(ctx =>
        {
            var packageName = ctx.Target["name"].String;
            var packageType = ctx.Target["type"].IfPresent?.String;

            foreach (var (correctAffix, wrongAffix, correctType, wrongType) in k_NameAffixes)
            {
                if (packageType == wrongType)
                {
                    ctx.ErrorBuilder.Append($".type: should not be \"{wrongType}\" (did you mean \"{correctType}\"?)");
                    return;
                }

                var isSuffix = correctAffix[0] == '.';
                var verb = isSuffix ? "end" : "start";

                var isWrongAffix = isSuffix ? packageName.EndsWithOrdinal(wrongAffix) : packageName.StartsWithOrdinal(wrongAffix);
                if (isWrongAffix)
                {
                    ctx.ErrorBuilder.Append($".name: should not {verb} with \"{wrongAffix}\" (did you mean \"{correctAffix}\"?)");
                    return;
                }

                var isCorrectAffix = isSuffix ? packageName.EndsWithOrdinal(correctAffix) : packageName.StartsWithOrdinal(correctAffix);
                if (isCorrectAffix != (packageType == correctType))
                {
                    var sb = ctx.ErrorBuilder.Append(".type: was ");
                    if (packageType == null) sb.Append("undefined");
                    else sb.AppendAsJson(packageType);
                    sb.Append($", but must be \"{correctType}\" if and only if .name {verb}s with \"{correctAffix}\"");
                    return;
                }

                if (isCorrectAffix) return;
            }
        });

        static Requirement Literal(string literal) => new Requirement($"must be the literal string \"{literal}\"", json => json.String == literal);

        // REMEMBER: Checks must not be changed once added. Any modifications must be implemented as a NEW check.
        // These checks first selects one or more locations in the manifest, then applies a requirement to these locations.
        // This split allows for better and more consistent error reporting.
        static readonly (string, Func<Json, object>, Requirement)[] k_LocationChecks =
        {
            ("PVP-101-1", m => m["name"], new Regex(@"$(?<!\.plugin|\.framework|\.bundle)", k_IgnoreCase)), // Unity technical restriction
            ("PVP-101-1", m => m["name"], PackageId.ValidName),
            ("PVP-101-1", m => m["version"], PackageId.ValidSemVer),

            ("PVP-104-1", m => m["displayName"], new Regex(@"^(?!unity).*$", k_IgnoreCase)),

            ("PVP-102-1", m => m["displayName"], new Regex("^[ a-zA-Z0-9]{1,50}$")),
            ("PVP-102-1", m => m["unity"].IfPresent, new Regex(@"^[0-9]{4}\.[1-9][0-9]*$")),
            ("PVP-102-1", m => m["unityRelease"].IfPresent, new Regex(@"^[0-9]+[abf][1-9][0-9]*$")),
            ("PVP-102-1", m => m["unityRelease"].IfPresent.Unless(m["unity"].IsPresent), Fail("requires that the 'unity' key is present")),
            ("PVP-102-1", m => m["dependencies"].MembersIfPresent.Where(e => !PackageId.ValidName.IsMatch(e.Key)), Fail($"key must match {PackageId.ValidName}")),
            ("PVP-102-1", m => m["dependencies"].MembersIfPresent, PackageId.ValidSemVer),

            ("PVP-102-2", m => m["displayName"], new Regex("^[ a-zA-Z0-9]{1,50}$")),
            ("PVP-102-2", m => m["unity"].IfPresent, new Regex(@"^[0-9]{4}\.[1-9]$")),
            ("PVP-102-2", m => m["unityRelease"].IfPresent, new Regex(@"^[0-9]+[abf][1-9][0-9]*$")),
            ("PVP-102-2", m => m["unityRelease"].IfPresent.Unless(m["unity"].IsPresent), Fail("requires that the 'unity' key is present")),
            ("PVP-102-2", m => m["dependencies"].MembersIfPresent.Where(e => !PackageId.ValidName.IsMatch(e.Key)), Fail($"key must match {PackageId.ValidName}")),
            ("PVP-102-2", m => m["dependencies"].MembersIfPresent.Unless(m["type"].IsPresent && m["type"].String == "feature"), PackageId.ValidSemVer),

            ("PVP-102-3", m => m["displayName"], new Regex("^[ a-zA-Z0-9]{1,50}$")),
            ("PVP-102-3", m => m["unity"].IfPresent, new Regex(@"^[0-9]{4}\.[0-9]$")),
            ("PVP-102-3", m => m["unityRelease"].IfPresent, new Regex(@"^[0-9]+[abf][1-9][0-9]*$")),
            ("PVP-102-3", m => m["unityRelease"].IfPresent.Unless(m["unity"].IsPresent), Fail("requires that the 'unity' key is present")),
            ("PVP-102-3", m => m["dependencies"].MembersIfPresent.Where(e => !PackageId.ValidName.IsMatch(e.Key)), Fail($"key must match {PackageId.ValidName}")),
            ("PVP-102-3", m => m["dependencies"].MembersIfPresent.Unless(m["type"].IsPresent && m["type"].String == "feature"), PackageId.ValidSemVer),

            ("PVP-108-1", m => m["description"], new Regex("(?s)^.{50,}")),

            ("PVP-103-1", m => m.Unless(!IsUnityPackage(m))?["author"].IfPresent, Fail("must not be specified in Unity packages")),
            ("PVP-103-1", m => m.Unless(IsUnityPackage(m))?["author"]["name"], k_NonEmpty),

            ("PVP-105-1", m => m["name"], k_ValidCompany),

            ("PVP-110-1", m => m["dist"].IfPresent, Fail("key must not be present")),

            ("PVP-111-1", m => m["repository"]["url"], k_NonEmpty),
            ("PVP-111-1", m => m["repository"]["revision"], XrayUtils.Sha1Regex),

            ("PVP-111-2", m => m["repository"]["url"], ValidRepositoryUrl),
            ("PVP-111-2", m => m["repository"]["revision"], XrayUtils.Sha1Regex),

            ("PVP-112-1", m => m["dependencies"].MembersIfPresent.Unless(m["type"].IfPresent?.String != "feature"), Literal("default")),

            ("PVP-113-1", m => m["type"].IfPresent, new Regex(@"^(feature|template)$")),

            ("PVP-114-1", m => m, k_ValidTypeForName),
        };

        public static string[] Checks => k_LocationChecks.Select(v => v.Item1).Distinct().ToArray();
        public static int PassCount => 0;

        public ManifestVerifier(Verifier.Context context)
        {
            var manifest = context.Manifest;
            var scratch = new StringBuilder();

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
                        if (requirement.TryGetError(location, scratch, out var error))
                        {
                            context.AddError(checkId, error);
                        }
                    }
                }
                catch (SimpleJsonException e)
                {
                    context.AddError(checkId, e.Message);
                }
            }
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            throw new InvalidOperationException();
        }

        public void Finish()
        {
        }
    }
}
