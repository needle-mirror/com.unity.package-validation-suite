using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// This file has been copied from unity/trunk Tools/Bee/Bee.Tools/SimpleJsonReader.cs
// Yes, a copy already exists in PVS; but due to code sharing/assembly boundaries,
// here's another. Blame Microsoft for not supporting JSON in .NET until .NET Core 3.

// Changes:
// - namespace and access modifier
// - keyFilter removed
// - all numbers parsed as type double
// - bugfix: now correctly rejects this invalid JSON: "{\"foo\":1,}"
// - bugfix: now correctly rejects this invalid JSON: "[1,]"
// - bugfix: now correctly rejects this invalid JSON: ""
// - bugfix: now correctly rejects this invalid JSON: "\"\n\""
// - bugfix: now correctly rejects this invalid JSON: "01"
// - raises exception on invalid JSON or duplicate keys

namespace PvpXray
{
    class SimpleJsonException : Exception
    {
        public SimpleJsonException(string messageV1, string messageV2) : base(messageV2 ?? messageV1) { LegacyMessage = messageV1; }
        public string FullMessage => PackageFilePath != null ? $"{PackageFilePath}: {Message}" : Message;
        public string LegacyMessage { get; }
        public string LegacyFullMessage => PackageFilePath != null ? $"{PackageFilePath}: {LegacyMessage}" : LegacyMessage;
        public string PackageFilePath { get; set; }
    }

    static class SimpleJsonReader
    {
        static readonly char[] Escapes = new char[128];

        static SimpleJsonReader()
        {
            // Valid single-character escapes
            Escapes['"'] = '"';
            Escapes['\\'] = '\\';
            Escapes['/'] = '/';
            Escapes['b'] = '\b';
            Escapes['f'] = '\f'; // per RFC 8259
            Escapes['n'] = '\n';
            Escapes['r'] = '\r';
            Escapes['t'] = '\t';
        }

        struct ParseContext
        {
            public List<object> Path;
            public bool PermitInvalidJson;

            public void PopPath() => Path.RemoveAt(Path.Count - 1);
            public void SetPathLeaf(object value) => Path[Path.Count - 1] = value;

            public string GetPathError(string error)
            {
                var sb = new StringBuilder(20 + error.Length);
                foreach (var p in Path)
                {
                    var isFirstElement = sb.Length == 0;
                    if (p is ulong arrayIndex) AppendJsonPathElement(sb, arrayIndex.ToString(), true, isFirstElement);
                    else AppendJsonPathElement(sb, (string)p, false, isFirstElement);
                }
                if (sb.Length == 0) sb.Append('.');
                return sb.Append(": ").Append(error).ToString();
            }
        }

        /// <summary>
        /// Reads an object from JSON:
        /// - Objects {...} are returned as Dictionary of string,object
        /// - Arrays [...] are returned as List of object
        /// - Strings "..." are returned as string
        /// - Numbers are returned as double
        /// - "true", "false" and "null" are returned as themselves
        /// </summary>
        /// <param name="json">JSON string</param>
        /// <param name="packageFilePath">Path to be passed to any raised <see cref="SimpleJsonException"/>.</param>
        /// <param name="permitInvalidJson">Whether to allow invalid JSON that was accepted in the past.</param>
        /// <returns>Object read from JSON.</returns>
        public static object Read(string json, string packageFilePath, bool permitInvalidJson = false)
        {
            try
            {
                var parseContext = new ParseContext
                {
                    Path = new List<object>(),
                    PermitInvalidJson = permitInvalidJson,
                };
                var idx = ParseValue(json, 0, parseContext, out var value);
                if (idx == -1)
                    throw new SimpleJsonException("Invalid JSON document", null);
                SkipSpace(json, ref idx, json.Length);
                if (idx != json.Length)
                    throw new SimpleJsonException("Garbage following JSON document", null);
                return value;
            }
            catch (SimpleJsonException e)
            {
                e.PackageFilePath = packageFilePath;
                throw;
            }
        }

