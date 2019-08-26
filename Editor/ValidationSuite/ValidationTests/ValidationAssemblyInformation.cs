using System.Linq;
using UnityEditor.Compilation;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests
{
#if UNITY_2018_1_OR_NEWER
    /// <summary>
    /// Used by tests to override assembly information for ApiValidation
    /// </summary>
    internal class ValidationAssemblyInformation
    {
        public bool? isPreviousPackageTestOverride;
        public bool? isProjectPackageTestOverride;

        public string previousAssemblyNameOverride { get; private set; }
        public string projectAssemblyNameOverride { get; private set; }

        public ValidationAssemblyInformation()
        {}

        public ValidationAssemblyInformation(bool? isPreviousPackageTestOverride, bool? isProjectPackageTestOverride, string previousAssemblyNameOverride, string projectAssemblyNameOverride)
        {
            this.isPreviousPackageTestOverride = isPreviousPackageTestOverride;
            this.isProjectPackageTestOverride = isProjectPackageTestOverride;
            this.previousAssemblyNameOverride = previousAssemblyNameOverride;
            this.projectAssemblyNameOverride = projectAssemblyNameOverride;
        }

        public virtual bool IsTestAssembly(AssemblyInfo assembly)
        {
            if (isProjectPackageTestOverride.HasValue)
                return isProjectPackageTestOverride.Value;

            return assembly.assemblyDefinition.references.Contains("TestAssemblies") ||
                assembly.assemblyDefinition.optionalUnityReferences.Contains("TestAssemblies") ||
                assembly.assemblyDefinition.precompiledReferences.Contains("nunit.framework.dll");
        }

        public string GetAssemblyName(Assembly assembly, bool isPrevious)
        {
            return GetOverriddenAssemblyName(isPrevious) ?? assembly.name;
        }

        public string GetAssemblyName(AssemblyDefinition assembly, bool isPrevious)
        {
            return GetOverriddenAssemblyName(isPrevious) ?? assembly.name;
        }

        private string GetOverriddenAssemblyName(bool isPrevious)
        {
            if (isPrevious && previousAssemblyNameOverride != null)
                return previousAssemblyNameOverride;
            if (!isPrevious && projectAssemblyNameOverride != null)
                return projectAssemblyNameOverride;

            return null;
        }
    }
#endif
}
