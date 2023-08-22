using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using YamlMapping = System.Collections.Generic.Dictionary<string, object>;

namespace PvpXray
{
    class YamlAccessException : Exception
    {
        public string PackageFilePath { get; set; }
        public string NodePath { get; }
        public string FullMessage => $"{(PackageFilePath != null ? $"{PackageFilePath}: " : "")}{NodePath}: {Message}";

        public YamlAccessException(string message, string nodePath) : base(message)
        {
            NodePath = nodePath;
        }
    }

    public class Yaml
    {
        /// Object property name simple enough to use in Path unquoted.
        static readonly Regex k_SimpleKey = new Regex("^[_a-zA-Z][_a-zA-Z0-9]*$");

        readonly object m_Node;
        readonly Yaml m_Parent;
        readonly string m_ParentKey;

        // The key of this YAML node in the parent mapping, or the index (as a
        // string) in the parent sequence. Throws if this is the root node.
        public string Key => m_ParentKey ?? throw new InvalidOperationException("Cannot take Key of a YAML root node");
        string PackageFilePath { get; }

        void BuildPath(StringBuilder sb)
        {
            if (m_Parent != null)
            {
                m_Parent.BuildPath(sb);

                var isIndexOperation = m_Parent.IsSequence || !k_SimpleKey.IsMatch(Key);
                if (!isIndexOperation || sb.Length == 0)
                {
                    sb.Append('.');
                }
                if (isIndexOperation)
                {
                    sb.Append('[');
                }

                if (isIndexOperation && m_Parent.IsMapping) // complex object key
                {
                    Encode(Key, sb);
                }
                else
                {
                    sb.Append(Key);
                }

                if (isIndexOperation)
                {
                    sb.Append(']');
                }
            }
        }

        static readonly Dictionary<Type, string> k_YamlTagNames = new Dictionary<Type, string>()
        {
            [typeof(Undefined)] = "undefined",
            [typeof(YamlMapping)] = "mapping",
            [typeof(List<object>)] = "sequence",
            [typeof(string)] = "string",
            [typeof(void)] = "null",
            [typeof(bool)] = "boolean",
            [typeof(int)] = "integer",
            [typeof(float)] = "floating point",
        };

        Type Tag => m_Node == null ? typeof(void) : m_Node.GetType();

        T CheckTag<T>()
        {
            if (Tag != typeof(T))
            {
                throw new YamlAccessException($"wrong tag, expected {k_YamlTagNames[typeof(T)]} but was {k_YamlTagNames[Tag]}", Path) { PackageFilePath = PackageFilePath };
            }

            return (T)m_Node;
        }

        Yaml(object node, Yaml parent, string parentKey, string packageFilePath)
        {
            m_Node = node;
            m_Parent = parent;
            m_ParentKey = parentKey;
            PackageFilePath = packageFilePath;
        }

        /// <summary>
        /// Can only parse small subset of YAML; see <see cref="MiniYamlParser"/>.
        /// </summary>
        public Yaml(string yaml, string packageFilePath) : this(new MiniYamlParser(yaml, packageFilePath).Node, null, null, packageFilePath) { }

        // Enumerate elements of a YAML sequence.
        public IEnumerable<Yaml> Elements
            => CheckTag<List<object>>().Select((elm, index) => new Yaml(elm, this, index.ToString(), PackageFilePath));

        public IEnumerable<Yaml> ElementsIfPresent
            => IfPresent?.Elements ?? Enumerable.Empty<Yaml>();

        // Enumerate members of a YAML mapping.
        public IEnumerable<Yaml> Members
            => RawMapping.Select(kv => new Yaml(kv.Value, this, kv.Key, PackageFilePath));

        public IEnumerable<Yaml> MembersIfPresent
            => IfPresent?.Members ?? Enumerable.Empty<Yaml>();

        public Yaml IfPresent => IsPresent ? this : null;
        public bool IsPresent => !(m_Node is Undefined);

