using System;
using System.Collections.Generic;
using System.Linq;

namespace PvpXray
{
    class SampleVerifier : Verifier.IChecker
    {
        public static string[] Checks => new[] { "PVP-80-1", "PVP-81-1" };
        public static int PassCount => 1;

        const string k_Manifest = "package.json";

        readonly Verifier.IContext m_Context;
        bool m_HasSamplesDir;
        bool m_HasSamplesTildeDir;
        readonly Dictionary<string, SampleEntry> m_SamplesByDir;

        class SampleEntry
        {
            public Json Json;
            public string JsonFilePath;

            public string Definition => JsonFilePath ?? $"{k_Manifest}: {Json.Path}";
        }

        public SampleVerifier(Verifier.IContext context)
        {
            m_Context = context;

            m_HasSamplesDir = false;
            m_HasSamplesTildeDir = false;
            m_SamplesByDir = new Dictionary<string, SampleEntry>();
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            var manifest = m_Context.Manifest;

            var entry = new PathVerifier.Entry(file.Path);
            var isInsideSampleDir = false;

            void CheckSamples(string expected, string expectedLower, ref bool hasIt)
            {
                if (entry.Components[0] != expectedLower) return;
                if (!entry.PathWithCase.StartsWithOrdinal(expected))
                {
                    var actualCasing = entry.PathWithCase.Split('/')[0];
                    m_Context.AddError("PVP-80-1", $"{actualCasing}: path has incorrect casing, should be {expected}");
                }

                hasIt = isInsideSampleDir = true;
            }

            CheckSamples("Samples", "samples", ref m_HasSamplesDir);
            CheckSamples("Samples~", "samples~", ref m_HasSamplesTildeDir);

            if (isInsideSampleDir && entry.Filename == ".sample.json")
            {
                if (!entry.PathWithCase.EndsWithOrdinal(".sample.json"))
                {
                    m_Context.AddError("PVP-80-1", $"{entry.PathWithCase}: .sample.json path has incorrect casing");
                }

                try
                {
                    var se = m_SamplesByDir[entry.DirectoryWithCase] = new SampleEntry
                    {
                        Json = file.ReadAsJson(),
                        JsonFilePath = entry.PathWithCase,
                    };

                    if (se.Json["path"].IsPresent)
                    {
                        m_Context.AddError("PVP-80-1", $"{entry.PathWithCase}: .sample.json file should not contain \"path\" key");
                    }
                }
                catch (Exception e) when (e is JsonException || e is Verifier.FailAllException)
                {
                    m_Context.AddError("PVP-80-1", e.Message);
                }
            }
        }

        public void Finish()
        {
            var manifest = m_Context.Manifest;

            if (m_HasSamplesDir && m_HasSamplesTildeDir)
            {
                m_Context.AddError("PVP-80-1", "package has both Samples and Samples~");
            }

            if (m_HasSamplesDir)
            {
                m_Context.AddError("PVP-81-1", "packed package must have Samples~ directory, not Samples");
            }

            if (manifest["samples"].IsArray)
            {
                var manifestSamples = manifest["samples"].Elements.Select(e => new SampleEntry { Json = e }).ToList();

                // If package has both .sample.json files and a "samples" list in manifest, count must match.
                if (m_SamplesByDir.Count != 0 && manifestSamples.Count != m_SamplesByDir.Count)
                {
                    m_Context.AddError("PVP-80-1", "number of samples in manifest does not match number of .sample.json files");
                }

                foreach (var manifestSample in manifestSamples)
                {
                    try
                    {
                        var sampleDir = manifestSample.Json["path"].String;
                        if (string.IsNullOrWhiteSpace(sampleDir))
                        {
                            m_Context.AddError("PVP-80-1", $"{manifestSample.Definition} specifies a blank path");
                        }
                        else if (!m_Context.DirectoryExists(sampleDir))
                        {
                            m_Context.AddError("PVP-80-1", $"{manifestSample.Definition} specifies a non-existent path: {sampleDir}");
                        }

                        if (m_SamplesByDir.TryGetValue(sampleDir, out var existing))
                        {
                            // We accept an existing definition from a .sample.json file, but not from another package.json entry.
                            if (existing.JsonFilePath == null) // it's from the manifest
                            {
                                m_Context.AddError("PVP-80-1", $"{k_Manifest}: multiple definitions for sample at path {sampleDir}");
                            }
                        }

                        m_SamplesByDir[sampleDir] = manifestSample;
                    }
                    catch (JsonException e)
                    {
                        m_Context.AddError("PVP-80-1", $"{k_Manifest}: {e.Message}");
                    }
                }
            }

            foreach (var item in m_SamplesByDir)
            {
                var sampleDir = item.Key;
                var sampleDirComponents = sampleDir.Split('/');
                var entry = item.Value;

                try
                {
                    var sampleName = entry.Json["displayName"].String;
                    if (sampleName == "")
                    {
                        m_Context.AddError("PVP-80-1", $"{entry.Definition}: displayName must be non-empty");
                    }
                }
                catch (JsonException e)
                {
                    m_Context.AddError("PVP-80-1", $"{entry.Definition}: {e.Message}");
                }

                var rootDirLower = sampleDirComponents[0].ToLowerInvariant();
                if (rootDirLower != "samples" && rootDirLower != "samples~")
                {
                    m_Context.AddError("PVP-80-1", $"{entry.Definition}: samples must be inside Samples~ or Samples directory, not {sampleDir}");
                }

                if (sampleDirComponents.Length > 2)
                {
                    m_Context.AddError("PVP-80-1", $"{entry.Definition}: samples should be either immediately inside {sampleDirComponents[0]} directory or one level below, not {sampleDir}");
                }

                var prefix = sampleDir + "/";
                foreach (var other in m_SamplesByDir)
                {
                    if (other.Key.StartsWithOrdinal(prefix))
                    {
                        m_Context.AddError("PVP-80-1", $"{other.Value.Definition}: sample directory {other.Key} is nested inside another sample");
                    }
                }
            }
        }
    }
}
