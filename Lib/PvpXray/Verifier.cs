using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PvpXray
{
    public class Verifier
    {
        /// All PVP checks implemented by Verifier.
        public static readonly IReadOnlyList<string> Checks =
            Context.Checks
                .Concat(ChangelogVerifier.Checks)
                .Concat(DocumentationVerifier.Checks)
                .Concat(LicenseVerifier.Checks)
                .Concat(ManifestTypeVerifier.Checks)
                .Concat(ManifestVerifier.Checks)
                .Concat(MetaFileVerifier.Checks)
                .Concat(MetaGuidVerifier.Checks)
                .Concat(PathVerifier.Checks)
                .Concat(SampleVerifier.Checks)
                .Concat(ThirdPartyNoticesVerifier.Checks)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

        // If some precondition fails, ALL dependent checks must be marked
        // as failed. This exception may be used to do so.
        internal class FailAllException : Exception
        {
            public FailAllException(string message) : base(message) { }
        }

        internal class Context
        {
            // Checks implemented directly in Context.
            public static readonly string[] Checks = { "PVP-100-1" };

            public Json Manifest => m_Manifest ?? throw new FailAllException(m_ManifestError);
            public IReadOnlyList<string> Files { get; }

            readonly Action<string, string> m_AddError;
            readonly Action<string, string> m_Skip;
            readonly HashSet<string> m_CurrentBatchChecks;
            readonly Json m_Manifest;
            readonly string m_ManifestError;
            readonly IPackage m_Package;
            readonly HashSet<string> m_PreviousErrors;
            // TODO: Remove preprocessor directive when we have at least one check using the Skip method.
#pragma warning disable CS0649
            readonly Dictionary<string, string> m_PreviousSkipReasons;
#pragma warning restore CS0649

            public Context(IPackage package, Action<string, string> addError, Action<string, string> skip)
            {
                m_Package = package;
                m_AddError = addError;
                m_Skip = skip;
                m_CurrentBatchChecks = new HashSet<string>();
                m_PreviousErrors = new HashSet<string>();

                Files = m_Package.Files;

                try
                {
                    var text = ReadFileToString("package.json");

                    // UTF-8 BOM is unwelcome, but we can proceed with verification.
                    if (text.StartsWithOrdinal("\ufeff"))
                    {
                        addError("PVP-100-1", "manifest file contains UTF-8 BOM");
                        text = text.Substring(1);
                    }

                    m_Manifest = new Json(text);
                    m_ManifestError = null;
                }
                catch (Exception e)
                {
                    m_Manifest = null;
                    m_ManifestError = e is JsonException ? "package.json manifest is not valid JSON" : "package.json manifest could not be read";
                    addError("PVP-100-1", m_ManifestError);
                }
            }

            public void AddError(string checkId, string error)
            {
                // deduplicate errors when exactly identical
                if (m_PreviousErrors.Add($"{checkId}: {error}"))
                {
                    if (!m_CurrentBatchChecks.Contains(checkId))
                    {
                        throw new InvalidOperationException($"batch tried to add error with undeclared check ID {checkId}: {error}");
                    }

                    m_AddError(checkId, error);
                }
            }

            public void Skip(string checkId, string reason)
            {
                if (m_PreviousSkipReasons.TryGetValue(checkId, out var previousReason))
                {
                    throw new InvalidOperationException($"batch tried to skip check ID {checkId} more than once; previous skip reason: {previousReason}, new skip reason: {reason}");
                }

                if (!m_CurrentBatchChecks.Contains(checkId))
                {
                    throw new InvalidOperationException($"batch tried to skip undeclared check ID {checkId}: {reason}");
                }

                m_Skip(checkId, reason);
            }

            /// Determine if the specified path exists in package and is a directory. Note that
            /// IPackage never tracks empty directories, so this will return false in that case.
            public bool DirectoryExists(string path)
            {
                path += "/";
                return Files.Any(p => p.StartsWithOrdinal(path));
            }

            public Stream Open(string path) => m_Package.Open(path);

            public Json ReadFileAsJson(string path)
            {
                var text = ReadFileToString(path);

                if (text.StartsWithOrdinal("\ufeff"))
                {
                    throw new FailAllException($"{path}: file contains UTF-8 BOM");
                }

                try
                {
                    return new Json(text);
                }
                catch (JsonException)
                {
                    throw new FailAllException($"{path}: file is not valid JSON");
                }
            }

            public string ReadFileToString(string path)
            {
                try
                {
                    // Read without .NET's magic BOM handling
                    using (var buf = new MemoryStream())
                    using (var stream = Open(path))
                    {
                        stream.CopyTo(buf);
                        return Encoding.UTF8.GetString(buf.ToArray());
                    }
                }
                catch (Exception)
                {
                    throw new FailAllException($"{path}: file could not be read as UTF-8 text");
                }
            }

            public void RunBatch(string[] checks, Action<Context> runner)
            {
                var undeclaredChecks = checks.Except(Verifier.Checks).ToArray();
                if (undeclaredChecks.Any())
                {
                    throw new InvalidOperationException($"batch run for undeclared checks: {string.Join(", ", undeclaredChecks)}");
                }

                m_CurrentBatchChecks.Clear();
                m_CurrentBatchChecks.UnionWith(checks);

                // Since check IDs shouldn't overlap between batches, clear this for a bit of performance.
                m_PreviousErrors.Clear();

                try
                {
                    runner(this);
                }
                catch (FailAllException e)
                {
                    foreach (var check in checks)
                    {
                        AddError(check, e.Message);
                    }
                }
            }
        }

        public void Verify(IPackage package, Action<string, string> addError, Action<string, string> skip, IPvpHttpClient httpClient)
        {
            // TODO: New checks that require access to a network resource must be skipped immediately with reason "offline_requested" if httpClient is null.
            // TODO: New checks that call httpClient.GetStream must be skipped with reason "network_error" if it throws PvpHttpException.

            var context = new Context(package, addError, skip);
            context.RunBatch(ChangelogVerifier.Checks, ChangelogVerifier.Run);
            context.RunBatch(LicenseVerifier.Checks, LicenseVerifier.Run);
            context.RunBatch(ManifestTypeVerifier.Checks, ManifestTypeVerifier.Run);
            context.RunBatch(ManifestVerifier.Checks, ManifestVerifier.Run);
            context.RunBatch(MetaFileVerifier.Checks, MetaFileVerifier.Run);
            context.RunBatch(MetaGuidVerifier.Checks, MetaGuidVerifier.Run);
            context.RunBatch(PathVerifier.Checks, PathVerifier.Run);
            context.RunBatch(SampleVerifier.Checks, SampleVerifier.Run);
            context.RunBatch(ThirdPartyNoticesVerifier.Checks, ThirdPartyNoticesVerifier.Run);
            context.RunBatch(DocumentationVerifier.Checks, DocumentationVerifier.Run);
        }
    }
}
