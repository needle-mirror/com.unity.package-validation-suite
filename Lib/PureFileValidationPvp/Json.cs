using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JsonObject = System.Collections.Generic.Dictionary<string, object>;

namespace PureFileValidationPvp
{
    public enum Undefined { Undefined }

    public class Json
    {
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
                var isArrayElement = m_Parent.Kind == typeof(List<object>);
                if (!isArrayElement || sb.Length == 0)
                {
                    sb.Append('.');
                }
                if (isArrayElement)
                {
                    sb.Append('[');
                }
                sb.Append(Key);
                if (isArrayElement)
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
                throw new JsonException($"{Path} was {k_JsonTypeNames[Kind]}, expected {k_JsonTypeNames[typeof(T)]}");
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
                    throw new JsonException($"illegal key '{key}' in object {Path}");
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
        public bool IsPresent => !(m_Value is Undefined);

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
    }
}