        public bool IsMapping => Tag == typeof(YamlMapping);
        public bool IsSequence => Tag == typeof(List<object>);
        public bool IsString => Tag == typeof(string);
        public bool IsNull => Tag == typeof(void);
        public bool IsBoolean => Tag == typeof(bool);
        public bool IsInteger => Tag == typeof(int);
        public bool IsFloatingPoint => Tag == typeof(float);

        /// Returns the "location" of this element in the document as a jq-style path.
        public string Path
        {
            get
            {
                var sb = new StringBuilder();
                BuildPath(sb);
                return sb.Length == 0 ? "." : sb.ToString();
            }
        }

        YamlMapping RawMapping => CheckTag<YamlMapping>();

        public string String => CheckTag<string>();
        public bool Boolean => CheckTag<bool>();
        public int Integer => CheckTag<int>();
        public float FloatingPoint => CheckTag<float>();

        public Yaml this[string key] => new Yaml(RawMapping.TryGetValue(key, out var result) ? result : Undefined.Undefined, this, key, PackageFilePath);

        public static string Encode(string str)
        {
            var sb = new StringBuilder();
            Encode(str, sb);
            return sb.ToString();
        }

        internal static void Encode(string str, StringBuilder sb)
        {
            sb.Append('"');
            foreach (var c in str)
            {
                var raw = (c >= ' ' && c <= '~' && c != '"' && c != '\\') // Printable ASCII except '"' and '\'.
                    || (c >= 0xa0 && c != 0xfeff && c != 0xfffe && c != 0xffff); // Effectively U+00A0-U+D7FF, U+E000-U+FEFE, U+FF00-U+FFFD, and U+10000-U+10FFFF.
                if (raw) sb.Append(c);
                else
                {
                    sb.Append('\\');
                    var j = "\"\\\n\r\t\b\f".IndexOf(c);
                    if (j >= 0)
                        sb.Append("\"\\nrtbf"[j]);
                    else
                        sb.AppendFormat("u{0:X4}", (uint)c);
                }
            }
            sb.Append('"');
        }
    }

    class YamlParseException : Exception
    {
        public string PackageFilePath { get; set; }
        public int Line { get; }
        public int Column { get; }
        public string FullMessage => $"{(PackageFilePath != null ? $"{PackageFilePath}:" : "")}{Line}:{Column}: {Message}";

        public YamlParseException(string message, int line, int column) : base(message)
        {
            Line = line;
            Column = column;
        }
    }

