using System;
using System.IO;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests.Standards
{
    internal class PackageContainsMetafileUS0112 : BaseStandardChecker
    {
        public override string StandardCode => "US-0112";
        public override StandardVersion Version => new StandardVersion(1, 0, 0);

        public void Check(string folder)
        {
            CheckMetaInFolderRecursively(folder);
        }

        bool ShouldIgnore(string name)
        {
            //Names starting with a "." are ignored by AssetDB.
            //Names finishing with ".meta" are considered meta files in Editor Code.
            if (Path.GetFileName(name).StartsWith(".") || name.EndsWith(".meta"))
                return true;

            // Honor the Unity tilde skipping of import
            if (Path.GetDirectoryName(name).EndsWith("~") || name.EndsWith("~"))
                return true;

            // Ignore node_modules folder as it is created inside the tested directory when production dependencies exist
            if (Path.GetDirectoryName(name).EndsWith("node_modules") || name.Contains("node_modules"))
                return true;

            return false;
        }

        // Files in Loadable Plugin Directories are ignored by AssetDB
        // if a plugin has been configured for the directory.
        static readonly string[] k_LoadableDirectoryExtensionTypes = { ".androidlib", ".bundle", ".plugin", ".framework" };

        bool ShouldIgnoreChildren(string name)
        {
            // Newer Unity versions ignore files inside plugin directories.
            // This has been backported to 2020.3 LTS, 2021.3 LTS, 2022.2 LTS, 2023.1 LTS.
#if UNITY_2020_3 || UNITY_2021_3 || UNITY_2022_2_OR_NEWER
            var fileName = Path.GetFileName(name);
            foreach (var value in k_LoadableDirectoryExtensionTypes)
            {
                if (fileName.EndsWith(value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
#endif
            return false;
        }

        void CheckMeta(string toCheck)
        {
            if (System.IO.File.Exists(toCheck + ".meta"))
                return;

            AddError("Did not find meta file for " + toCheck);
        }

        void CheckMetaInFolderRecursively(string folder)
        {
            try
            {
                foreach (string file in Directory.GetFiles(folder))
                {
                    if (!ShouldIgnore(file))
                        CheckMeta(file);
                }

                foreach (string dir in Directory.GetDirectories(folder))
                {
                    if (ShouldIgnore(dir))
                        continue;

                    CheckMeta(dir);

                    if (!ShouldIgnoreChildren(dir))
                        CheckMetaInFolderRecursively(dir);
                }
            }
            catch (Exception e)
            {
                AddError("Exception " + e.Message);
            }
        }
    }
}
