using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PvpXray
{
    static class MetaGuidValidations
    {
        const string k_MetaExtension = ".meta";

        static readonly Regex k_GuidLine = new Regex(@"^guid: ([0-9a-f]{32})$", RegexOptions.Multiline);

        public static readonly string[] Checks = { "PVP-27-1" };

        public static void Run(Validator.Context context)
        {
            var pathByGuid = new Dictionary<string, string>();
            foreach (var metaPath in context.Files)
            {
                if (!metaPath.EndsWithOrdinal(k_MetaExtension)) continue;
                var assetPath = metaPath.Substring(0, metaPath.Length - k_MetaExtension.Length);

                var text = context.ReadFileToString(metaPath);
                var match = k_GuidLine.Match(text);
                if (!match.Success) continue;
                var guid = match.Groups[1].Value;

                if (pathByGuid.TryGetValue(guid, out var existing))
                {
                    context.AddError("PVP-27-1", $"{existing}: {guid}");
                    context.AddError("PVP-27-1", $"{assetPath}: {guid}");
                }
                else
                {
                    pathByGuid[guid] = assetPath;
                }
            }
        }
    }
}