    /// Parses small subset of YAML 1.1/1.2 syntax, allowing only plain and
    /// double-quoted untagged single-line scalars, block mappings (with
    /// implicit string scalar keys), block sequences, the empty flow
    /// sequence (i.e. "[]"), and the empty flow mapping (i.e. "{}").
    ///
    /// Implicit tags are resolved according to the YAML 1.2.2 Core schema.
    ///
    /// The following is not allowed for YAML 1.1/1.2 compatibility:
    /// - YAML 1.1-only boolean notation (e.g. yes/no).
    /// - Tab; sometimes valid YAML 1.2 but invalid YAML 1.1.
    /// - Non-ASCII line breaks; only breaks line in YAML 1.1.
    /// - Escaped forward slash; YAML 1.2 only.
    /// - U+007F-U+0084, U+0086-U+009F, U+FFFE, and U+FFFF in double-quoted
    ///   scalar; YAML 1.2 only.
    /// - U+FEFF (byte order marker) except once at the beginning; allowed in
    ///   plain scalar in YAML 1.1 but not 1.2, multiple BOMs are confusing.
    ///
    /// The following is not allowed for simplicity/clarity:
    /// - '~' as null plain scalar.
    /// - Base 8/16 integer plain scalar (e.g. 0o7, 0xf).
    /// - Unrecognized capitalization of plain scalar (e.g. nULL/YeS).
    class MiniYamlParser
    {
        const char k_ByteOrderMarker = '\ufeff';
        const RegexOptions k_IgnoreCase = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // https://yaml.org/spec/1.2.2/#103-core-schema
        static readonly Regex k_Null = new Regex("^(?:null|Null|NULL|~)$");
        static readonly Regex k_NullIgnoreCase = new Regex(k_Null.ToString(), k_IgnoreCase);
        static readonly Regex k_Bool = new Regex("^(?:true|True|TRUE|false|False|FALSE)$");
        static readonly Regex k_BoolIgnoreCase = new Regex(k_Bool.ToString(), k_IgnoreCase);
        static readonly Regex k_IntBase10 = new Regex("^[-+]?[0-9]+$");
        static readonly Regex k_IntBase8 = new Regex("^0o[0-7]+$");
        static readonly Regex k_IntBase8IgnoreCase = new Regex(k_IntBase8.ToString(), k_IgnoreCase);
        static readonly Regex k_IntBase16 = new Regex("^0x[0-9a-fA-F]+$");
        static readonly Regex k_IntBase16IgnoreCase = new Regex(k_IntBase16.ToString(), k_IgnoreCase);
        static readonly Regex k_FloatNumber = new Regex(@"^[-+]?(?:\.[0-9]+|[0-9]+(?:\.[0-9]*)?)(?:[eE][-+]?[0-9]+)?$");
        static readonly Regex k_FloatInfinity = new Regex(@"[-+]?(?:\.inf|\.Inf|\.INF)$");
        static readonly Regex k_FloatInfinityIgnoreCase = new Regex(k_FloatInfinity.ToString(), k_IgnoreCase);
        static readonly Regex k_FloatNaN = new Regex(@"^(?:\.nan|\.NaN|\.NAN)$");
        static readonly Regex k_FloatNaNIgnoreCase = new Regex(k_FloatNaN.ToString(), k_IgnoreCase);

        // https://yaml.org/type/bool.html
        static readonly Regex k_Yaml11Bool = new Regex(@"^(?:y|Y|yes|Yes|YES|n|N|no|No|NO|true|True|TRUE|false|False|FALSE|on|On|ON|off|Off|OFF)$");
        static readonly Regex k_Yaml11BoolIgnoreCase = new Regex(k_Yaml11Bool.ToString(), k_IgnoreCase);

        static readonly float k_NegativeZero;

        int m_LineNumber = 1;
        int m_LineStartIndex;

        bool m_PlainScalarContinuable; // Used to determine correct error message without parsing multi-line scalars.

        string Input { get; }
        int Index { get; set; }
        public object Node { get; }

        static MiniYamlParser()
        {
            var bytes = new byte[4];
            bytes[BitConverter.IsLittleEndian ? 3 : 0] = 0x80;
            k_NegativeZero = BitConverter.ToSingle(bytes, 0);
        }

        /// Assumes valid UTF-16 encoding.
        public MiniYamlParser(string yaml, string packageFilePath)
        {
            Input = yaml;

            try
            {
                Node = ParseDocument();
            }
            catch (YamlParseException e)
            {
                e.PackageFilePath = packageFilePath;
                throw;
            }
        }

        object ParseDocument()
        {
            Accept(k_ByteOrderMarker);
            var indent = SkipCommentLines();
            var node = ParseNode(ref indent);
            if (!EndOfInput) throw Fail(m_PlainScalarContinuable ? "multi-line plain scalar not allowed" : "expected end of input");
            return node;
        }

        /// Skip comment lines (and indentation).
        int SkipCommentLines()
        {
            int indent;
            do
            {
                indent = SkipSpaces();
                SkipTrailingComment();
            } while (AcceptLineBreak());
            return indent;
        }

        int SkipSpaces()
        {
            var start = Index;
            while (Accept(' ')) { }
            return Index - start;
        }

