using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PvpXray
{
    class NdaVerifier : Verifier.IChecker
    {
        /// Tools related to the ipvs_hash_v1 hash function.
        public static class IpvsHashV1
        {
            const int k_HashInputSizeMask = 31;

            /// Maximum length of a filtered input stream if we are to hash it.
            public const int MaxHashInputLength = XrayUtils.MaxByteArrayLength & ~k_HashInputSizeMask;

            public static int Filter(byte[] input, byte[] output, int length)
            {
                var filteredLength = 0;
                for (var i = 0; i < length; i++)
                {
                    var b = input[i];
                    if (b != '\r' && b != '\n')
                    {
                        output[filteredLength++] = b;
                    }
                }

                return filteredLength;
            }

            /// Rounds up length to account for padding. length argument must
            /// not exceed int.MaxValue - 31.
            public static int GetPaddedLength(int length)
            {
                // Round up length to a multiple of 32.
                return (length + k_HashInputSizeMask) & ~k_HashInputSizeMask;
            }

            /// Buffer will be modified, and its length must be big enough to
            /// accomodate padding (see GetPaddedLength). length argument must
            /// not exceed MaxHashInputLength, and must be larger than 0, since
            /// our xxHash implementation is broken for lengths less than 32.
            public static ulong Hash(byte[] buffer, int length)
            {
                var paddedLength = GetPaddedLength(length);
                for (var i = length; i < paddedLength; i++)
                {
                    buffer[i] = (byte)'\r';
                }

                var hash = xxHash.Create64(0);
                hash.Write(buffer, 0, paddedLength);
                return hash.Compute();
            }
        }

        public class InvalidNdaPatternFileException : Exception { }

        public struct NdaPatternFile
        {
            public const uint Magic = 0x0041444e; // "NDA\0"
            public const int HeaderSize = 8;
            public const int EntrySize = 8 + 8 + 4;

            public readonly byte[] Contents;
            public readonly int Length;

            public int HashCount => BitConverter.ToInt32(Contents, 4);
            public int JsonOffset => HeaderSize + HashCount * EntrySize;

            public NdaPatternFile(byte[] contents, int length)
            {
                Contents = contents;
                Length = length;

                if (!BitConverter.IsLittleEndian) throw new InvalidOperationException("little-endian machine required");
                if (Length > contents.Length) throw new ArgumentException("length > contents.Length");
                if (Length < HeaderSize) throw new InvalidNdaPatternFileException();

                var magic = BitConverter.ToUInt32(contents, 0);
                if (magic != Magic || contents.Length < JsonOffset) throw new InvalidNdaPatternFileException();
            }

            public string GetJsonString()
            {
                var jsonOffset = JsonOffset;
                return XrayUtils.Utf8Strict.GetString(Contents, jsonOffset, Length - jsonOffset);
            }

            public unsafe int GetSmallestFileSize()
            {
                var hashCount = HashCount;
                var result = int.MaxValue;
                fixed (byte* bytePtr = Contents)
                {
                    var sizeArray = (int*)(bytePtr + HeaderSize + sizeof(ulong) * 2 * hashCount);
                    for (var i = 0; i < hashCount; ++i)
                    {
                        result = Math.Min(result, sizeArray[i]);
                    }
                }
                return result;
            }
        }

        public struct NdaHashSearch
        {
            readonly byte[] m_Patterns;
            readonly int m_HashCount;
            readonly ulong m_TargetPrefixHash;
            int m_Index;

            public unsafe NdaHashSearch(NdaPatternFile patternFile, ulong prefixHash)
            {
                m_Patterns = patternFile.Contents;
                m_HashCount = patternFile.HashCount;
                m_TargetPrefixHash = prefixHash;

                fixed (byte* bytePtr = m_Patterns)
                {
                    var hashArray = (ulong*)(bytePtr + NdaPatternFile.HeaderSize);
                    var left = 0;
                    var right = m_HashCount - 1;

                    while (left < right)
                    {
                        var mid = (left + right) / 2; // cannot overflow, as we're counting ulongs not bytes
                        if (hashArray[mid] < prefixHash)
                        {
                            left = mid + 1;
                        }
                        else
                        {
                            right = mid;
                        }
                    }
                    m_Index = left;
                }
            }

            public unsafe bool Next(out int size, out ulong fullHash)
            {
                fixed (byte* bytePtr = m_Patterns)
                {
                    var hashArray = (ulong*)(bytePtr + NdaPatternFile.HeaderSize);
                    if (m_Index >= m_HashCount || hashArray[m_Index] != m_TargetPrefixHash)
                    {
                        size = 0;
                        fullHash = 0;
                        return false;
                    }

                    // the array of full hashes comes right after the array of prefix hashes
                    fullHash = hashArray[m_Index + m_HashCount];

                    var sizeArray = (int*)(bytePtr + NdaPatternFile.HeaderSize + sizeof(ulong) * 2 * m_HashCount);
                    size = sizeArray[m_Index];

                    ++m_Index;
                    return true;
                }
            }
        }

        class WildcardSet
        {
            readonly Regex m_PathRegex;
            readonly Regex m_FilenameRegex;

            public WildcardSet(IEnumerable<string> patterns)
            {
                var filenameRegexCount = 0;
                var pathRegexCount = 0;
                var pathRegex = new StringBuilder("^(?:");
                var filenameRegex = new StringBuilder("^(?:");

                foreach (var pattern in patterns)
                {
                    int count;
                    StringBuilder regex;
                    if (pattern.Contains('/'))
                    {
                        regex = pathRegex;
                        count = pathRegexCount++;
                    }
                    else
                    {
                        regex = filenameRegex;
                        count = filenameRegexCount++;
                    }

                    if (count != 0) regex.Append('|');
                    regex.Append(Regex.Escape(pattern).Replace(@"\?", ".").Replace(@"\*", ".*"));
                }

                if (pathRegexCount != 0)
                {
                    pathRegex.Append(")$");
                    m_PathRegex = new Regex(pathRegex.ToString().ToLowerInvariant(), k_FilenameRegexOptions);
                }

                if (filenameRegexCount != 0)
                {
                    filenameRegex.Append(")$");
                    m_FilenameRegex = new Regex(filenameRegex.ToString().ToLowerInvariant(), k_FilenameRegexOptions);
                }
            }

            public bool IsMatch(string fullPathLower, int filenameIndex)
            {
                return (m_PathRegex != null && m_PathRegex.IsMatch(fullPathLower)) || (m_FilenameRegex != null && m_FilenameRegex.IsMatch(fullPathLower, filenameIndex));
            }
        }

        const string k_NdaPatternUrl = "https://artifactory-upload.prd.it.unity3d.com/artifactory/pets-internal/pvp/nda_patterns_v1.dat";
        const RegexOptions k_FilenameRegexOptions = 0;
        const RegexOptions k_ContentRegexOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        const int k_PrefixLength = 1024; // Already divisible by 32.
        const int k_ReplacementFileMaxSize = 1024;
        static readonly byte[] k_ReplacementFileKeyword = XrayUtils.Utf8Strict.GetBytes("ipvs_hash_v1: ");

        public static string[] Checks => new[] { "PVP-90-1", "PVP-91-1", "PVP-92-1" };
        public static int PassCount => 1;

        readonly byte[] m_PrefixBuffer = new byte[k_PrefixLength];
        readonly NdaPatternFile m_PatternFile;
        readonly Verifier.Context m_Context;
        readonly WildcardSet m_Paths;
        readonly WildcardSet m_ContentIgnorePaths;
        readonly List<(WildcardSet, Regex)> m_ContentRegexes;
        readonly int m_SmallestHashFile;
        readonly bool m_EnableReplacementFiles;

        public NdaVerifier(Verifier.Context context)
        {
            m_Context = context;

            byte[] patternArray;
            int patternLength;

            var ndaPatternPath = m_Context.GetXrayEnv("PVP_INTERNAL_NDA_PATTERN_FILE");
            if (ndaPatternPath != null)
            {
                patternArray = File.ReadAllBytes(ndaPatternPath);
                patternLength = patternArray.Length;
            }
            else
            {
                var stream = context.HttpClient.GetStream(k_NdaPatternUrl, out var status);
                PvpHttpException.CheckHttpStatus(k_NdaPatternUrl, status, 200);
                XrayUtils.GetStreamArray(stream, out patternArray, out patternLength);
            }

            try
            {
                m_PatternFile = new NdaPatternFile(patternArray, patternLength);
            }
            catch (InvalidNdaPatternFileException)
            {
                throw new Verifier.SkipAllException("invalid_baseline");
            }

            try
            {
                var json = (Dictionary<string, object>)SimpleJsonReader.Read(m_PatternFile.GetJsonString(), null);
                m_Paths = new WildcardSet(((List<object>)json["paths"]).Cast<string>());
                m_ContentIgnorePaths = new WildcardSet(((List<object>)json["content_ignore_paths"]).Cast<string>());
                m_ContentRegexes = new List<(WildcardSet, Regex)>();
                foreach (var entry in (List<object>)json["content_regexes"])
                {
                    var elements = (List<object>)entry;
                    var paths = new WildcardSet(((List<object>)elements[0]).Cast<string>());
                    var regexes = ((List<object>)elements[1]).Cast<string>();
                    var regex = new Regex(string.Join("|", regexes), k_ContentRegexOptions);
                    m_ContentRegexes.Add((paths, regex));
                }

                {
                    var elements = (List<object>)json["content_keywords"];
                    var paths = new WildcardSet(((List<object>)elements[0]).Cast<string>());
                    var keywords = ((List<object>)elements[1]).Select(e => Regex.Escape((string)e));
                    var regex = new Regex($"[\"/\\\\](?:{string.Join("|", keywords)})\"", k_ContentRegexOptions);
                    m_ContentRegexes.Add((paths, regex));
                }
            }
            catch (Exception e) when (e is SimpleJsonException || e is InvalidCastException || e is ArgumentException)
            {
                throw new Verifier.SkipAllException("invalid_baseline");
            }

            m_SmallestHashFile = m_PatternFile.GetSmallestFileSize();

            m_Context.SetBlobBaseline("nda_patterns", patternArray, patternLength);

            m_EnableReplacementFiles = m_Context.GetXrayEnv("PVP_INTERNAL_NDA_REPLACEMENT_FILES") == "1";
        }

        static bool TryParseReplacementFile(byte[] content, out ulong prefixHash, out ulong fullHash, out int fullFilterLength)
        {
            prefixHash = 0;
            fullHash = 0;
            fullFilterLength = 0;

            // A replacement file must end with a NUL byte.
            if (content.Length == 0 || content[content.Length - 1] != 0) return false;

            // Look for keyword.
            for (var i = 0; i <= content.Length - k_ReplacementFileKeyword.Length; i++)
            {
                int j;
                for (j = 0; j < k_ReplacementFileKeyword.Length && content[i + j] == k_ReplacementFileKeyword[j]; j++)
                {
                }

                if (j != k_ReplacementFileKeyword.Length) continue;

                // Extract hashes (assumes correct format; throws otherwise).
                var cursor = i + j;
                prefixHash = Convert.ToUInt64(XrayUtils.Utf8Strict.GetString(content, cursor, 16), 16); cursor += 16 + 1;
                fullHash = Convert.ToUInt64(XrayUtils.Utf8Strict.GetString(content, cursor, 16), 16); cursor += 16 + 1;
                var eolIndex = Array.IndexOf(content, (byte)'\n', cursor, content.Length - cursor);
                fullFilterLength = Convert.ToInt32(XrayUtils.Utf8Strict.GetString(content, cursor, eolIndex - cursor), 16);
                return true;
            }

            return false;
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            // Path check
            var pathLower = file.Entry.Path;
            var filenameIndex = pathLower.LastIndexOf('/') + 1;
            if (m_Paths.IsMatch(pathLower, filenameIndex))
            {
                m_Context.AddError("PVP-91-1", file.Path);
            }

            // Hash check
            ulong prefixHash = 0;
            ulong fullHash = 0;
            var fullFilterLength = 0;

            var replacementFile = false;
            if (m_EnableReplacementFiles && file.Size <= k_ReplacementFileMaxSize)
            {
                // Attempt to extract hashes from potential replacement file (instead of computing them).
                replacementFile = TryParseReplacementFile(file.Content, out prefixHash, out fullHash, out fullFilterLength);
            }

            // Skip hashing if file is too big to be read into a byte array (or
            // too small to match any NDA file entry).
            if (replacementFile || (file.Size >= m_SmallestHashFile && file.Size <= XrayUtils.MaxByteArrayLength))
            {
                var fileSize = (int)file.Size;

                if (!replacementFile)
                {
                    var prefixFilterLength = IpvsHashV1.Filter(file.Content, m_PrefixBuffer, Math.Min(k_PrefixLength, fileSize));
                    prefixHash = IpvsHashV1.Hash(m_PrefixBuffer, prefixFilterLength);

                    // Start with values that are correct if file is at most k_PrefixLength bytes.
                    fullFilterLength = prefixFilterLength;
                    fullHash = prefixHash;
                }

                var search = new NdaHashSearch(m_PatternFile, prefixHash);

                byte[] fullBuffer = null;
                var fullHashComputed = replacementFile || fileSize <= k_PrefixLength;
                while (search.Next(out var candidateSize, out var candidateFullHash))
                {
                    if (!replacementFile)
                    {
                        // Check against unfiltered length.
                        if (candidateSize > fileSize) continue;

                        if (fullBuffer == null && fileSize > k_PrefixLength)
                        {
                            // GetPaddedLength limits on length argument satisfied, since file.Size <= MaxByteArrayLength.
                            var paddedLength = IpvsHashV1.GetPaddedLength(fileSize);

                            // This code path should be reasonably rare, so simply allocate a new buffer every time.
                            fullBuffer = new byte[paddedLength];
                            fullFilterLength = IpvsHashV1.Filter(file.Content, fullBuffer, fileSize);
                        }
                    }

                    // Check against filtered length.
                    if (candidateSize != fullFilterLength) continue;

                    if (!fullHashComputed)
                    {
                        if (fullFilterLength > IpvsHashV1.MaxHashInputLength)
                        {
                            // File is too large to hash even after filtering; assume no match.
                            break;
                        }

                        fullHash = IpvsHashV1.Hash(fullBuffer, fullFilterLength);
                        fullHashComputed = true;
                    }

                    if (candidateFullHash != fullHash) continue;

                    m_Context.AddError("PVP-90-1", file.Path);
                }
            }

            // Content check
            if (!m_ContentIgnorePaths.IsMatch(pathLower, filenameIndex))
            {
                string content = null;
                foreach (var (paths, regex) in m_ContentRegexes)
                {
                    if (!paths.IsMatch(pathLower, filenameIndex)) continue;

                    if (content == null)
                    {
                        try
                        {
                            content = file.ReadToStringLegacy();
                        }
                        catch (Verifier.FailAllException e)
                        {
                            // If filename matches path wildcards and is either
                            // invalid UTF-8 or too big to read, fail PVP-92-1
                            // regardless of file contents (but only PVP-92-1).
                            m_Context.AddError("PVP-92-1", e.Message);
                            break;
                        }
                    }

                    if (regex.IsMatch(content))
                    {
                        m_Context.AddError("PVP-92-1", file.Path);
                        break;
                    }
                }
            }
        }

        public void Finish()
        {
        }
    }
}
