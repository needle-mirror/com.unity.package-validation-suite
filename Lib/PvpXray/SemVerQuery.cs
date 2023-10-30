using System;
using System.Globalization;
using System.Text;

namespace PvpXray
{
    /// A SemVer version triple, with no pre-release tag or build information.
    public struct VersionTriple
    {
        public static readonly VersionTriple MaxValue = new VersionTriple(uint.MaxValue, uint.MaxValue, uint.MaxValue);

        public uint Major;
        public uint Minor;
        public uint Patch;

        public VersionTriple NextPatch => new VersionTriple(Major, Minor, Patch + 1);
        public VersionTriple NextMinor => new VersionTriple(Major, Minor + 1, 0);

        public VersionTriple(uint major, uint minor, uint patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public int CompareTo(VersionTriple other)
        {
            if (Major < other.Major) return -1;
            if (Major > other.Major) return +1;
            if (Minor < other.Minor) return -1;
            if (Minor > other.Minor) return +1;
            if (Patch < other.Patch) return -1;
            if (Patch > other.Patch) return +1;
            return 0;
        }

        public static bool TryParse(string version, out VersionTriple triple)
            => TryParse(version, version.Length, out triple);

        static readonly char[] k_Separators = { '-', '+' };

        /// Parses the MAJOR.MINOR.PATCH part of a SemVer string, ignoring any
        /// pre-release tag or build information. Note that only the version
        /// triple is validated to conform with SemVer, not the ignored part.
        public static bool TryParseIgnoringPrereleaseAndBuildInfo(string version, out VersionTriple triple)
        {
            var i = version.IndexOfAny(k_Separators);
            return TryParse(version, i == -1 ? version.Length : i, out triple);
        }

        static bool TryParse(string version, int len, out VersionTriple triple)
        {
            triple = default;

            var i = version.IndexOf('.');
            var j = version.IndexOf('.', i + 1);
            if (j == -1 || j >= len - 1) return false;
            if ((i != 1 && version[0] == '0') ||
                (j != i + 2 && version[i + 1] == '0') ||
                (len != j + 2 && version[j + 1] == '0')) return false;

            var majorText = version.SpanOrSubstring(0, i);
            var minorText = version.SpanOrSubstring(i + 1, j - i - 1);
            var patchText = version.SpanOrSubstring(j + 1, len - j - 1);
            return uint.TryParse(majorText, NumberStyles.None, CultureInfo.InvariantCulture, out triple.Major) &&
                uint.TryParse(minorText, NumberStyles.None, CultureInfo.InvariantCulture, out triple.Minor) &&
                uint.TryParse(patchText, NumberStyles.None, CultureInfo.InvariantCulture, out triple.Patch);
        }

        public void AppendTo(StringBuilder sb)
        {
            sb.Append(Major);
            sb.Append('.');
            sb.Append(Minor);
            sb.Append('.');
            sb.Append(Patch);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            AppendTo(sb);
            return sb.ToString();
        }
    }

    public struct SemVerQuery
    {
        public enum Op
        {
            LessThan = 0,
            LessThanOrEqual = 1,
            GreaterThan = 2,
            GreaterThanOrEqual = 3,
        }

        public static readonly string[] OperatorNames = { " < ", " <= ", " > ", " >= " };

        public readonly Op Operator;
        public readonly VersionTriple RefVersion;

        public string BestVersion;
        public VersionTriple BestTriple;

        public SemVerQuery(Op op, VersionTriple refVersion)
        {
            Operator = op;
            RefVersion = refVersion;
            BestVersion = null;
            BestTriple = op == Op.LessThan || op == Op.LessThanOrEqual ? default : VersionTriple.MaxValue;
        }

        public void Consider(string candidateVersion)
        {
            // SemVer queries are used for compatibility checks, and thus ignore
            // pre-release versions (including all 0.* versions), versions with
            // build information, and versions that are not valid SemVer. For
            // practical reasons we also ignore valid SemVer with ridiculously
            // high component values, like "1.0.4294967296".
            if (!VersionTriple.TryParse(candidateVersion, out var candidateTriple) || candidateTriple.Major == 0) return;

            var direction = Direction(Operator);

            var cmp = candidateTriple.CompareTo(RefVersion);
            if (cmp == direction)
            {
                cmp = candidateTriple.CompareTo(BestTriple);
                if (cmp != direction) // at least as good as current best
                {
                    // We need to check "at least as good" and not just "better" for the
                    // edge case considering VersionTriple.MaxValue with Op ">" or ">=",
                    // since in that case, BestTriple is already MaxValue, but we must
                    // still assign BestVersion to signify that we found a match.
                    BestVersion = candidateVersion;
                    BestTriple = candidateTriple;
                }
            }
            else if (cmp == 0 && (Operator == Op.LessThanOrEqual || Operator == Op.GreaterThanOrEqual))
            {
                BestVersion = candidateVersion;
                BestTriple = candidateTriple;
            }
        }

        static int Direction(Op op) => op == Op.LessThan || op == Op.LessThanOrEqual ? -1 : 1;
    }
}
