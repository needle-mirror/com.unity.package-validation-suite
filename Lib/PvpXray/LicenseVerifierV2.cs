using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PvpXray
{
    class LicenseVerifierV2 : Verifier.IChecker
    {
        public static int PassCount => 1;
        public static string[] Checks { get; } = {
            "PVP-30-2", // LICENSE.md exists
            "PVP-31-2", // LICENSE.md contains valid copyright notice
        };

        const string k_License = "LICENSE.md";
        static readonly Regex k_Copyright = new Regex(@"(?:(?:copyright|\u00a9|\(c\))[ \t]*)+[0-9]{4}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        readonly Verifier.Context m_Context;

        public LicenseVerifierV2(Verifier.Context context)
        {
            m_Context = context;

            if (!context.IsFeatureSetPackage && !context.Files.Contains(k_License))
            {
                context.AddError("PVP-30-2", $"{k_License}: file not found");
            }
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            if (file.Path != k_License) return;

            string license;
            try
            {
                license = file.ReadToStringLax();
            }
            catch (Verifier.FailAllException e)
            {
                m_Context.AddError("PVP-31-2", e.Message);
                return;
            }

            var match = k_Copyright.Match(license);
            if (!match.Success)
            {
                m_Context.AddError("PVP-31-2", $"{k_License}: copyright notice not found in file");
                return;
            }

            var copyright = license.Slice(match.Index, match.Index + match.Length);
            var line = ExpandLine(copyright).Trim(' ', '\t');
            var linePrefix = license.Slice(line.Start, copyright.Start);
            var lineSuffix = license.Slice(copyright.End, line.End);

            string packageName;
            string displayName;
            try
            {
                packageName = m_Context.Manifest["name"].IfString?.String;
                displayName = m_Context.Manifest["displayName"].IfString?.String;
            }
            catch (SimpleJsonException)
            {
                packageName = null;
                displayName = null;
            }

            var validName =
                packageName != null && linePrefix.StartsWith(packageName) && linePrefix.Slice(packageName.Length).Equals(" ") ||
                displayName != null && linePrefix.StartsWith(displayName) && linePrefix.Slice(displayName.Length).Equals(" ");

            const string copyrightPrefix = "copyright \u00a9 ";
            var validCopyright = copyright.StartsWith(copyrightPrefix) && copyright.Slice(copyrightPrefix.Length, copyright.Length - 2).Equals("20");

            const string entityPrefix = " Unity Technologies";
            var validEntity = lineSuffix.StartsWith(entityPrefix) && (lineSuffix.Length == entityPrefix.Length || !Net7Compat.IsAsciiLetterOrDigit(lineSuffix[entityPrefix.Length]));

            if (validName && validCopyright && validEntity) return;

            m_Context.AddError("PVP-31-2", $"{k_License}: bad copyright notice: {line}");

            if (linePrefix.Length == 0 || !validCopyright || !validEntity)
            {
                m_Context.AddError("PVP-31-2", $"{k_License}: copyright notice should begin: PACKAGE-NAME copyright \u00a9 YEAR Unity Technologies");
            }

            if (!validName && linePrefix.Length != 0)
            {
                var error = new StringBuilder(k_License, 256).Append(": copyright notice should begin with the package name");
                if (packageName != null) error.Append(" \"").Append(packageName).Append('"');
                error.Append(" or display name");
                if (displayName != null) error.Append(" \"").Append(displayName).Append('"');
                m_Context.AddError("PVP-31-2", error.ToString());
            }

            if (!validEntity && lineSuffix.Length >= 2 && lineSuffix[0] == '-' && Net7Compat.IsAsciiDigit(lineSuffix[1]))
            {
                m_Context.AddError("PVP-31-2", $"{k_License}: copyright notice should specify the year the code was first published, not a year range");
            }
        }

        static StringSlice ExpandLine(StringSlice slice)
        {
            var start = slice.String.LastIndexOf('\n', slice.Start) + 1;
            var end = slice.String.IndexOf('\n', slice.End);
            if (end == -1) end = slice.String.Length;
            if (end != 0 && slice.String[end - 1] == '\r') end--;
            return new StringSlice { String = slice.String, Start = start, End = end };
        }

        public void Finish()
        {
        }
    }
}