        /// Skip trailing comment (but keep line break).
        void SkipTrailingComment()
        {
            if (!Accept('#')) return;
            char c;
            while (!EndOfInput && (c = Next) != '\n' && c != '\r') ExpectNonBreakCodeUnit();
            m_PlainScalarContinuable = false; // Comment cannot appear inside multi-line plain scalar.
        }

        /// Parse node (and skip comment lines and indentation).
        object ParseNode(ref int indent)
        {
            if (!EndOfInput && Next == '-' && !PlainScalarPrefix) return ParseBlockSequence(ref indent);
            var start = Index;
            var scalar = ParseScalar(out var end);
            if (Accept(':')) return ParseBlockMapping(ref indent, scalar, start, end);
            SkipTrailingComment();
            if (AcceptLineBreak()) indent = SkipCommentLines();
            return scalar;
        }

        YamlParseException NonStringBlockMappingKeyNotAllowed(int start, int end)
        {
            var raw = start == end ? "(empty)" : Input.Substring(start, end - start);
            throw Fail($"non-string block mapping key not allowed: {raw}", start);
        }

        /// Look two characters ahead to determine if we're about to parse a plain scalar.
        /// Only relevant when next character is '?', ':', or '-'.
        bool PlainScalarPrefix
        {
            get
            {
                var i = Index + 1;
                if (i >= Input.Length) return false;
                var c = Input[i];
                return c != '\n' && c != '\r' && c != '\t' && c != ' ';
            }
        }

        /// Parse scalar or empty flow collection (and skip trailing spaces).
        object ParseScalar(out int end)
        {
            if (!EndOfInput)
            {
                var c = Next;
                switch (c)
                {
                    case '[': return ParseEmptyFlowSequence(out end);
                    case '{': return ParseEmptyFlowMapping(out end);
                    case '|': case '>': throw Fail($"block scalar not allowed: {c}");
                    case '\'': throw Fail($"single-quoted scalar not allowed: {c}");
                    case '"': return ParseDoubleQuotedScalar(out end);
                }
            }

            return ParsePlainScalar(out end);
        }

        object ParseEmptyFlowSequence(out int end)
        {
            var start = Index;
            Index++; // Skip '['.
            SkipSpaces();
            if (!Accept(']')) throw Fail("non-empty/multi-line flow sequence not allowed", start);
            SkipSpaces();
            end = Index;
            return new List<object>();
        }

        object ParseEmptyFlowMapping(out int end)
        {
            var start = Index;
            Index++; // Skip '{'.
            SkipSpaces();
            if (!Accept('}')) throw Fail("non-empty/multi-line flow mapping not allowed", start);
            SkipSpaces();
            end = Index;
            return new YamlMapping();
        }

        object ParseDoubleQuotedScalar(out int end)
        {
            var start = Index;
            Index++; // Skip '"';
            var builder = new StringBuilder();
            while (!EndOfInput)
            {
                var c = Next;
                switch (c)
                {
                    case '"':
                        Index++;
                        SkipSpaces();
                        end = Index;
                        return builder.ToString();
                    case '\\': builder.Append(ParseEscapeSequence()); continue;
                    case '\n': case '\r': throw Fail("multi-line double-quoted scalar not allowed", start);
                }
                ExpectNonBreakCodeUnit(doubleQuotedScalar: true);
                builder.Append(c);
            }
            throw Fail("unterminated double-quoted scalar", start);
        }

