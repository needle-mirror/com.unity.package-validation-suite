using System.Globalization;
using System.Text.RegularExpressions;

namespace PvpXray
{
    class LifecycleVerifier : Verifier.IChecker
    {
        public static string[] Checks { get; } = { "PVP-106-1", "PVP-106-2", "PVP-106-3" };
        public static int PassCount => 0;

        static readonly string[] k_Checks1And2 = { "PVP-106-1", "PVP-106-2" };
        static readonly string[] k_Checks2And3 = { "PVP-106-2", "PVP-106-3" };
        static readonly Regex k_VersionSplitRegex = new Regex(@"^\d+\.\d+\.\d+\-(.+)");
        static readonly Regex k_TagPattern = new Regex(@"^(\w+)");
        static readonly Regex k_IterationPattern = new Regex(@"^\w+(?:\-[a-zA-Z\-]+)?\.([0-9]+)$");
        static readonly Regex k_FeaturePattern = new Regex(@"^\w+\-([a-zA-Z\-]+)\.");

        public LifecycleVerifier(Verifier.Context context)
        {
            try
            {
                var versionJson = context.ManifestPermitInvalidJson["version"];

                string version;
                try
                {
                    version = versionJson.String;
                }
                catch (SimpleJsonException e)
                {
                    context.AddError("PVP-106-2", e.LegacyFullMessage);
                    context.AddError("PVP-106-3", e.FullMessage);
                    version = versionJson.IfPresent?.String;
                }

                // if there is no version, no Lifecycle Tests can be run
                if (version == null)
                    return;

                CheckV1And2(context, version);
                if (version == "0.0.0") context.AddError(k_Checks2And3, "Version 0.0.0 is not allowed");
                else CheckV3(context, version);
            }
            catch (SimpleJsonException e)
            {
                context.AddError(k_Checks1And2, e.LegacyFullMessage);
                context.AddError("PVP-106-3", e.FullMessage);
            }
        }

        static void CheckV1And2(Verifier.Context context, string version)
        {
            var versionOptionalLabels = GetOptionalLabels(version);

            // if there are no labels, there are no Package Lifecycle Tests to run
            // This would be a Verified package in PlcV1 or a Release package for PlcV2
            if (versionOptionalLabels == "")
                return;

            var tag = GetStringSegment(versionOptionalLabels, k_TagPattern);
            var iteration = GetIntSegment(versionOptionalLabels, k_IterationPattern);
            var feature = GetStringSegment(versionOptionalLabels, k_FeaturePattern);

            if (tag == "preview")
            {
                if (versionOptionalLabels.EndsWithOrdinal("preview"))
                    return;
                if (iteration == 0 || iteration > 999999)
                    context.AddError(k_Checks1And2, "Package Lifecycle v1 iteration must be number between 1 and 999999 (or absent)");
                return;
            }

            if (tag != "pre" && tag != "exp")
            {
                context.AddError(k_Checks1And2, "Valid pre-release tags are \"exp[-feature].N\" and \"pre.N\"");
                return;
            }

            if (iteration < 1)
            {
                context.AddError(k_Checks1And2, "Iteration must be positive number");
            }

            switch (tag)
            {
                case "pre" when version.StartsWithOrdinal("0"):
                    context.AddError(k_Checks1And2, "Major version 0 cannot have a pre-release tag");
                    break;
                case "exp" when feature != null && feature.Length > 10:
                    context.AddError(k_Checks1And2, "Feature string must not exceed 10 characters");
                    break;
            }
        }

        static void CheckV3(Verifier.Context context, string version)
        {
            var buildMetadataIndex = version.IndexOf('+');
            if (buildMetadataIndex != -1)
            {
                context.AddError("PVP-106-3", "Build metadata may not appear in public package versions");
                version = version.Substring(0, buildMetadataIndex);
            }

            var hyphenIndex = version.IndexOf('-');
            if (hyphenIndex == -1)
            {
                if (version.StartsWithOrdinal("0."))
                {
                    context.AddError("PVP-106-3", "0.* versions must include a pre-release tag, for example: 0.1.0-exp.1");
                }

                // If there are no labels, there are no Package Lifecycle Tests to run.
                // This would be a Verified package in PlcV1 or a Release package for PlcV2.
                return;
            }

            const string preview = "preview";
            if (version.Length == hyphenIndex + 1 + preview.Length && version.EndsWithOrdinal(preview)) return; // "-preview" is valid in PLv1.

            var periodIndex = version.IndexOf('.', hyphenIndex + 1);
            if (periodIndex != -1)
            {
                var tag = version.Slice(hyphenIndex + 1, periodIndex);
                var iteration = version.Slice(periodIndex + 1);

                var isValidIteration = iteration.Length > 0 && iteration[0] != '0';
                for (var i = iteration.Start; i < iteration.End; ++i) isValidIteration &= Net7Compat.IsAsciiDigit(version[i]);

                if (tag.Equals(preview)) // Package Lifecycle v1
                {
                    if (!isValidIteration || iteration.Length > 6)
                        context.AddError("PVP-106-3", "Package Lifecycle v1 iteration must be number between 1 and 999999 (or absent)");
                    return;
                }

                if (!isValidIteration)
                    context.AddError("PVP-106-3", "Pre-release iteration must be a positive integer without leading zeros");

                if (tag.Equals("exp")) return;
                if (tag.Equals("pre"))
                {
                    if (version.StartsWithOrdinal("0.")) context.AddError("PVP-106-3", "Major version 0 cannot have a -pre.N tag; please use -exp.N");
                    return;
                }
                if (tag.Length >= 5 && tag.Slice(0, 4).Equals("exp-"))
                {
                    const int maxFeatureLength = 10;
                    var isValidFeature = tag.Length <= 4 + maxFeatureLength;
                    for (var i = tag.Start + 4; i < tag.End; ++i) isValidFeature &= version[i] == '-' || Net7Compat.IsAsciiLetterOrDigit(version[i]);
                    if (!isValidFeature)
                        context.AddError("PVP-106-3", "Pre-release feature strings must consist of 1 to 10 alphanumeric characters or hyphens");
                    return;
                }
            }
            context.AddError("PVP-106-3", "Valid pre-release tags are \"exp[-feature].N\" and \"pre.N\"");
        }

        static string GetOptionalLabels(string version)
        {
            // separate the optional semver labels from the major.minor.patch
            var match = k_VersionSplitRegex.Match(version);
            return match.Success ? match.Groups[1].Value : "";
        }

        static string GetStringSegment(string labels, Regex pattern)
        {
            var match = pattern.Match(labels);
            return match.Success ? match.Groups[1].Value : null;
        }

        static int GetIntSegment(string labels, Regex pattern)
        {
            var str = GetStringSegment(labels, pattern);
            return str != null && int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var res) ? res : 0;
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex) { }
        public void Finish() { }
    }
}
