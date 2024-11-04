using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JsonObject = System.Collections.Generic.Dictionary<string, object>;

namespace PvpXray
{
    public enum Undefined { Undefined }

    public class Json
    {
        readonly object m_Value;
        readonly Json m_Parent;

        // The key of this JSON value in the parent object, or the index (as
        // a string) in the parent array, or null if this is the root value.
        public string Key { get; }
        public string PackageFilePath { get; }

        void BuildPath(StringBuilder sb)
        {
            if (m_Parent != null)
            {
                m_Parent.BuildPath(sb);
                SimpleJsonReader.AppendJsonPathElement(sb, Key, m_Parent.IsArray, isFirstElement: m_Parent.m_Parent == null);
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

        public string KindName => k_JsonTypeNames[Kind];

        T CheckKind<T>()
        {
            if (Kind != typeof(T))
            {
                var sb = new StringBuilder(80);
                AppendPathTo(sb);
                var i = sb.Length;
                var messageV1 = sb.Append($" was {KindName}, expected {k_JsonTypeNames[typeof(T)]}").ToString();
                var messageV2 = sb.Insert(i, ':').ToString();
                throw new SimpleJsonException(messageV1, messageV2) { PackageFilePath = PackageFilePath };
            }

            return (T)m_Value;
        }

        Json(object value, Json parent, string key, string packageFilePath)
        {
            m_Value = value;
            m_Parent = parent;
            Key = key;
            PackageFilePath = packageFilePath;
        }

        public Json(string json, string packageFilePath, bool permitInvalidJson = false)
            : this(SimpleJsonReader.Read(json, packageFilePath, permitInvalidJson), null, null, packageFilePath) { }
        internal Json(object root, string packageFilePath) : this(root, null, null, packageFilePath)
        {
            if (!k_JsonTypeNames.ContainsKey(Kind)) throw new ArgumentException("bad JSON root object type", nameof(root));
        }

        internal SimpleJsonException GetException(string message)
            => new SimpleJsonException($"{Path}: {message}", null) { PackageFilePath = PackageFilePath };

        /// Enumerate elements of a JSON array.
        public IEnumerable<Json> Elements
            => CheckKind<List<object>>().Select((elm, index) => new Json(elm, this, index.ToString(), PackageFilePath));

        public IEnumerable<Json> ElementsIfPresent
            => IfPresent?.Elements ?? Enumerable.Empty<Json>();

        /// Enumerate members of a JSON object.
        public IEnumerable<Json> Members
            => RawObject.Select(kv => new Json(kv.Value, this, kv.Key, PackageFilePath));

        public IEnumerable<Json> MembersIfPresent
            => IfPresent?.Members ?? Enumerable.Empty<Json>();

        public Json IfPresent => IsPresent ? this : null;
        public bool IsArray => Kind == typeof(List<object>);
        public bool IsBoolean => Kind == typeof(bool);
        public bool IsNumber => Kind == typeof(double);
        public bool IsObject => Kind == typeof(JsonObject);
        public bool IsPresent => !(m_Value is Undefined);
        public bool IsString => Kind == typeof(string);

        public Json IfObject => IsObject ? this : null;
        public Json IfString => IsString ? this : null;

        /// Returns the "location" of this element in the document as a jq-style path.
        public string Path
        {
            get
            {
                var sb = new StringBuilder();
                AppendPathTo(sb);
                return sb.ToString();
            }
        }

        internal void AppendPathTo(StringBuilder sb)
        {
            var i = sb.Length;
            BuildPath(sb);
            if (sb.Length == i) sb.Append('.');
        }

        public JsonObject RawObject => CheckKind<JsonObject>();

        public bool Boolean => CheckKind<bool>();
        public string String => CheckKind<string>();
        public double Number => CheckKind<double>();

        /// Checks that this is a JSON array of JSON strings (if present).
        /// Limitations of .NET type system prevents returning the proper
        /// generic type without allocating new list.
        public IReadOnlyList<object> ArrayOfStringIfPresent
        {
            get
            {
                if (!IsPresent) return Array.Empty<string>();
                var list = CheckKind<List<object>>();
                for (var i = 0; i < list.Count; i++)
                {
                    var elm = list[i];
                    if (!(elm is string))
                    {
                        new Json(elm, this, i.ToString(), PackageFilePath).CheckKind<string>();
                    }
                }

                return list;
            }
        }

        public Json this[string key] => new Json(RawObject.TryGetValue(key, out var result) ? result : Undefined.Undefined, this, key, PackageFilePath);
    }
}
