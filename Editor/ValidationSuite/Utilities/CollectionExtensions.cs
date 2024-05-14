using System.Collections.Generic;

namespace UnityEditor.PackageManager.ValidationSuite
{
    static class CollectionExtensions
    {
#if !(NET472_OR_GREATER || NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER)
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer = null)
            => new HashSet<T>(source, comparer);
#endif
    }
}
