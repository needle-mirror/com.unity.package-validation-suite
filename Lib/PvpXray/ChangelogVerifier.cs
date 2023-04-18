using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PvpXray
{
    class ChangelogVerifier : Verifier.IChecker
    {
        const string k_Changelog = "CHANGELOG.md";
        const string k_Manifest = "package.json";

        static readonly Regex k_H2Pattern = new Regex(@"^## (?<text>.*?)\r?$", RegexOptions.Multiline);
        static readonly Regex k_HeaderPattern = new Regex(@"^\[(?<version>.*)\]( - (?<date>\d{4}-\d{2}-\d{2}))?$");
        const string k_DateFormat = "yyyy-MM-dd";

        public static string[] Checks => new[]
        {
            "PVP-40-1", // Changelog sections are well-formed (US-0039)
            "PVP-41-1", // Changelog has no [Unreleased] section (US-0039)
            "PVP-43-1", // Changelog has entry for package version (US-0039)
        };

        public static int PassCount => 1;

        readonly Verifier.IContext m_Context;

        public ChangelogVerifier(Verifier.IContext context)
        {
            m_Context = context;

            if (!context.Files.Contains(k_Changelog))
            {
                throw new Verifier.FailAllException($"{k_Changelog}: file could not be read as UTF-8 text");
            }
        }

        static int LineNumber(string text, int index)
        {
            var lineNumber = 1;
            for (var i = 0; i < index; i++)
            {
                if (text[i] == '\n')
                {
                    lineNumber++;
                }
            }

            return lineNumber;
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            if (file.Path != k_Changelog) return;

            var changelog = file.ReadToString();
            var h2Matches = k_H2Pattern.Matches(changelog);

            if (h2Matches.Count == 0)
            {
                m_Context.AddError("PVP-40-1", $"{k_Changelog}: at least one changelog section is required");
                m_Context.AddError("PVP-43-1", $"{k_Changelog}: missing Unreleased section or section for package version");
            }

            foreach (var (h2Match, sectionIndex) in h2Matches.Cast<Match>().Select((match, index) => (match, index)))
            {
                var h2Text = h2Match.Groups["text"].Value;

                string cachedLocation = null;
                string Location() => cachedLocation = cachedLocation ?? $"line {LineNumber(changelog, h2Match.Index)}";
                void AddErrorWithLocation(string checkId, string error) => m_Context.AddError(checkId, $"{k_Changelog}: {Location()}: {error}");

                var headerMatch = k_HeaderPattern.Match(h2Text);
                if (!headerMatch.Success)
                {
                    AddErrorWithLocation("PVP-40-1", $"header must match regex: {k_HeaderPattern}");
                    continue;
                }

                var version = headerMatch.Groups["version"].Value;
                var dateGroup = headerMatch.Groups["date"];
                var firstSection = sectionIndex == 0;

                if (version == "Unreleased")
                {
                    if (dateGroup.Success)
                    {
                        AddErrorWithLocation("PVP-40-1", "Unreleased section header must not specify a date");
                    }

                    if (!firstSection)
                    {
                        AddErrorWithLocation("PVP-40-1", "Unreleased section is not the first section");
                    }

                    AddErrorWithLocation("PVP-41-1", "Unreleased section is not allowed for public release");
                }
                else
                {
                    if (!ManifestVerifier.SemVer.IsMatch(version))
                    {
                        AddErrorWithLocation("PVP-40-1", "version must be a valid SemVer version");
                    }

                    if (dateGroup.Success)
                    {
                        var date = dateGroup.Value;
                        if (!DateTime.TryParseExact(date, k_DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        {
                            AddErrorWithLocation("PVP-40-1", $"date must be valid and match format: {k_DateFormat}");
                        }
                    }
                    else
                    {
                        AddErrorWithLocation("PVP-40-1", $"date must be specified");
                    }

                    if (firstSection)
                    {
                        try
                        {
                            if (version != m_Context.Manifest["version"].String)
                            {
                                AddErrorWithLocation("PVP-43-1", "version in first section header doesn't match version in package manifest");
                            }
                        }
                        catch (JsonException e)
                        {
                            m_Context.AddError("PVP-43-1", $"{k_Manifest}: ${e.Message}");
                        }
                    }
                }
            }
        }

        public void Finish()
        {
        }
    }
}
