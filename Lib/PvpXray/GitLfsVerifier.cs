namespace PvpXray
{
    class GitLfsVerifier : Verifier.IChecker
    {
        // https://github.com/git-lfs/git-lfs/blob/v3.4.0/docs/spec.md#the-pointer
        const int k_PointerMinSize = 126;
        const int k_PointerMaxSize = 1024; // https://github.com/git-lfs/git-lfs/blob/v3.4.0/lfs/scanner.go#L10-L12
        static readonly byte[] k_PointerPrefix = XrayUtils.Utf8Strict.GetBytes("version https://git-lfs.github.com/spec/");

        public static string[] Checks { get; } = { "PVP-36-1" }; // No Git LFS pointer files
        public static int PassCount => 1;

        readonly Verifier.Context m_Context;

        public GitLfsVerifier(Verifier.Context context)
        {
            context.IsLegacyCheckerEmittingLegacyJsonErrors = true;
            m_Context = context;
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            if (file.Size < k_PointerMinSize || file.Size > k_PointerMaxSize) return;

            var content = file.Content;
            for (var i = 0; i < k_PointerPrefix.Length; i++)
            {
                if (content[i] != k_PointerPrefix[i]) return;
            }

            m_Context.AddError("PVP-36-1", file.Path);
        }

        public void Finish()
        {
        }
    }
}