        string ParseEscapeSequence()
        {
            var start = Index;
            Index++; // Skip '\'.
            YamlParseException InvalidEscapeSequence() => Fail($"invalid escape sequence: {Input.Substring(start, Index - start)}", start);
            if (EndOfInput) throw InvalidEscapeSequence();
            var c = Input[Index++];
            switch (c)
            {
                case '0': return "\0";
                case 'a': return "\a";
                case 'b': return "\b";
                case 't': case '\t': return "\t";
                case 'n': return "\n";
                case 'v': return "\v";
                case 'f': return "\f";
                case 'r': return "\r";
                case 'e': return "\x1b";
                case ' ': return " ";
                case '"': return "\"";
                case '/': throw Fail(@"forward slash escape sequence not allowed: \/", start);
                case '\\': return "\\";
                case 'N': return "\x85";
                case '_': return "\xa0";
                case 'L': return "\x2028";
                case 'P': return "\x2029";
                case 'x': return ParseEscapedUnicodeCodePoint(2, InvalidEscapeSequence);
                case 'u': return ParseEscapedUnicodeCodePoint(4, InvalidEscapeSequence);
                case 'U': return ParseEscapedUnicodeCodePoint(8, InvalidEscapeSequence);
            }
            throw InvalidEscapeSequence();
        }

        string ParseEscapedUnicodeCodePoint(int digits, Func<YamlParseException> invalidEscapeSequence)
        {
            var start = Index;
            for (var i = 0; i < digits; i++)
            {
                if (EndOfInput) throw invalidEscapeSequence();
                var c = Next;
                Index++;
                var isHexDigit = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
                if (!isHexDigit) throw invalidEscapeSequence();
            }

            var codePoint = Convert.ToUInt32(Input.Substring(start, Index - start), 16);
            if (codePoint > int.MaxValue) throw invalidEscapeSequence();
            try
            {
                return char.ConvertFromUtf32((int)codePoint);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw invalidEscapeSequence();
            }
        }

        object ParsePlainScalar(out int end)
        {
            m_PlainScalarContinuable = true;

            var start = end = Index;
            if (m_LineStartIndex == Index && Accept("---", "..."))
            {
                throw Fail($"document marker not allowed: {Input.Substring(start, Index - start)}", start);
            }

            if (!EndOfInput)
            {
                var c = Next;
                switch (c)
                {
                    case '-' when !PlainScalarPrefix: throw Fail($"unexpected block sequence indicator: {c}");
                    case '?' when !PlainScalarPrefix: throw Fail($"explicit block mapping key not allowed: {c}");
                    case ',': case ']': case '}': case '@': case '`': throw Fail($"indicator cannot start plain scalar: {c}");
                    case '&': throw Fail($"node anchor not allowed: {c}");
                    case '*': throw Fail($"alias node not allowed: {c}");
                    case '!': throw Fail($"explicit tag not allowed: {c}");
                    case '%': throw Fail($"directive not allowed: {c}");
                }
            }

            end = start;
            while (!EndOfInput)
            {
                var c = Next;
                if (c == ':' && !PlainScalarPrefix) break;
                if (c == '#' && end != Index) break;
                if (c == '\r' || c == '\n') break;
                ExpectNonBreakCodeUnit();
                if (c != ' ') end = Index;
            }

            var length = end - start;
            string String() => Input.Substring(start, length);
            bool IsMatch(Regex regex) => regex.Match(Input, start, length).Success;

            // Null
            if (length == 0) return null;
            var first = Input[start];
            if (length == 1 && first == '~') throw Fail("~ (null) not allowed", start);
            if (IsMatch(k_Null)) return null;

            // Bool
            if (IsMatch(k_Bool)) return first == 't' || first == 'T';

