using System.Collections.Generic;
using Mono.Cecil;

namespace Unity.APIComparison.Framework.Collectors
{
    internal sealed class TypeDefinitionEqualityComparer : IEqualityComparer<TypeDefinition>
    {
        public static readonly IEqualityComparer<TypeDefinition> Instance = new TypeDefinitionEqualityComparer();

        public bool Equals(TypeDefinition x, TypeDefinition y)
        {
            return TypeEqualityComparer.Instance.Equals(x, y);
        }

        public int GetHashCode(TypeDefinition obj)
        {
            return TypeEqualityComparer.Instance.GetHashCode(obj);
        }
    }

    internal sealed class TypeEqualityComparer : IEqualityComparer<TypeReference>
    {
        public static readonly IEqualityComparer<TypeReference> Instance = new TypeEqualityComparer();

        public bool Equals(TypeReference x, TypeReference y)
        {
            if (string.CompareOrdinal(x.Name, y.Name) != 0)
                return false;

            if (x.DeclaringType != null ^ y.DeclaringType != null)
                return false;

            if (x.DeclaringType != null && !Equals(x.DeclaringType, y.DeclaringType))
                return false;

            if (x.Namespace != null ^ y.Namespace != null)
                return false;

            if (x.Namespace != null && string.CompareOrdinal(x.Namespace, y.Namespace) != 0)
                return false;

            return true;
        }

        public int GetHashCode(TypeReference obj)
        {
            return obj.FullName.GetHashCode();
        }

        private TypeEqualityComparer()
        {
        }
    }
}
