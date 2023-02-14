using System.Text.RegularExpressions;

namespace PvpXray
{
    static class LicenseVerifier
    {
        const string k_License = "LICENSE.md";
        const string k_Manifest = "package.json";

        static readonly Regex k_CopyrightNotice = new Regex(@"^(?<name>.*?) copyright \u00a9 \d+ \S(.*\S)?(?:\r?\n|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static readonly string[] Checks =
        {
            "PVP-30-1", // LICENSE.md file (US-0032)
            "PVP-31-1", // LICENSE.md copyright notice (US-0032)
        };

        public static void Run(Verifier.Context context)
        {
            var license = context.ReadFileToString(k_License);

            if (license == "")
            {
                context.AddError("PVP-30-1", $"{k_License}: license file must not be empty");
            }

            var match = k_CopyrightNotice.Match(license);
            if (match.Success)
            {
                try
                {
                    var lowerNameInNotice = match.Groups["name"].Value.ToLowerInvariant();
                    var lowerName = context.Manifest["name"].String.ToLowerInvariant();
                    var lowerDisplayName = context.Manifest["displayName"].String.ToLowerInvariant();
                    if (lowerNameInNotice != lowerName && lowerNameInNotice != lowerDisplayName)
                    {
                        context.AddError("PVP-31-1", $"{k_License}: name in copyright notice must match either name or displayName of package");
                    }
                }
                catch (JsonException e)
                {
                    context.AddError("PVP-31-1", $"{k_Manifest}: {e.Message}");
                }
            }
            else
            {
                context.AddError("PVP-31-1", $"{k_License}: license must match regex: {k_CopyrightNotice}");
            }
        }
    }
}
