namespace PvpXray
{
    class LargeFileVerifier : Verifier.IChecker
    {
        public static string[] Checks => new[] { "PVP-93-1" };
        public static int PassCount => 1;

        readonly Verifier.IContext m_Context;

        public LargeFileVerifier(Verifier.IContext context)
        {
            m_Context = context;
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            if (file.Size > 1_000_000_000)
            {
                m_Context.AddError("PVP-93-1", file.Path);
            }
        }

        public void Finish()
        {
        }
    }
}