            // Integer
            try
            {
                if (IsMatch(k_IntBase10)) return int.Parse(String(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                throw Fail($"base 10 integer cannot be represented by 32-bit signed integer: {String()}", start);
            }
            if (IsMatch(k_IntBase8)) throw Fail($"base 8 integer not allowed: {String()}", start);
            if (IsMatch(k_IntBase16)) throw Fail($"base 16 integer not allowed: {String()}", start);

            // Floating point
            try
            {
                if (IsMatch(k_FloatNumber))
                {
                    var f = float.Parse(String(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture);
                    if (f == 0 && first == '-') return k_NegativeZero; // Mono doesn't parse negative zero correctly.
                    return f;
                }
            }
            catch (OverflowException)
            {
                return first == '-' ? float.NegativeInfinity : float.PositiveInfinity;
            }
            if (IsMatch(k_FloatInfinity)) return first == '-' ? float.NegativeInfinity : float.PositiveInfinity;
            if (IsMatch(k_FloatNaN)) return float.NaN;

            // Reject YAML 1.1 boolean notation.
            if (IsMatch(k_Yaml11Bool)) throw Fail($"YAML 1.1 boolean variation not allowed: {String()}", start);

            // Reject "unrecognized" capitalization.
            if (IsMatch(k_NullIgnoreCase)) throw Fail($"unrecognized capitalization of null not allowed: {String()}", start);
            if (IsMatch(k_BoolIgnoreCase)) throw Fail($"unrecognized capitalization of boolean not allowed: {String()}", start);
            if (IsMatch(k_IntBase8IgnoreCase)) throw Fail($"unrecognized capitalization of base 8 integer not allowed: {String()}", start);
            if (IsMatch(k_IntBase16IgnoreCase)) throw Fail($"unrecognized capitalization of base 16 integer not allowed: {String()}", start);
            if (IsMatch(k_FloatInfinityIgnoreCase)) throw Fail($"unrecognized capitalization of floating point infinity not allowed: {String()}", start);
            if (IsMatch(k_FloatNaNIgnoreCase)) throw Fail($"unrecognized capitalization of floating point not-a-number not allowed: {String()}", start);
            if (IsMatch(k_Yaml11BoolIgnoreCase)) throw Fail($"unrecognized capitalization of YAML 1.1 boolean variation not allowed: {String()}", start);

            return String();
        }

        object ParseBlockSequence(ref int indent)
        {
            var sequence = new List<object>();
            var firstIndent = indent;

            do
            {
                // Skip '-', separation spaces, and trailing comment.
                Index++;
                indent += 1 + SkipSpaces();
                SkipTrailingComment();

                if (EndOfInput)
                {
                    sequence.Add(null);
                    return sequence;
                }

                if (AcceptLineBreak())
                {
                    indent = SkipCommentLines();
                    var nested = indent > firstIndent;
                    sequence.Add(nested ? ParseNode(ref indent) : null);
                }
                else
                {
                    sequence.Add(ParseNode(ref indent));
                }
            } while (indent == firstIndent && !EndOfInput && Next == '-' && !PlainScalarPrefix);

            if (indent <= firstIndent) m_PlainScalarContinuable = false; // Multi-line plain scalar must respect indentation.
            else if (!m_PlainScalarContinuable && !EndOfInput && Next == '-' && !PlainScalarPrefix) throw Fail("bad indentation of block sequence entry");

            return sequence;
        }

        object ParseBlockMapping(ref int indent, object key, int keyStart, int keyEnd)
        {
            var mapping = new YamlMapping();
            var firstIndent = indent;

            // First key (and key indicator) already parsed at this point.
            while (true)
            {
                m_PlainScalarContinuable = false; // Key indicator cannot appear inside multi-line plain scalar.
                if (!(key is string stringKey)) throw NonStringBlockMappingKeyNotAllowed(keyStart, keyEnd);
                CheckKeyLookaheadLimit(keyStart);
                if (mapping.ContainsKey(stringKey)) throw Fail($"duplicate block mapping key: {Yaml.Encode(stringKey)}", keyStart);
                SkipSpaces();
                SkipTrailingComment();

                // Parse value.
                if (AcceptLineBreak())
                {
                    indent = SkipCommentLines();
                    var nested = indent > firstIndent
                        || (indent == firstIndent && !EndOfInput && Next == '-' && !PlainScalarPrefix);
                    mapping.Add(stringKey, nested ? ParseNode(ref indent) : null);
                }
                else
                {
                    mapping.Add(stringKey, ParseScalar(out _));
                    if (Accept(':')) throw Fail("unexpected block mapping key indicator: :", Index - 1);
                    SkipTrailingComment();
                    if (AcceptLineBreak()) indent = SkipCommentLines();
                }

                if (indent != firstIndent || EndOfInput) break;

                // Parse key.
                keyStart = Index;
                key = ParseScalar(out keyEnd);
                if (!Accept(':')) throw Fail("expected block mapping entry", keyStart);
            }

            if (indent <= firstIndent) m_PlainScalarContinuable = false; // Multi-line plain scalar must respect indentation.
            else if (!EndOfInput && !m_PlainScalarContinuable) throw Fail("bad indentation of block mapping entry");

            return mapping;
        }

        void CheckKeyLookaheadLimit(int keyStart)
        {
            const int codePointLimit = 1024;
            var keyIndicatorIndex = Index - 1;
            var codeUnitLength = keyIndicatorIndex - keyStart;
            if (codeUnitLength <= codePointLimit) return;
            if (CodePointLength(keyStart, keyIndicatorIndex) <= codePointLimit) return;
            throw Fail("YAML requires \":\" to appear at most 1024 Unicode code points beyond start of key", keyStart);
        }

        int CodePointLength(int start, int end)
        {
            var codePointLength = 0;
            for (var i = start; i < end; i += char.IsSurrogatePair(Input, i) ? 2 : 1)
            {
                codePointLength++;
            }
            return codePointLength;
        }

        bool AcceptLineBreak()
        {
            if (!Accept("\r\n", "\r", "\n")) return false;
            m_LineNumber++;
            m_LineStartIndex = Index;
            return true;
        }

        /// Consumes a non-break code unit. Throws YamlParseException on invalid/disallowed code point.
        /// Assumes: !EndOfInput && Next != '\n' && Next != '\r'
        void ExpectNonBreakCodeUnit(bool doubleQuotedScalar = false)
        {
            var c = Next;
            switch (c)
            {
                case '\t' when !doubleQuotedScalar: throw Fail("tab outside double-quoted scalar not allowed"); // Not allowed as "separation space" in YAML 1.1.
                case '\x85': case '\x2028': case '\x2029': throw Fail($"non-ASCII line break not allowed: {NextCodePoint}"); // Only treated as line break in YAML 1.1.
            }

            var valid = c == '\t'
                || (c >= ' ' && c <= '~') // Printable ASCII.
                || (c >= 0xa0 && c != k_ByteOrderMarker && c != 0xfffe && c != 0xffff); // Effectively U+00A0-U+D7FF, U+E000-U+FFFD, and U+10000-U+10FFFF. (YAML 1.1)
            if (!valid)
            {
                var allowedInYaml12 = doubleQuotedScalar && (c == '\t' || c >= ' ');
                throw Fail(allowedInYaml12
                    ? $"disallowed Unicode code point: {NextCodePoint}" // Only allowed in YAML 1.2.
                    : $"invalid Unicode code point: {NextCodePoint}");
            }

            Index++;
        }

        bool EndOfInput => Index == Input.Length;
        char Next => Input[Index];
        string NextCodePoint => $"U+{char.ConvertToUtf32(Input, Index):X4}";

        bool Accept(params string[] options)
        {
            foreach (var option in options)
            {
                var i = 0;
                while (i < option.Length && Index + i < Input.Length && Input[Index + i] == option[i]) i++;
                if (i != option.Length) continue;
                Index += i;
                return true;
            }
            return false;
        }

        bool Accept(params char[] options)
        {
            if (EndOfInput) return false;
            if (options.All(option => Next != option)) return false;
            Index++;
            return true;
        }

        YamlParseException Fail(string error) => Fail(error, Index);

        /// Assumes index is within current line (tracked by AcceptLineBreak).
        YamlParseException Fail(string error, int index)
        {
            if (index < m_LineStartIndex) throw new InvalidOperationException();
            var line = m_LineNumber;
            var column = 1 + CodePointLength(m_LineStartIndex, index);
            return new YamlParseException(error, line, column);
        }
    }
}
