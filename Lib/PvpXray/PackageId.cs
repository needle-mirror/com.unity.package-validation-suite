using System;
using System.Text.RegularExpressions;

namespace PvpXray
{
    /// A package "Name@Version" with minimal validation of the Name and Version
    /// (they must simply be non-empty, and '@' is banned to prevent ambiguity).
    /// In particular, Version does not need to be valid Semver.
    public struct PackageId : IEquatable<PackageId>
    {
        public const string UnityModuleNamePrefix = "com.unity.modules.";

        /// Regex matching valid package names, based on the (ill-defined) NPM rules.
        /// Per NPM rules, such package names can be used in URLs without escaping.
        /// (The PackageId struct does not require conformance with this.)
        public static readonly Regex ValidName = new Regex("^[a-z0-9][-._a-z0-9]{0,213}$");

        /// Regex matching valid SemVer (2.0.0) version string.
        /// (The PackageId struct does not require conformance with this.)
        public static readonly Regex ValidSemVer = new Regex(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$");

        public string Id { get; }
        public string Name { get; }
        public string Version { get; }

        public PackageId(string id)
        {
            var i = id.IndexOf('@');
            if (i <= 0 || i == id.Length - 1 || id.IndexOf('@', i + 1) != -1)
                throw new ArgumentException("Invalid package ID: " + id, nameof(id));
            Id = id;
            Name = id.Substring(0, i);
            Version = id.Substring(i + 1);
        }

        public PackageId(string name, string version)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            if (Name.Length == 0 || Name.Contains('@'))
                throw new ArgumentException("Invalid package name: " + name, nameof(name));
            if (Version.Length == 0 || Version.Contains('@'))
                throw new ArgumentException("Invalid package version: " + version, nameof(version));
            Id = name + "@" + version;
        }

        public PackageId(Json manifest)
        {
            var nameJson = manifest["name"];
            var versionJson = manifest["version"];
            Name = nameJson.String;
            Version = versionJson.String;
            if (Name.Length == 0 || Name.Contains('@'))
                throw nameJson.GetException("invalid package name");
            if (Version.Length == 0 || Version.Contains('@'))
                throw versionJson.GetException("invalid package version");
            Id = Name + "@" + Version;
        }

        public override string ToString() => Id;
        public bool Equals(PackageId other) => Id == other.Id;
        public override bool Equals(object obj) => obj is PackageId other && Id == other.Id;
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(PackageId left, PackageId right) => left.Equals(right);
        public static bool operator !=(PackageId left, PackageId right) => !left.Equals(right);
    }
}
