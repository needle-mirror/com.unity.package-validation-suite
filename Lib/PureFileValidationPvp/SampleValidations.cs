using System;
using System.Collections.Generic;
using System.Linq;

namespace PureFileValidationPvp
{
    static class SampleValidations
    {
        const string k_Manifest = "package.json";
        public static readonly string[] Checks = { "PVP-80-1", "PVP-81-1" };

        class SampleEntry
        {
            public Json Json;
            public string JsonFilePath;

            public string Definition => JsonFilePath ?? $"{k_Manifest}: {Json.Path}";
        }

        public static void Run(Validator.Context context)
        {
            var manifest = context.Manifest;

            var hasSamplesDir = false;
            var hasSamplesTildeDir = false;
            var samplesByDir = new Dictionary<string, SampleEntry>();

            foreach (var path in context.Files)
            {
                var entry = new PathValidations.Entry(path);
                var isInsideSampleDir = false;

                void CheckSamples(string expected, string expectedLower, ref bool hasIt)
                {
                    if (entry.Components[0] != expectedLower) return;
                    if (!entry.PathWithCase.StartsWithOrdinal(expected))
                    {
                        var actualCasing = entry.PathWithCase.Split('/')[0];
                        context.AddError("PVP-80-1", $"{actualCasing}: path has incorrect casing, should be {expected}");
                    }

                    hasIt = isInsideSampleDir = true;
                }

                CheckSamples("Samples", "samples", ref hasSamplesDir);
                CheckSamples("Samples~", "samples~", ref hasSamplesTildeDir);

                if (isInsideSampleDir && entry.Filename == ".sample.json")
                {
                    if (!entry.PathWithCase.EndsWithOrdinal(".sample.json"))
                    {
                        context.AddError("PVP-80-1", $"{entry.PathWithCase}: .sample.json path has incorrect casing");
                    }

                    try
                    {
                        var se = samplesByDir[entry.DirectoryWithCase] = new SampleEntry
                        {
                            Json = context.ReadFileAsJson(entry.PathWithCase),
                            JsonFilePath = entry.PathWithCase,
                        };

                        if (se.Json["path"].IsPresent)
                        {
                            context.AddError("PVP-80-1", $"{entry.PathWithCase}: .sample.json file should not contain \"path\" key");
                        }
                    }
                    catch (Exception e) when (e is JsonException || e is Validator.FailAllException)
                    {
                        context.AddError("PVP-80-1", e.Message);
                    }
                }
            }

            if (hasSamplesDir && hasSamplesTildeDir)
            {
                context.AddError("PVP-80-1", "package has both Samples and Samples~");
            }

            if (hasSamplesDir)
            {
                context.AddError("PVP-81-1", "packed package must have Samples~ directory, not Samples");
            }

            if (manifest["samples"].IsArray)
            {
                var manifestSamples = manifest["samples"].Elements.Select(e => new SampleEntry { Json = e }).ToList();

                // If package has both .sample.json files and a "samples" list in manifest, count must match.
                if (samplesByDir.Count != 0 && manifestSamples.Count != samplesByDir.Count)
                {
                    context.AddError("PVP-80-1", "number of samples in manifest does not match number of .sample.json files");
                }

                foreach (var manifestSample in manifestSamples)
                {
                    try
                    {
                        var sampleDir = manifestSample.Json["path"].String;
                        if (string.IsNullOrWhiteSpace(sampleDir))
                        {
                            context.AddError("PVP-80-1", $"{manifestSample.Definition} specifies a blank path");
                        }
                        else if (!context.DirectoryExists(sampleDir))
                        {
                            context.AddError("PVP-80-1", $"{manifestSample.Definition} specifies a non-existent path: {sampleDir}");
                        }

                        if (samplesByDir.TryGetValue(sampleDir, out var existing))
                        {
                            // We accept an existing definition from a .sample.json file, but not from another package.json entry.
                            if (existing.JsonFilePath == null) // it's from the manifest
                            {
                                context.AddError("PVP-80-1", $"{k_Manifest}: multiple definitions for sample at path {sampleDir}");
                            }
                        }

                        samplesByDir[sampleDir] = manifestSample;
                    }
                    catch (JsonException e)
                    {
                        context.AddError("PVP-80-1", $"{k_Manifest}: {e.Message}");
                    }
                }
            }

            foreach (var item in samplesByDir)
            {
                var sampleDir = item.Key;
                var sampleDirComponents = sampleDir.Split('/');
                var entry = item.Value;

                try
                {
                    var sampleName = entry.Json["displayName"].String;
                    if (sampleName == "")
                    {
                        context.AddError("PVP-80-1", $"{entry.Definition}: displayName must be non-empty");
                    }
                }
                catch (JsonException e)
                {
                    context.AddError("PVP-80-1", $"{entry.Definition}: {e.Message}");
                }

                var rootDirLower = sampleDirComponents[0].ToLowerInvariant();
                if (rootDirLower != "samples" && rootDirLower != "samples~")
                {
                    context.AddError("PVP-80-1", $"{entry.Definition}: samples must be inside Samples~ or Samples directory, not {sampleDir}");
                }

                if (sampleDirComponents.Length > 2)
                {
                    context.AddError("PVP-80-1", $"{entry.Definition}: samples should be either immediately inside {sampleDirComponents[0]} directory or one level below, not {sampleDir}");
                }

                var prefix = sampleDir + "/";
                foreach (var other in samplesByDir)
                {
                    if (other.Key.StartsWithOrdinal(prefix))
                    {
                        context.AddError("PVP-80-1", $"{other.Value.Definition}: sample directory {other.Key} is nested inside another sample");
                    }
                }
            }
        }
    }
}
