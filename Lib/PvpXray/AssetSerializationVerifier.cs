namespace PvpXray
{
    class AssetSerializationVerifier : Verifier.IChecker
    {
        static readonly byte[] k_UnityYamlPrefix = XrayUtils.Utf8Strict.GetBytes("%YAML ");

        public static string[] Checks => new[] { "PVP-37-1" }; // No Unity assets using binary serialization
        public static int PassCount => 1;

        readonly Verifier.IContext m_Context;

        public AssetSerializationVerifier(Verifier.IContext context)
        {
            m_Context = context;
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            // Ignore files without known Unity YAML filename extensions.
            var isEligible = file.Extension.HasFlags(FileExt.V1 | FileExt.V2, FileExt.UnityYaml);
            if (!isEligible) return;

            // Ignore hidden assets.
            if (file.Entry.IsHidden) return;

            if (file.Size >= k_UnityYamlPrefix.Length)
            {
                var content = file.Content;

                int i;
                for (i = 0; i < k_UnityYamlPrefix.Length; i++)
                {
                    if (content[i] != k_UnityYamlPrefix[i]) break;
                }
                if (i == k_UnityYamlPrefix.Length) return;
            }

            m_Context.AddError("PVP-37-1", file.Path);
        }

        public void Finish()
        {
        }
    }
}
