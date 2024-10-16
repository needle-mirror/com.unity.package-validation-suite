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
        public static string[] Checks { get; } = { "PVP-100-1", "PVP-100-2" };
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
            public Requirement(string error, Regex regex) : this(error, json => regex.IsMatch(json.String)) { }

            // Allow implicit conversion from Regex, which (unlike a helper method) gets us proper syntax highlighting.
            public static implicit operator Requirement(Regex regex)
                => new Requirement(
                    regex.Options.HasFlag(RegexOptions.IgnoreCase) ? $"must match {regex} (case insensitive)" : $"must match {regex}",
                    regex
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

        static readonly Requirement k_Boolean = new Requirement(ctx => { _ = ctx.Target.Boolean; });
        static readonly Requirement k_String = new Requirement(ctx => { _ = ctx.Target.String; });
        static readonly Requirement k_Object = new Requirement(ctx => { _ = ctx.Target.RawObject; });
        static readonly Requirement k_ObjectOrString = new Requirement(ctx =>
        {
            if (!ctx.Target.IsObject && !ctx.Target.IsString)
                throw ctx.Target.GetException($"was {ctx.Target.KindName}, expected object or string");
        });

        static readonly Requirement k_NonEmpty = new Requirement("must be a non-empty string", json => json.String != "");
        static Requirement MaxLength(int max) => new Requirement($"must be a string with maximum length {max}", json => json.String.Length <= max);

        static Requirement MustNotStartWith(string prefix, bool caseSensitive)
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var error = caseSensitive ? $"must not start with '{prefix}'" : $"must not start with '{prefix}' (case insensitive)";
            return new Requirement(error, json => !json.String.StartsWith(prefix, comparison));
        }

        static Requirement MustNotEndWith(string suffix, bool caseSensitive)
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var error = caseSensitive ? $"must not end with '{suffix}'" : $"must not end with '{suffix}' (case insensitive)";
            return new Requirement(error, json => !json.String.EndsWith(suffix, comparison));
        }

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
                origin = url.Slice(i + 3);
                if (origin.TryIndexOf('/', out i)) origin.Length = i;
                if (origin.TryIndexOf('@', out i)) origin.Start += i + 1;

                prefix = url.Slice(0, origin.Start);

                organization = url.Slice(origin.End);
                if (organization.Length != 0) organization.Start += 1; // skip '/'
                if (organization.TryIndexOf('/', out i)) organization.Length = i + 1;
            }
            else if (i > 0) // SCP syntax (user@host:path)
            {
                validUrlPrefixes = k_ValidRepositoryScpPrefixes;
                origin = url.Slice(0, i);
                if (origin.TryIndexOf('@', out i)) origin.Start += i + 1;

                prefix = url.Slice(0, origin.Start);

                organization = url.Slice(origin.End + 1);
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

        // https://github.com/hapijs/joi/blob/v17.13.3/lib/types/string.js#L649
        // https://github.com/hapijs/address/blob/v4.1.5/lib/uri.js
        static readonly Regex k_RegistryUrlRegex = new Regex(@"^(?=.)(?!https?:\/(?:$|[^/]))(?!https?:\/\/\/)(?!https?:[^/])(?:[a-zA-Z][a-zA-Z\d+-\.]*:(?:(?:\/\/(?:[\w-\.~%\dA-Fa-f!\$&'\(\)\*\+,;=:]*@)?(?:\[(?:(?:(?:[\dA-Fa-f]{1,4}:){6}(?:[\dA-Fa-f]{1,4}:[\dA-Fa-f]{1,4}|(?:(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.){3}(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5]))|::(?:[\dA-Fa-f]{1,4}:){5}(?:[\dA-Fa-f]{1,4}:[\dA-Fa-f]{1,4}|(?:(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.){3}(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5]))|(?:[\dA-Fa-f]{1,4})?::(?:[\dA-Fa-f]{1,4}:){4}(?:[\dA-Fa-f]{1,4}:[\dA-Fa-f]{1,4}|(?:(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.){3}(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5]))|(?:(?:[\dA-Fa-f]{1,4}:){0,1}[\dA-Fa-f]{1,4})?::(?:[\dA-Fa-f]{1,4}:){3}(?:[\dA-Fa-f]{1,4}:[\dA-Fa-f]{1,4}|(?:(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.){3}(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5]))|(?:(?:[\dA-Fa-f]{1,4}:){0,2}[\dA-Fa-f]{1,4})?::(?:[\dA-Fa-f]{1,4}:){2}(?:[\dA-Fa-f]{1,4}:[\dA-Fa-f]{1,4}|(?:(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.){3}(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5]))|(?:(?:[\dA-Fa-f]{1,4}:){0,3}[\dA-Fa-f]{1,4})?::[\dA-Fa-f]{1,4}:(?:[\dA-Fa-f]{1,4}:[\dA-Fa-f]{1,4}|(?:(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.){3}(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5]))|(?:(?:[\dA-Fa-f]{1,4}:){0,4}[\dA-Fa-f]{1,4})?::(?:[\dA-Fa-f]{1,4}:[\dA-Fa-f]{1,4}|(?:(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.){3}(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5]))|(?:(?:[\dA-Fa-f]{1,4}:){0,5}[\dA-Fa-f]{1,4})?::[\dA-Fa-f]{1,4}|(?:(?:[\dA-Fa-f]{1,4}:){0,6}[\dA-Fa-f]{1,4})?::)|v[\dA-Fa-f]+\.[\w-\.~!\$&'\(\)\*\+,;=:]+)\]|(?:(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.){3}(?:0{0,2}\d|0?[1-9]\d|1\d\d|2[0-4]\d|25[0-5])|[\w-\.~%\dA-Fa-f!\$&'\(\)\*\+,;=]{1,255})(?::\d*)?(?:\/[\w-\.~%\dA-Fa-f!\$&'\(\)\*\+,;=:@]*)*)|\/(?:[\w-\.~%\dA-Fa-f!\$&'\(\)\*\+,;=:@]+(?:\/[\w-\.~%\dA-Fa-f!\$&'\(\)\*\+,;=:@]*)*)?|[\w-\.~%\dA-Fa-f!\$&'\(\)\*\+,;=:@]+(?:\/[\w-\.~%\dA-Fa-f!\$&'\(\)\*\+,;=:@]*)*|(?:\/\/\/[\w-\.~%\dA-Fa-f!\$&'\(\)\*\+,;=:@]*(?:\/[\w-\.~%\dA-Fa-f!\$&'\(\)\*\+,;=:@]*)*)))(?:\?[\w-\.~%\dA-Fa-f!\$&'\(\)\*\+,;=:@\/\?]*(?=#|$))?(?:#[\w-\.~%\dA-Fa-f!\$&'\(\)\*\+,;=:@\/\?]*)?$", RegexOptions.ECMAScript);
        internal static readonly Requirement MustSatisfyPackageRegistryUrlCheck = new Requirement("must be a valid RFC 3986 URI string", json => k_RegistryUrlRegex.IsMatch(json.String));

        // https://github.com/hapijs/joi/blob/v17.13.3/lib/types/string.js#L290
        // https://github.com/hapijs/address/blob/v4.1.5/lib/email.js
        static readonly Regex k_NonAsciiRegex = new Regex(@"[^\x00-\x7f]", RegexOptions.ECMAScript);
        static readonly Regex k_AtextRegex = new Regex(@"^[\w!#\$%&'\*\+\-/=\?\^`\{\|\}~]+$", RegexOptions.ECMAScript);
        static readonly Regex k_AtomRegex = new Regex(@"(?:[\xc2-\xdf][\x80-\xbf])|(?:\xe0[\xa0-\xbf][\x80-\xbf])|(?:[\xe1-\xec][\x80-\xbf]{2})|(?:\xed[\x80-\x9f][\x80-\xbf])|(?:[\xee-\xef][\x80-\xbf]{2})|(?:\xf0[\x90-\xbf][\x80-\xbf]{2})|(?:[\xf1-\xf3][\x80-\xbf]{3})|(?:\xf4[\x80-\x8f][\x80-\xbf]{2})", RegexOptions.ECMAScript);
        static readonly Regex k_DomainControlRegex = new Regex(@"[\x00-\x20@\:\/\\#!\$&\'\(\)\*\+,;=\?]", RegexOptions.ECMAScript);
        static readonly Regex k_DomainSegmentRegex = new Regex("^[a-zA-Z0-9](?:[a-zA-Z0-9\\-]*[a-zA-Z0-9])?$", RegexOptions.ECMAScript);
        static readonly Regex k_TldSegmentRegex = new Regex("^[a-zA-Z](?:[a-zA-Z0-9\\-]*[a-zA-Z0-9])?$", RegexOptions.ECMAScript);
        internal static readonly Requirement MustSatisfyPackageRegistryEmailCheck = new Requirement("must be a valid RFC 5321 email string", json =>
        {
            var email = json.String;
            if (email.Length == 0) return false;
            var ascii = !k_NonAsciiRegex.IsMatch(email);
            if (ascii) email = email.Normalize();
            if (email.Length > 254) return false;
            var localLen = email.IndexOf('@');
            if (localLen <= 0) return false;
            if (email.IndexOf('@', localLen + 1) != -1) return false;
            if (Encoding.UTF8.GetByteCount(email, 0, localLen) > 64) return false;
            var utf8 = ascii ? null : new byte[4];
            for (int start = 0, end; start <= localLen; start = end + 1)
            {
                var i = email.IndexOf('.', start);
                end = i == -1 || i >= localLen ? localLen : i;
                if (start == end) return false;
                if (ascii)
                {
                    if (!k_AtextRegex.Match(email, start, end - start).Success) return false;
                    continue;
                }
                for (i = start; i < end; i++)
                {
                    if (k_AtextRegex.Match(email, i, 1).Success) continue;
                    var count = Encoding.UTF8.GetBytes(email, i, 1, utf8, 0);
                    var utf8CodeUnits = new StringBuilder(count);
                    for (var j = 0; j < count; j++) utf8CodeUnits.Append((char)utf8[j]);
                    if (!k_AtomRegex.IsMatch(utf8CodeUnits.ToString())) return false;
                }
            }
            var domainStart = localLen + 1;
            if (domainStart == email.Length) return false;
            if (k_DomainControlRegex.Match(email, domainStart, email.Length - domainStart).Success) return false;
            // discrepancy: no punycode conversion
            // discrepancy: no hardcoded TLD allow-list: https://github.com/hapijs/address/blob/v4.1.5/lib/tlds.js
            for (int start = domainStart, end; start <= email.Length; start = end + 1)
            {
                var i = email.IndexOf('.', start);
                if (start == domainStart && i == -1) return false;
                end = i == -1 ? email.Length : i;
                if (start == end) return false;
                if (end - start > 63) return false;
                var regex = end == email.Length ? k_TldSegmentRegex : k_DomainSegmentRegex;
                if (!regex.Match(email, start, end - start).Success) return false;
            }
            return true;
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

        // Similar k_LocationChecks, but with the following changes:
        // - Emits an error for each item of an enumerable location throwing SimpleJsonException.
        // - Emits .FullMessage instead of .LegacyMessage as error on thrown SimpleJsonException.
        // - Error (from requirement.TryGetError) is prefixed with "package.json: ".
        static readonly (string, Func<Json, object>, Requirement)[] k_LocationChecksV2 =
        {
            // https://github.com/Unity-Technologies/assetstore-upm-registry/blob/v10.3.0/server/models/upm/package-manifest.ts#L83
            ("PVP-101-2", m => m["name"], PackageId.ValidName),
            ("PVP-101-2", m => m["name"], MustNotStartWith("com.unity.modules.", caseSensitive: true)),
            ("PVP-101-2", m => m["name"], MustNotEndWith(".plugin", caseSensitive: false)),
            ("PVP-101-2", m => m["name"], MustNotEndWith(".framework", caseSensitive: false)),
            ("PVP-101-2", m => m["name"], MustNotEndWith(".bundle", caseSensitive: false)),
            ("PVP-101-2", m => m["version"], new Requirement("must be valid SemVer", PackageId.ValidSemVer)),
            ("PVP-101-2", m => m["description"].IfPresent, MaxLength(4096)),
            ("PVP-101-2", m => m["displayName"].IfPresent, MaxLength(256)),
            ("PVP-101-2", m => m["dependencies"].MembersIfPresent, k_String),
            ("PVP-101-2", m => m["documentationUrl"].IfPresent, MustSatisfyPackageRegistryUrlCheck),
            ("PVP-101-2", m => m["documentationUrl"].IfPresent, MaxLength(256)),
            ("PVP-101-2", m => m["license"].IfPresent, MaxLength(256)),
            ("PVP-101-2", m => m["licensesUrl"].IfPresent, MustSatisfyPackageRegistryUrlCheck),
            ("PVP-101-2", m => m["licensesUrl"].IfPresent, MaxLength(256)),
            ("PVP-101-2", m => m["keywords"].ElementsIfPresent, MaxLength(32)),
            ("PVP-101-2", m => m["hideInEditor"].IfPresent, k_Boolean),
            ("PVP-101-2", m => m["unity"].IfPresent, MaxLength(64)),
            ("PVP-101-2", m => m["unityRelease"].IfPresent, MaxLength(64)),
            ("PVP-101-2", m => m["author"].IfPresent, k_ObjectOrString),
            ("PVP-101-2", m => m["author"].IfString, new Requirement("must be an object or a string with maximum length 64", json => json.String.Length <= 64)),
            ("PVP-101-2", m => m["author"].IfObject?["name"].IfPresent, MaxLength(64)),
            ("PVP-101-2", m => m["author"].IfObject?["email"].IfPresent, MustSatisfyPackageRegistryEmailCheck),
            ("PVP-101-2", m => m["author"].IfObject?["email"].IfPresent, MaxLength(64)),
            ("PVP-101-2", m => m["author"].IfObject?["url"].IfPresent, MustSatisfyPackageRegistryUrlCheck),
            ("PVP-101-2", m => m["author"].IfObject?["url"].IfPresent, MaxLength(256)),
            ("PVP-101-2", m => m["changelogUrl"].IfPresent, MustSatisfyPackageRegistryUrlCheck),
            ("PVP-101-2", m => m["changelogUrl"].IfPresent, MaxLength(256)),
            ("PVP-101-2", m => m["type"].IfPresent, MaxLength(32)),
            ("PVP-101-2", m => m["samples"].ElementsIfPresent, k_Object),
            ("PVP-101-2", m => m["samples"].ElementsIfPresent.Select(e => e.IfObject?["displayName"]), MaxLength(256)),
            ("PVP-101-2", m => m["samples"].ElementsIfPresent.Select(e => e.IfObject?["description"]), MaxLength(4096)),
            ("PVP-101-2", m => m["samples"].ElementsIfPresent.Select(e => e.IfObject?["path"]), MaxLength(512)),
            ("PVP-101-2", m => m["_upm"].IfPresent?["gameService"].IfPresent, k_Object),
            ("PVP-101-2", m => m["_upm"].IfPresent?["changelog"].IfPresent, MaxLength(4096)),
        };

        public static string[] Checks { get; } = k_LocationChecks.Select(v => v.Item1).Concat(k_LocationChecksV2.Select(v => v.Item1)).Distinct().ToArray();
        public static int PassCount => 0;

        public ManifestVerifier(Verifier.Context context)
        {
            context.IsLegacyCheckerEmittingLegacyJsonErrors = true;
            var manifest = context.ManifestPermitInvalidJson;
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
                    context.AddError(checkId, e.LegacyMessage);
                }
            }

            foreach (var (checkId, locationFunc, requirement) in k_LocationChecksV2)
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
                        try
                        {
                            if (requirement.TryGetError(location, scratch, out var error))
                            {
                                context.AddError(checkId, "package.json: " + error);
                            }
                        }
                        catch (SimpleJsonException e)
                        {
                            context.AddError(checkId, e.FullMessage);
                        }
                    }
                }
                catch (SimpleJsonException e)
                {
                    context.AddError(checkId, e.FullMessage);
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