        static int SkipUntilStringEnd(string json, int beginIdx, int len)
        {
            for (var i = beginIdx + 1; i < len; i++)
            {
                var c = json[i];
                if (c == '\\')
                {
                    i++; // Skip next character as it is escaped
                }
                else if (c == '"')
                {
                    return i + 1;
                }
            }
            return -1;
        }

        static int CharToHex(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            return -1;
        }

        static string DecodeString(string json, int beginIdx, int endIdx, ParseContext context)
        {
            StringBuilder sb = new StringBuilder(endIdx - beginIdx);
            for (var i = beginIdx + 1; i < endIdx - 1; ++i)
            {
                var c = json[i];
                if (c < 0x20 && !context.PermitInvalidJson)
                    throw new SimpleJsonException(context.GetPathError("literal control character in string"), null);
                if (c == '\\')
                {
                    // handle JSON escapes
                    ++i;
                    if (i >= endIdx - 1)
                        return null;
                    c = json[i];
                    if (c >= 128)
                        return null;
                    if (c == 'u')
                    {
                        // Unicode escape in form of "\uHHHH"
                        if (i + 4 >= endIdx - 1)
                            return null;
                        var h0 = CharToHex(json[i + 1]);
                        var h1 = CharToHex(json[i + 2]);
                        var h2 = CharToHex(json[i + 3]);
                        var h3 = CharToHex(json[i + 4]);
                        var h = (h0 << 12) | (h1 << 8) | (h2 << 4) | h3;
                        if (h < 0)
                            return null;
                        c = (char)h;
                        i += 4;
                    }
                    else
                    {
                        // Simple escape, e.g. "\n"
                        c = Escapes[c];
                        if (c == 0) return null; // not a valid escape
                    }
                }

                sb.Append(c);
            }
            var result = sb.ToString();
            if (!context.PermitInvalidJson && !XrayUtils.IsValidUnicode(result))
                throw new SimpleJsonException(context.GetPathError("string is not valid Unicode"), null);
            return result;
        }

        static void SkipSpace(string json, ref int idx, int len)
        {
            while (idx < len)
            {
                var c = json[idx];
                if (c != '\x20' && c != '\n' && c != '\r' && c != '\t') break;
                ++idx;
            }
        }

        static int ParseDict(string json, int idx, ParseContext context, Dictionary<string, object> res)
        {
            var length = json.Length;
            SkipSpace(json, ref idx, length);
            // empty?
            if (idx < length && json[idx] == '}')
                return idx + 1;

            while (idx < length)
            {
                // key name
                string name;
                if (json[idx] == '"')
                {
                    var endS = SkipUntilStringEnd(json, idx, length);
                    if (endS == length || endS == -1)
                        return -1;
                    {
                        name = DecodeString(json, idx, endS, context);
                        if (name == null)
                            return -1;
                    }
                    idx = endS;
                }
                else
                    return -1;
                SkipSpace(json, ref idx, length);

                // :
                if (idx >= length || json[idx] != ':')
                    return -1;
                ++idx;
                SkipSpace(json, ref idx, length);

                // value
                context.Path.Add(name);
                var endO = ParseValue(json, idx, context, out var val);
                if (endO == -1)
                    return -1;

                if (res.ContainsKey(name))
                {
                    throw new SimpleJsonException($"Duplicate JSON key: {name}", context.GetPathError("duplicate JSON key"));
                }
                context.PopPath();

                res.Add(name, val);
                idx = endO;

                // next should be either , or }
                SkipSpace(json, ref idx, length);
                if (idx >= length)
                    return -1;
                if (json[idx] == '}')
                    return idx + 1;

                if (json[idx] != ',')
                    return -1;
                ++idx;

                SkipSpace(json, ref idx, length);
            }
            return -1;
        }

