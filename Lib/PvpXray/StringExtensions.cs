using System;

namespace PvpXray
{
    // This is a public API to facilitate code sharing. The nature of extension
    // methods is that even ADDING a public extension method to a namespace is
    // a breaking change. New extension methods must thus go in a new namespace.
    public static class StringExtensions
    {
        // We want string comparisons to be culturally invariant (so the program
        // doesn't unexpectedly change behavior just because OS settings changed),
        // and ordinal (so e.g. the strings "ß" and "ss" are not treated as equal).
        // However, it's very easy to forget that many String methods default to
        // non-ordinal (and culture sensitive) comparisons. The below extension
        // methods serve both as convenient shortcuts to ordinal comparisons and
        // as helpful reminders of this gotcha with the default methods. FWIW,
        // built-in '==', 'Equals', 'Contains', 'Replace', 'Split' are ordinal
        // by default, as are any method working with chars (instead of strings).
        // See also: https://confluence.unity3d.com/pages/viewpage.action?pageId=43573703 ('Locale / culture considerations')
        public static bool StartsWithOrdinal(this string str, string value) => str.StartsWith(value, StringComparison.Ordinal);
        public static bool EndsWithOrdinal(this string str, string value) => str.EndsWith(value, StringComparison.Ordinal);
        public static int IndexOfOrdinal(this string str, string value, int startIndex = 0) => str.IndexOf(value, startIndex, StringComparison.Ordinal);
        public static int IndexOfOrdinal(this string str, string value, int startIndex, int count) => str.IndexOf(value, startIndex, count, StringComparison.Ordinal);
        public static int LastIndexOfOrdinal(this string str, string value) => str.LastIndexOf(value, StringComparison.Ordinal);
        public static int LastIndexOfOrdinal(this string str, string value, int startIndex) => str.LastIndexOf(value, startIndex, StringComparison.Ordinal);
        public static int LastIndexOfOrdinal(this string str, string value, int startIndex, int count) => str.LastIndexOf(value, startIndex, count, StringComparison.Ordinal);

        internal static bool EqualsIgnoreCase(this string str, string value) => str.Equals(value, StringComparison.OrdinalIgnoreCase);
    }
}
