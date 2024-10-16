using System;
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

        public bool Equals(StringSlice other) => Length == other.Length && string.CompareOrdinal(String, Start, other.String, other.Start, Length) == 0;
        public bool EqualsIgnoreCase(StringSlice other) => Length == other.Length && string.Compare(String, Start, other.String, other.Start, Length, StringComparison.OrdinalIgnoreCase) == 0;

        public StringSlice Slice(int start, int end) => new StringSlice { String = String, Start = Start + start, End = Start + end };

        public bool TryIndexOf(char c, out int i)
        {
            i = String.IndexOf(c, Start, Length);
            if (i == -1) return false;
            i -= Start;
            return true;
        }

        public override string ToString() => String.Substring(Start, Length);
        public static implicit operator StringSlice(string str) => new StringSlice { String = str, End = str.Length };

        public char this[int i] => String[i + Start];
    }

    static class StringSliceExtensions
    {
        public static StringBuilder Append(this StringBuilder sb, StringSlice ss) => sb.Append(ss.String, ss.Start, ss.Length);

        public static StringSlice Slice(this string self, int start)
            => new StringSlice { String = self, Start = start, End = self.Length };
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

        public static bool TryIndexOfIgnoreCase(this string[] array, StringSlice slice, out int i)
        {
            for (i = 0; i < array.Length; i++)
            {
                if (slice.EqualsIgnoreCase(array[i])) return true;
            }
            return false;
        }

        /// Try removing the given prefix from this string. Returns true if prefix was found.
        /// Sets `rest` to remaining string after removal (or entire string).
        public static bool TryStripPrefix(this string self, string prefix, out StringSlice rest)
        {
            if (self.StartsWithOrdinal(prefix))
            {
                rest = self.Slice(prefix.Length);
                return true;
            }
            rest = self;
            return false;
        }
    }
}