        static int ParseList(string json, int idx, ParseContext context, List<object> res)
        {
            var length = json.Length;
            SkipSpace(json, ref idx, length);
            // empty?
            if (idx < length && json[idx] == ']')
                return idx + 1;

            var elementIndex = 0ul;
            context.Path.Add(elementIndex);
            while (idx < length)
            {
                // value
                var endO = ParseValue(json, idx, context, out var val);
                if (endO == -1)
                    return -1;
                res.Add(val);
                idx = endO;

                // next should be either , or ]
                SkipSpace(json, ref idx, length);
                if (idx >= length)
                    return -1;
                if (json[idx] == ']')
                {
                    context.PopPath();
                    return idx + 1;
                }

                if (json[idx] != ',')
                    return -1;
                ++idx;

                SkipSpace(json, ref idx, length);
                context.SetPathLeaf(++elementIndex);
            }
            return -1;
        }

        static int ParseValue(string json, int idx, ParseContext context, out object value)
        {
            value = null;

            var length = json.Length;
            SkipSpace(json, ref idx, length);
            if (idx == length)
                return -1;

            var c = json[idx];

            // object
            if (c == '{')
            {
                var dict = new Dictionary<string, object>();
                idx = ParseDict(json, idx + 1, context, dict);
                if (idx == -1)
                    return -1;
                value = dict;
                return idx;
            }

            // array
            if (c == '[')
            {
                var list = new List<object>();
                idx = ParseList(json, idx + 1, context, list);
                if (idx == -1)
                    return -1;
                value = list;
                return idx;
            }

            // string
            if (c == '"')
            {
                var endS = SkipUntilStringEnd(json, idx, length);
                if (endS == -1)
                    return -1;
                {
                    value = DecodeString(json, idx, endS, context);
                    if (value == null)
                        return -1;
                }
                return endS;
            }

            // value (number or true/false/null)
            var endV = idx;
            while (endV < length)
            {
                var c2 = json[endV];
                if (('0' <= c2 && c2 <= '9') || ('a' <= c2 && c2 <= 'z') || c2 == 'E' || c2 == '.' || c2 == '-' || c2 == '+')
                    ++endV;
                else
                    break;
            }

            if (('0' <= c && c <= '9') || c == '-')
            {
                var num = json.SpanOrSubstring(idx, endV - idx);
                if (!context.PermitInvalidJson)
                {
                    var i = c == '-' ? 1 : 0;
                    var period = num.IndexOf('.');
                    var hasLeadingZero = num.Length >= i + 2 && num[i] == '0' && Net7Compat.IsAsciiDigit(num[i + 1]);
                    var hasBadPeriod = period != -1 && (period == 0 || period == num.Length - 1 || !Net7Compat.IsAsciiDigit(num[period - 1]) || !Net7Compat.IsAsciiDigit(num[period + 1]));
                    if (hasLeadingZero || hasBadPeriod)
                        throw new SimpleJsonException(context.GetPathError("invalid JSON number syntax"), null);
                }

                if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                {
                    value = result;
                    return endV;
                }
            }

            var len = endV - idx;
            if (len == 4 && string.CompareOrdinal(json, idx, "true", 0, 4) == 0)
            {
                value = true;
                return endV;
            }
            if (len == 5 && string.CompareOrdinal(json, idx, "false", 0, 5) == 0)
            {
                value = false;
                return endV;
            }
            if (len == 4 && string.CompareOrdinal(json, idx, "null", 0, 4) == 0)
            {
                value = null;
                return endV;
            }

            return -1;
        }

        /// Object property name simple enough to use in Path unquoted.
        static readonly Regex k_SimpleKey = new Regex("^[_a-zA-Z][_a-zA-Z0-9]*$");

        internal static void AppendJsonPathElement(StringBuilder sb, string key, bool isArrayIndex, bool isFirstElement)
        {
            var isIndexOperation = isArrayIndex || !k_SimpleKey.IsMatch(key);
            if (!isIndexOperation || isFirstElement) sb.Append('.');
            if (isIndexOperation) sb.Append('[');

            if (isIndexOperation && !isArrayIndex) // complex object key
            {
                sb.AppendAsJson(key);
            }
            else
            {
                sb.Append(key);
            }

            if (isIndexOperation) sb.Append(']');
        }
    }
}
