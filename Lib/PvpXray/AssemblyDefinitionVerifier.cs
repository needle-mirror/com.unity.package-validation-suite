using System.Linq;
using System.Text.RegularExpressions;

namespace PvpXray
{
    class AssemblyDefinitionVerifier : Verifier.IChecker
    {
        const string k_Extension = ".asmdef";
        static readonly Regex k_AssemblyNamePattern = new Regex(@"^[A-Z][0-9A-Za-z]*(\.[A-Z][0-9A-Za-z]*)*$");

        public static string[] Checks => new[]
        {
            "PVP-130-1", // .asmdef file name should match the assembly name (US-0038)
            "PVP-131-1", // Assembly names must start with 'Unity.' for ApiUpdater coverage
            "PVP-132-1", // Assembly names must follow naming convention (US-0038)
            "PVP-133-1", // .asmdef files must be located in appropriate folder
        };

        public static int PassCount => 1;

        readonly Verifier.IContext m_Context;

        public AssemblyDefinitionVerifier(Verifier.IContext context)
        {
            m_Context = context;
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            if (!file.Path.EndsWithOrdinal(k_Extension)) return;

            var json = file.ReadAsJson();
            var assemblyName = json["name"].String;

            var entry = new PathVerifier.Entry(file.Path);
            if (entry.FilenameWithCase.Substring(0, entry.FilenameWithCase.Length - k_Extension.Length) != assemblyName)
            {
                m_Context.AddError("PVP-130-1", $"{file.Path}: assembly name does not match filename: {assemblyName}");
            }

            if (m_Context.Manifest["name"].String.StartsWithOrdinal("com.unity."))
            {
                if (!assemblyName.StartsWithOrdinal("Unity."))
                {
                    m_Context.AddError("PVP-131-1", $"{file.Path}: assembly name does not start with \"Unity.\": {assemblyName}");
                }
            }

            if (!k_AssemblyNamePattern.IsMatch(assemblyName))
            {
                m_Context.AddError("PVP-132-1", $"{file.Path}: assembly name does not follow naming convention: {assemblyName}");
            }

            var isEditorAssembly = json["includePlatforms"].ElementsIfPresent.Any(element => element.String == "Editor");
            if (isEditorAssembly)
            {
                if (!file.Path.StartsWithOrdinal("Editor/") && !file.Path.StartsWithOrdinal("Tests/Editor/"))
                {
                    m_Context.AddError("PVP-133-1", $"{file.Path}: editor assembly definition file not inside \"Editor\" or \"Tests/Editor\" directory");
                }
            }
            else
            {
                if (!file.Path.StartsWithOrdinal("Runtime/") && !file.Path.StartsWithOrdinal("Tests/Runtime/"))
                {
                    m_Context.AddError("PVP-133-1", $"{file.Path}: runtime assembly definition file not inside \"Runtime\" or \"Tests/Runtime\" directory");
                }
            }
        }

        public void Finish()
        {
        }
    }
}
