using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PvpXray
{
    class LifecycleVerifier : Verifier.IChecker
    {
        public static string[] Checks => new[] { "PVP-106-1" };

        public static int PassCount => 0;

        readonly string m_VersionOptionalLabels;

        readonly string m_Version;

        public LifecycleVerifier(Verifier.IContext context)
        {
            m_Version = context.Manifest["version"].IfPresent?.String;

            // if there is no version, no Lifecycle Tests can be run
            if (m_Version == null)
                return;

            var versionOptionalLabels = GetOptionalLabels();

            // if there are no labels, there are no Package Lifecycle Tests to run
            // This would be a Verified package in PlcV1 or a Release package for PlcV2
            if (versionOptionalLabels == "")
                return;

            m_VersionOptionalLabels = versionOptionalLabels;

            var tag = GetLabelSegment<string>(@"^(?<tag>\w+)", "tag");
            var iteration = GetLabelSegment<int>(@"^\w+(?:\-[a-zA-Z\-]+)?\.(?<iteration>[0-9]+)$", "iteration");
            var feature = GetLabelSegment<string>(@"^\w+\-(?<feature>[a-zA-Z\-]+)\.", "feature");

            if (tag == "preview")
            {
                if (m_VersionOptionalLabels.EndsWithOrdinal("preview"))
                    return;
                if (iteration == 0 || iteration > 999999)
                    context.AddError("PVP-106-1", "Package Lifecycle v1 iteration must be number between 1 and 999999 (or absent)");
                return;
            }

            if (tag != "pre" && tag != "exp")
            {
                context.AddError("PVP-106-1", "Valid pre-release tags are \"exp[-feature].N\" and \"pre.N\"");
                return;
            }

            if (iteration < 1)
            {
                context.AddError("PVP-106-1", "Iteration must be positive number");
            }

            switch (tag)
            {
                case "pre" when m_Version.StartsWithOrdinal("0"):
                    context.AddError("PVP-106-1", "Major version 0 cannot have a pre-release tag");
                    break;
                case "exp" when feature != null && feature.Length > 10:
                    context.AddError("PVP-106-1", "Feature string must not exceed 10 characters");
                    break;
            }
        }

        string GetOptionalLabels()
        {
            // separate the optional semver labels from the major.minor.patch
            var versionSplitRegex = new Regex(@"^\d+\.\d+\.\d+\-(?<labels>.+)");
            var match = versionSplitRegex.Match(m_Version);
            return match.Success ? match.Groups["labels"].Value : "";
        }

        T GetLabelSegment<T>(string pattern, string name)
        {
            var tagRegex = new Regex(pattern);
            var match = tagRegex.Match(m_VersionOptionalLabels);
            object segment = null;

            if (typeof(T) == typeof(int))
            {
                segment = match.Success ? int.Parse(
                    match.Groups[name].Value, NumberStyles.Integer, CultureInfo.InvariantCulture) : (object)null;
            }
            else if (typeof(T) == typeof(string))
            {
                segment = match.Success ? match.Groups[name].Value : null;
            }

            return segment != null ? (T)segment : default(T);
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {

        }

        public void Finish()
        {

        }
    }
}
