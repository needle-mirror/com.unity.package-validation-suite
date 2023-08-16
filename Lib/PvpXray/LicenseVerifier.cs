using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PvpXray
{
    class LicenseVerifier : Verifier.IChecker
    {
        const string k_License = "LICENSE.md";

        static readonly Regex k_CopyrightNotice = new Regex(@"^(?<name>.*?) copyright \u00a9 \d+ \S(.*\S)?(?:\r?\n|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string[] Checks => new[]
        {
            "PVP-30-1", // LICENSE.md file (US-0032)
            "PVP-31-1", // LICENSE.md copyright notice (US-0032)
        };

        public static int PassCount => 1;

        readonly Verifier.IContext m_Context;

        public LicenseVerifier(Verifier.IContext context)
        {
            m_Context = context;

            if (!context.Files.Contains(k_License))
            {
                throw new Verifier.FailAllException($"{k_License}: file could not be read as UTF-8 text");
            }
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            if (file.Path != k_License) return;

            var license = file.ReadToStringLegacy();

            if (license == "")
            {
                m_Context.AddError("PVP-30-1", $"{k_License}: license file must not be empty");
            }

            var match = k_CopyrightNotice.Match(license);
            if (match.Success)
            {
                try
                {
                    var lowerNameInNotice = match.Groups["name"].Value.ToLowerInvariant();
                    var lowerName = m_Context.Manifest["name"].String.ToLowerInvariant();
                    var lowerDisplayName = m_Context.Manifest["displayName"].String.ToLowerInvariant();
                    if (lowerNameInNotice != lowerName && lowerNameInNotice != lowerDisplayName)
                    {
                        m_Context.AddError("PVP-31-1", $"{k_License}: name in copyright notice must match either name or displayName of package");
                    }
                }
                catch (SimpleJsonException e)
                {
                    m_Context.AddError("PVP-31-1", e.FullMessage);
                }
            }
            else
            {
                m_Context.AddError("PVP-31-1", $"{k_License}: license must match regex: {k_CopyrightNotice}");
            }
        }

        public void Finish()
        {
        }
    }
}
