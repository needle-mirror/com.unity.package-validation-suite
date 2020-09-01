using System;
using Semver;
using UnityEditor.PackageManager.ValidationSuite;

namespace UnityEditor.PackageManager.ValidationSuite.Utils {
    [Flags]
    internal enum LifecyclePhase {
        InvalidVersionTag = 0,
        Preview = 2,
        Experimental = 4,
        PreRelease = 8,
        ReleaseCandidate = 16,
        Release = 32
    }

    internal static class PackageLifecyclePhase {
        internal static bool IsRCVersion(SemVersion version, VersionTag tag)
        {
            return tag.Tag == "rc" && version.Major > 0;
        }

        internal static bool IsPreReleaseVersion(SemVersion version, VersionTag tag)
        {
            return tag.Tag == "pre" && version.Major > 0;
        }

        internal static bool IsExperimentalVersion(SemVersion version, VersionTag tag)
        {
            return (tag.Tag == "exp" && version.Major > 0) || version.Major == 0;
        }

        internal static bool IsReleasedVersion(SemVersion version, VersionTag tag)
        {
            return string.IsNullOrEmpty(tag.Tag) && version.Major >= 1;
        }

        internal static bool IsPreviewVersion(SemVersion version, VersionTag tag)
        {
            #if UNITY_2021_1_OR_NEWER
            return version.Prerelease.Contains("preview");
            #else
            return version.Prerelease.Contains("preview") || version.Major == 0;
            #endif
        }

        internal static LifecyclePhase GetLifecyclePhase(string version) {
            SemVersion semVer = SemVersion.Parse(version);
            try {
                VersionTag pre = VersionTag.Parse(semVer.Prerelease);

                if (PackageLifecyclePhase.IsPreviewVersion(semVer, pre)) return LifecyclePhase.Preview;
                if (PackageLifecyclePhase.IsReleasedVersion(semVer, pre)) return LifecyclePhase.Release;
                if (PackageLifecyclePhase.IsRCVersion(semVer, pre)) return LifecyclePhase.ReleaseCandidate;
                if (PackageLifecyclePhase.IsPreReleaseVersion(semVer, pre)) return LifecyclePhase.PreRelease;
                if (PackageLifecyclePhase.IsExperimentalVersion(semVer, pre)) return LifecyclePhase.Experimental;
                return LifecyclePhase.InvalidVersionTag;
            } catch (System.ArgumentException e) {
                if (e.Message.Contains("invalid version tag"))
                    return LifecyclePhase.InvalidVersionTag;
                throw e;
            }
        }

        internal static LifecyclePhase GetPhaseSupportedVersions(LifecyclePhase phase) {
            var supportedVersions = phase;
            // Set extra supported versions for other tracks
            switch (phase)
            {
                case LifecyclePhase.Release:
                    supportedVersions = LifecyclePhase.Release;
                    break;
                case LifecyclePhase.ReleaseCandidate:
                    supportedVersions = LifecyclePhase.Release | LifecyclePhase.ReleaseCandidate;
                    break;
                case LifecyclePhase.PreRelease:
                    supportedVersions = LifecyclePhase.PreRelease | LifecyclePhase.ReleaseCandidate | LifecyclePhase.Release;
                    break;
                case LifecyclePhase.Experimental:
                    supportedVersions = LifecyclePhase.Preview | LifecyclePhase.Experimental | LifecyclePhase.PreRelease | LifecyclePhase.ReleaseCandidate | LifecyclePhase.Release;
                    break;
                case LifecyclePhase.Preview:
                    supportedVersions = LifecyclePhase.Preview | LifecyclePhase.Experimental | LifecyclePhase.PreRelease | LifecyclePhase.ReleaseCandidate | LifecyclePhase.Release;
                    break;
            }

            return supportedVersions;
        }
    }
}