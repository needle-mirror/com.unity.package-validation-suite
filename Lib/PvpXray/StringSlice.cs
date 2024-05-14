using System.Text;

namespace PvpXray
{
    // Utility primarily making up for lack of Span, though conveniences have been added now we're at it.
    struct StringSlice
    {
        public string String;
        public int Start;
        public int End;

        public int Length
        {
            get => End - Start;
            set => End = Start + value;
        }

        public bool Equals(StringSlice other) => Length == other.Length && string.Compare(String, Start, other.String, other.Start, Length) == 0;

        public bool TryIndexOf(char c, out int i)
        {
            i = String.IndexOf(c, Start, Length);
            if (i == -1) return false;
            i -= Start;
            return true;
        }

        public override string ToString() => String.Substring(Start, Length);
        public static implicit operator StringSlice(string str) => new StringSlice { String = str, End = str.Length };
    }

    static class StringSliceExtensions
    {
        public static StringBuilder Append(this StringBuilder sb, StringSlice ss) => sb.Append(ss.String, ss.Start, ss.Length);

        /// Note [start..end] to match new C# slice syntax, rather than traditional (start, length).
        public static StringSlice Slice(this string self, int start, int end)
            => new StringSlice { String = self, Start = start, End = end };

        public static bool TryIndexOf(this string[] array, StringSlice slice, out int i)
        {
            for (i = 0; i < array.Length; i++)
            {
                if (slice.Equals(array[i])) return true;
            }
            return false;
        }
    }
}
