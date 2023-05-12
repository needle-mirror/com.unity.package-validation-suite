using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JsonObject = System.Collections.Generic.Dictionary<string, object>;

namespace PvpXray
{
    public enum Undefined { Undefined }

    public class Json
    {
        /// Object property name simple enough to use in Path unquoted.
        static readonly Regex k_SimpleKey = new Regex("^[_a-zA-Z][_a-zA-Z0-9]*$");

        readonly object m_Value;
        readonly Json m_Parent;

        // The key of this JSON value in the parent object, or the index (as
        // a string) in the parent array, or null if this is the root value.
        public string Key { get; }

        void BuildPath(StringBuilder sb)
        {
            if (m_Parent != null)
            {
                m_Parent.BuildPath(sb);
                var isIndexOperation = m_Parent.IsArray || !k_SimpleKey.IsMatch(Key);
                if (!isIndexOperation || sb.Length == 0)
                {
                    sb.Append('.');
                }
                if (isIndexOperation)
                {
                    sb.Append('[');
                }

                if (isIndexOperation && !m_Parent.IsArray) // complex object key
                {
                    EncodeJsonString(Key, sb);
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

        static readonly Dictionary<Type, string> k_JsonTypeNames = new Dictionary<Type, string>()
        {
            [typeof(Undefined)] = "undefined",
            [typeof(void)] = "null",
            [typeof(bool)] = "boolean",
            [typeof(string)] = "string",
            [typeof(double)] = "number",
            [typeof(JsonObject)] = "object",
            [typeof(List<object>)] = "array",
        };

        Type Kind => m_Value == null ? typeof(void) : m_Value.GetType();

        T CheckKind<T>()
        {
            if (Kind != typeof(T))
            {
                throw new SimpleJsonException($"{Path} was {k_JsonTypeNames[Kind]}, expected {k_JsonTypeNames[typeof(T)]}");
            }

            return (T)m_Value;
        }

        Json(object value, Json parent, string key)
        {
            m_Value = value;
            m_Parent = parent;
            Key = key;
        }

        public Json(string json) : this(SimpleJsonReader.Read(json), null, null) { }

        public void CheckKeys(IReadOnlyCollection<string> allowedKeys)
        {
            foreach (var key in RawObject.Keys)
            {
                if (!allowedKeys.Contains(key))
                {
                    throw new SimpleJsonException($"illegal key '{key}' in object {Path}");
                }
            }
        }

        // Enumerate elements of a JSON array.
        public IEnumerable<Json> Elements
            => CheckKind<List<object>>().Select((elm, index) => new Json(elm, this, index.ToString()));

        public IEnumerable<Json> ElementsIfPresent
            => IfPresent?.Elements ?? Enumerable.Empty<Json>();

        // Enumerate members of a JSON object.
        public IEnumerable<Json> Members
            => RawObject.Select(kv => new Json(kv.Value, this, kv.Key));

        public IEnumerable<Json> MembersIfPresent
            => IfPresent?.Members ?? Enumerable.Empty<Json>();

        public Json IfPresent => IsPresent ? this : null;
        public bool IsArray => Kind == typeof(List<object>);
        public bool IsBoolean => Kind == typeof(bool);
        public bool IsNumber => Kind == typeof(double);
        public bool IsObject => Kind == typeof(JsonObject);
        public bool IsPresent => !(m_Value is Undefined);
        public bool IsString => Kind == typeof(string);

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

        JsonObject RawObject => CheckKind<JsonObject>();

        public bool Boolean => CheckKind<bool>();
        public string String => CheckKind<string>();

        public Json this[string key] => new Json(RawObject.TryGetValue(key, out var result) ? result : Undefined.Undefined, this, key);

        static void EncodeJsonString(string str, StringBuilder sb)
        {
            // Taken from SimpleJsonWriter.
            sb.Append('"');
            for (var i = 0; i < str.Length; ++i)
            {
                var c = str[i];
                if (c < ' ' || c == '"' || c == '\\')
                {
                    sb.Append('\\');
                    var j = "\"\\\n\r\t\b\f".IndexOf(c);
                    if (j >= 0)
                        sb.Append("\"\\nrtbf"[j]);
                    else
                        sb.AppendFormat("u{0:X4}", (uint)c);
                }
                else
                    sb.Append(c);
            }
            sb.Append('"');
        }
    }
}
