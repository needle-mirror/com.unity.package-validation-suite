using System.Collections.Generic;
using System.IO;
using PvpXray;

namespace UnityEditor.PackageManager.ValidationSuite.ValidationTests.Standards
{
    internal class SamplesUS0116 : BaseStandardChecker
    {
        public override string StandardCode => "US-0116";

        public override StandardVersion Version => new StandardVersion(1, 0, 0);

        public void Check(string path, List<SampleData> samples, ValidationType validationType)
        {
            var samplesDirInfo = SamplesUtilities.GetSampleDirectoriesInfo(path);
            if (samplesDirInfo.SamplesDirExists && samplesDirInfo.SamplesTildeDirExists)
            {
                AddError("`Samples` and `Samples~` cannot both be present in the package.");
            }

            if ((validationType == ValidationType.Promotion || validationType == ValidationType.VerifiedSet) && samplesDirInfo.SamplesDirExists)
            {
                AddError("In a published package, the `Samples` needs to be renamed to `Samples~`. It should have been done automatically in the CI publishing process.");
            }

            if (validationType == ValidationType.Promotion || validationType == ValidationType.VerifiedSet)
            {
                foreach (var sample in samples)
                {
                    if (string.IsNullOrEmpty(sample.path))
                        AddError("Sample path must be set and non-empty in `package.json`.");
                    if (string.IsNullOrEmpty(sample.displayName))
                        AddError("Sample display name will be shown in the UI, and it must be set and non-empty in `package.json`.");
                    if ((sample.path + "/").Contains("/../"))
                    {
                        AddError("Sample path set in `package.json` cannot contain `..`");
                    }
                    if (sample.path != "Samples~" && !sample.path.StartsWithOrdinal("Samples~/"))
                    {
                        AddError("Sample path set in `package.json` must be rooted in the Samples~ directory.");
                    }
                    if (!Directory.Exists(Path.Combine(path, sample.path)))
                    {
                        AddError("Sample path set in `package.json` does not exist: " + sample.path + ".");
                    }
                }
            }
        }
    }
}
