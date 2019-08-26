using System;

namespace UnityEditor.PackageManager.ValidationSuite {
#if !UNITY_2018_2_OR_NEWER
    static class ValidationSuiteMenus
    {
#if UNITY_2018_2_OR_NEWER
        [MenuItem("internal:Packages/Test Packman Validation")]
        internal static void TestPackmanValidation()
        {
            ValidationSuite.RunValidationSuite(string.Format("{0}@{1}", "com.unity.package-manager-ui", "1.8.1"));
        }

#endif

        [MenuItem("internal:Packages/Test AssetStore Validation")]
        internal static void TestAssetStoreValidation()
        {
            ValidationSuite.RunAssetStoreValidationSuite("Graph - Charts", "5.3", "data/pkg1", "data/pkg2");
        }

        [MenuItem("internal:Packages/Test AssetStore Validation no Previous")]
        internal static void TestAssetStoreValidationNoPrevious()
        {
            ValidationSuite.RunAssetStoreValidationSuite("Graph - Charts", "5.3", "data/pkg1");
        }
    }
#endif
}
