namespace PvpXray
{
    class PackageStarterKitVerifier : Verifier.IChecker
    {
        public static string[] Checks { get; } = { "PVP-35-1" }; // No unmodified Package Starter Kit files
        public static int PassCount => 1;

        readonly Verifier.Context m_Context;

        public PackageStarterKitVerifier(Verifier.Context context)
        {
            context.IsLegacyCheckerEmittingLegacyJsonErrors = true;
            m_Context = context;
        }

        bool IsUnmodifiedPackageStarterKitFile(Verifier.PackageFile file)
        {
            var entry = file.Entry;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/.README%20-%20External.md
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/757557d470a4ac5fe01eb40b7447bebb2cbc4533/README%20-%20External.md
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/.gitignore
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/.npmignore
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/.yamato/has_moved.md
            // Filename/path caught by PVP-33-1.

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/CHANGELOG.md
            if (entry.Filename == "changelog.md" && file.ReadToStringLax().Contains("Short description of this release")) return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/CONTRIBUTING.md
            if (entry.Filename == "contributing.md" && file.ReadToStringLax().Contains("... Define guidelines & rules")) return true;
            // Filename in root caught by PVP-33-1.

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Documentation%7E/TableOfContents.md
            if (entry.Filename == "tableofcontents.md" && file.ReadToStringLax().Contains("UPM Package Starter Kit")) return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Documentation%7E/images/MultiPage_TOC-structure.png
            if (entry.Filename == "multipage_toc-structure.png") return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Documentation%7E/images/example.png
            if (entry.Filename == "example.png")
            {
                if (file.Size == 52261 && XrayUtils.Sha1(file.Content) == "071942eeb2ef55620e2514d0377d640f7c966a96") return true;
                // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/commit/5ac36739360d4ec396726f40c5ba64f6465efe01
                if (file.Size == 1) return true;
            }

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Documentation%7E/index.md
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/b2b1ffc35e495c38c68aa6286ae5ad355d421349/Documentation%7E/your-package-name.md
            if (entry.Filename == "index.md")
            {
                var text = file.ReadToStringLax();
                if (text.Contains("# Package documentation guides")) return true;
                if (text.Contains("an example of how to set up more complex documentation")) return true;
            }
            if (entry.Filename == "your-package-name.md") return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Documentation%7E/sample-package-guide.md
            if (entry.Filename == "sample-package-guide.md") return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Documentation%7E/test-package-guide.md
            if (entry.Filename == "test-package-guide.md") return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Documentation%7E/tools-package-guide.md
            if (entry.Filename == "tools-package-guide.md") return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Editor/EditorExample.cs
            if (entry.Filename == "editorexample.cs" && file.ReadToStringLax().Contains("MyPublicEditorExampleClass")) return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Editor/Unity.YourPackageName.SubGroup.Editor.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/fdd84bb0395d6b5d103bf9cde6aa4e757557e9f6/Editor/Unity.SubGroup.YourPackageName.Editor.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/6a8cad3a86788b6a647277d8c187a602ce4bb232/Editor/Unity.YourPackageName.Editor.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e0137b6625db46ce055917bd69632e0f50c3c79e/Editor/Unity.Your-package-name.Editor.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/1466f7cfeb157702c870d045170ebe2c08282820/Editor/com.unity.your-package-name.Editor.asmdef
            if (entry.HasExtension(".asmdef") && (entry.Filename.Contains("yourpackagename") || entry.Filename.Contains("your-package-name"))) return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/LICENSE.md
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/25cfc523d885da7459ced782d7e2e0d6725f422a/LICENSE_UCL.md
            if (entry.Filename == "license.md" && file.ReadToStringLax().Contains("6SKY9k5xnukFAXCYHx7u89MQ")) return true;
            if (entry.Path == "license_ucl.md") return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/QAReport.md
            if (entry.Filename == "qareport.md" && file.ReadToStringLax().Contains("Use this section to describe how this feature was tested")) return true;
            // Filename also caught by PVP-33-1.

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/README.md
            if (entry.Filename == "readme.md")
            {
                var text = file.ReadToStringLax();
                if (text.Contains("UPM Package Starter Kit")) return true;
                if (text.Contains("internaldocs.unity.com")) return true;
                // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/dbf7c4471fc9112e31d2d4558a20e3cbf2865849/README.md
                if (text.Contains("Fill in your package information")) return true;
            }

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Runtime/RuntimeExample.cs
            if (entry.Filename == "runtimeexample.cs" && file.ReadToStringLax().Contains("MyPublicRuntimeExampleClass")) return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Runtime/Unity.YourPackageName.SubGroup.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e7a6213c2e2cfb41bef198393390607c4252663a/Runtime/Unity.SubGroup.YourPackageName.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/cf90143f22eb3b047ea367ba7749c6f07b4b99a7/Runtime/Unity.YourPackageName.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/49e297299418560a83057d5bb8dabdff695ce8bf/Runtime/Unity.YourPackageName.Runtime.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e0137b6625db46ce055917bd69632e0f50c3c79e/Runtime/Unity.Your-package-name.Runtime.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/1466f7cfeb157702c870d045170ebe2c08282820/Runtime/com.unity.your-package-name.Runtime.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/afccaba3281208fd718d94e5b8a5b74a816b96ce/Runtime/YourPackageName.Runtime.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/61c9cfe0ee135388179b27ac5f41cd2b3fa4625a/Runtime/Runtime-your-package-name.asmdef
            // Caught above.

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Samples%7E/Example/.sample.json
            // Filename caught by PVP-33-1 and contents caught by PVP-82-1 (if copied into package manifest by UPM-CI).

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Samples%7E/Example/SampleExample.cs
            if (entry.Filename == "sampleexample.cs" && file.ReadToStringLax().Contains("sample example C# file to develop samples")) return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Tests/.tests.json
            // Filename caught by PVP-33-1.

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Tests/Editor/EditorExampleTest.cs
            if (entry.Filename == "editorexampletest.cs" && file.ReadToStringLax().Contains("EditorSampleTestSimplePasses")) return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Tests/Editor/Unity.YourPackageName.SubGroup.Editor.Tests.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/1c5eeeb935083251ae9753664198a00c5ad290e9/Tests/Editor/Unity.SubGroup.YourPackageName.EditorTests.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/cf90143f22eb3b047ea367ba7749c6f07b4b99a7/Tests/Editor/Unity.YourPackageName.EditorTests.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e0137b6625db46ce055917bd69632e0f50c3c79e/Tests/Editor/Untiy.Your-package-name.EditorTests.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/1466f7cfeb157702c870d045170ebe2c08282820/Tests/Editor/com.unity.your-package-name.EditorTests.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/2e7b45e051450f9e583bbad69258c77aad739d54/Tests/Editor/YourPackageName.EditorTests.asmdef
            // Caught above.

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Tests/Runtime/RuntimeExampleTest.cs
            if (entry.Filename == "runtimeexampletest.cs" && file.ReadToStringLax().Contains("PlayModeSampleTestSimplePasses")) return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Tests/Runtime/Unity.YourPackageName.SubGroup.Tests.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/1c5eeeb935083251ae9753664198a00c5ad290e9/Tests/Runtime/Unity.SubGroup.YourPackageName.Tests.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/cf90143f22eb3b047ea367ba7749c6f07b4b99a7/Tests/Runtime/Unity.YourPackageName.Tests.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/49e297299418560a83057d5bb8dabdff695ce8bf/Tests/Runtime/Unity.YourPackageName.RuntimeTests.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e0137b6625db46ce055917bd69632e0f50c3c79e/Tests/Runtime/Unity.Your-package-name.RuntimeTests.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/60c63f190b28c6df6ad23ed3d8e93c17eb671069/Tests/Runtime/com.unity.your-package-name.RuntimeTests.asmdef
            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/2e7b45e051450f9e583bbad69258c77aad739d54/Tests/Runtime/YourPackageName.RuntimeTests.asmdef
            // Caught above.

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/Third%20Party%20Notices.md
            if (entry.Filename == "third party notices.md" && file.ReadToStringLax().Contains("[provide component name]")) return true;

            // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/e72985bcd7d88ffd61e50e3b31af5426e83833c0/package.json
            if (entry.Path == "package.json")
            {
                var description = m_Context.ManifestPermitInvalidJson["description"];
                if (description.IsPresent)
                {
                    var text = description.String;
                    if (text.StartsWithOrdinal("Replace this string with your own description")) return true;
                    // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/5d92a5c2c62c95cba80036f00ddf950000430f77/package.json
                    if (text.StartsWithOrdinal("Your description")) return true;
                    // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/bc93ded2754d38004d1bbd755d6ca3743d8bf3ec/package.json
                    if (text == "Unity Package Template") return true;
                }

                // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/79e620af0299b774996184fbfab942f385e69c59/package.json
                // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/c6b39c2a8668fc16e544796bf498bf95a96ada6e/package.json
                var keywords = m_Context.ManifestPermitInvalidJson["keywords"];
                var keyKeywordSeen = false;
                foreach (var keyword in keywords.ElementsIfPresent)
                {
                    var text = keyword.String;
                    if (keyKeywordSeen)
                    {
                        if (text == "words") return true;
                    }
                    else if (text == "key") keyKeywordSeen = true;
                }

                // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/8906fe818474921ceb4851bb478ff25cb79fb335/package.json
                // https://github.cds.internal.unity3d.com/unity/com.unity.package-starter-kit/blob/c6b39c2a8668fc16e544796bf498bf95a96ada6e/package.json
                var category = m_Context.ManifestPermitInvalidJson["category"];
                if (category.IsPresent)
                {
                    var text = category.String;
                    if (text.StartsWithOrdinal("Your category")) return true;
                }
            }

            return false;
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            if (IsUnmodifiedPackageStarterKitFile(file))
            {
                m_Context.AddError("PVP-35-1", file.Path);
            }
        }

        public void Finish()
        {
        }
    }
}
