using System;
using System.Collections.Generic;

using UnityEngine;
using Semver;
using UnityEditor.PackageManager.ValidationSuite.Utils;

namespace UnityEditor.PackageManager.ValidationSuite
{
    public class ManifestData
    {
        public string path = "";
        public string name = "";
        public string documentationUrl = "";
        public string displayName = "";
        public string description = "";
        public string unity = "";
        public string unityRelease = "";
        public string version = "";
        public double lifecycle = 1.0;
        public string type = "";
        public List<SampleData> samples = new List<SampleData>();
        public Dictionary<string, string> repository = new Dictionary<string, string>();
        public Dictionary<string, string> dependencies = new Dictionary<string, string>();
        public Dictionary<string, string> relatedPackages = new Dictionary<string, string>();

        internal LifecyclePhase LifecyclePhase {
            get { return PackageLifecyclePhase.GetLifecyclePhase(version.ToLower()); }
        }

        public bool IsProjectTemplate
        {
            get { return type.Equals("template", StringComparison.InvariantCultureIgnoreCase); }
        }

        public string Id
        {
            get { return GetPackageId(name, version); }
        }

        public static string GetPackageId(string name, string version)
        {
            return name + "@" + version;
        }

        public static double EvaluateLifecycle(string unityVersion)
        {
            return (SemVersion.Parse(unityVersion) < new SemVersion(2021, 1, 0)) ? 1.0 : 2.0;
        }
    }

    [Serializable]
    public class SampleData
    {
        public string displayName = "";
        public string description = "";
        public string path = "";
    }

}
