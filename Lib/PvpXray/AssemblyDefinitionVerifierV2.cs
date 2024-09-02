#if false // disabled pending PVS-208
using System.Text.RegularExpressions;

namespace PvpXray
{
    class AssemblyDefinitionVerifierV2 : Verifier.IChecker
    {
        const string k_Extension = ".asmdef";
        static readonly Regex k_AssemblyNamePattern = new Regex(@"^[0-9A-Z][0-9A-Za-z]*(\.[0-9A-Z][0-9A-Za-z]*)*$");

        public static string[] Checks { get; } = {
            "PVP-130-2", // .asmdef file name should match the assembly name (US-0038)
            "PVP-131-2", // Assembly names must start with 'Unity' for ApiUpdater coverage
            "PVP-132-2", // Assembly names must follow naming convention (US-0038)
            // "PVP-133-2", // .asmdef files must be located in appropriate folder
        };

        public static int PassCount => 1;

        readonly Verifier.Context m_Context;
        readonly bool m_IsUnityPackage;

        public AssemblyDefinitionVerifierV2(Verifier.Context context)
        {
            m_Context = context;

            try
            {
                m_IsUnityPackage = m_Context.Manifest["name"].String.StartsWithOrdinal("com.unity.");
            }
            catch (SimpleJsonException e)
            {
                m_Context.AddError("PVP-131-2", e.FullMessage);
            }
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            var entry = file.Entry;
            if (!entry.HasExtension(k_Extension) || !entry.IsAsset(includeSamples: true)) return;

            var json = file.ReadAsJsonLax();
            var assemblyName = json["name"].String;

            if (!entry.FilenameWithCaseNoExtension.Equals(assemblyName))
            {
                m_Context.AddError("PVP-130-2", $"{file.Path}: assembly name does not match filename: {assemblyName}");
            }

            if (m_IsUnityPackage && !entry.IsSampleAsset && !assemblyName.StartsWithOrdinal("Unity"))
            {
                m_Context.AddError("PVP-131-2", $"{file.Path}: assembly name does not start with \"Unity\": {assemblyName}");
            }

            if (!k_AssemblyNamePattern.IsMatch(assemblyName))
            {
                m_Context.AddError("PVP-132-2", $"{file.Path}: assembly name does not follow naming convention: {assemblyName}");
            }
        }

        public void Finish() { }
    }
}
#endif
