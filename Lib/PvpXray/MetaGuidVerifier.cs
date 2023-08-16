using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PvpXray
{
    class MetaGuidVerifier : Verifier.IChecker
    {
        const string k_MetaExtension = ".meta";

        static readonly Regex k_GuidLine = new Regex(@"^guid: ([0-9a-f]{32})$", RegexOptions.Multiline);

        public static string[] Checks => new[] { "PVP-27-1" };
        public static int PassCount => 1;

        readonly Verifier.IContext m_Context;
        readonly Dictionary<string, string> m_PathByGuid;

        public MetaGuidVerifier(Verifier.IContext context)
        {
            m_Context = context;
            m_PathByGuid = new Dictionary<string, string>();
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            var metaPath = file.Path;

            if (!metaPath.EndsWithOrdinal(k_MetaExtension)) return;
            var assetPath = metaPath.Substring(0, metaPath.Length - k_MetaExtension.Length);

            var text = file.ReadToStringLegacy();
            var match = k_GuidLine.Match(text);
            if (!match.Success) return;
            var guid = match.Groups[1].Value;

            if (m_PathByGuid.TryGetValue(guid, out var existing))
            {
                m_Context.AddError("PVP-27-1", $"{existing}: {guid}");
                m_Context.AddError("PVP-27-1", $"{assetPath}: {guid}");
            }
            else
            {
                m_PathByGuid[guid] = assetPath;
            }
        }

        public void Finish()
        {
        }
    }
}
