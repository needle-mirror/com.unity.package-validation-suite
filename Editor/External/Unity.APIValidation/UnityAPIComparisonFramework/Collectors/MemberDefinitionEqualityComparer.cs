using System.Collections.Generic;
using Mono.Cecil;

namespace Unity.APIComparison.Framework.Collectors
{
    public class MemberDefinitionEqualityComparer<T> : IEqualityComparer<T> where T : IMemberDefinition
    {
        public bool Equals(T x, T y)
        {
            if (string.CompareOrdinal(x.Name, y.Name) != 0)
                return false;

            if (!TypeEqualityComparer.Instance.Equals(x.DeclaringType, y.DeclaringType))
                return false;

            if (!TypeEqualityComparer.Instance.Equals(x.ElementType(), y.ElementType()))
                return false;

            return true;
        }

        public int GetHashCode(T obj)
        {
            return 0;
        }
    }
}
