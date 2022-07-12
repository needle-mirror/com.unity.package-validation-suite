using System;
using JetBrains.Annotations;

namespace UnityEditor.PackageManager.ValidationSuite
{
    class PackageId
    {
        public string Id { get; }
        public string Name { get; }
        public string Version { get; }

        public PackageId([NotNull] string id)
        {
            var split = id.Split('@');
            if (split.Length != 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]))
                throw new ArgumentException(nameof(id));
            Id = id;
            Name = split[0];
            Version = split[1];
        }

        public PackageId([NotNull] string name, [NotNull] string version)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(nameof(name));
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException(nameof(version));
            Id = name + "@" + version;
            Name = name;
            Version = version;
        }

        // Note: This is distinct from `new PackageId(info.packageId)`,
        // as `packageId` may be a string like "com.unity.example@file:/foo/bar".
        public PackageId([NotNull] PackageInfo info) : this(info.name, info.version) { }

        public override string ToString() => Id;

        public override bool Equals(object obj) => obj is PackageId pid && pid.Id == Id;
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(PackageId left, PackageId right) => Equals(left, right);
        public static bool operator !=(PackageId left, PackageId right) => !Equals(left, right);
    }
}
