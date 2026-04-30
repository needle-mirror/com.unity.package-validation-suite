using System.Collections.Generic;
using Mono.Cecil;

namespace Unity.APIComparison.Framework.Collectors
{
    internal struct CustomAttributeTypeNameEqualityComparer : IEqualityComparer<CustomAttribute>
    {
        public bool Equals(CustomAttribute x, CustomAttribute y)
        {
            return x.AttributeType.FullName == y.AttributeType.FullName;
        }

        public int GetHashCode(CustomAttribute obj)
        {
            return obj.AttributeType.FullName.GetHashCode();
        }
    }
}
