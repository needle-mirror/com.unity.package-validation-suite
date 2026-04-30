using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Unity.APIComparison.Framework.Collectors
{
    public sealed class TypeReferenceComparer : IEqualityComparer<TypeReference>
    {
        public static readonly TypeReferenceComparer Instance = new TypeReferenceComparer();

        public bool Equals(TypeReference x, TypeReference y)
        {
            if (MatchesGenericParameter(y, x, out var result))
                return result;

            if (x.IsGenericInstance && y.IsGenericInstance)
            {
                if (!Instance.Equals(x.GetElementType(), y.GetElementType()))
                    return false;

                var xinst = (GenericInstanceType)x;
                var yinst = (GenericInstanceType)y;

                return xinst.GenericArguments.SequenceEqual(yinst.GenericArguments, Instance);
            }

            return string.Compare(x.FullName, y.FullName, StringComparison.Ordinal) == 0;
        }

        public int GetHashCode(TypeReference obj)
        {
            return 0;
        }

        bool MatchesGenericParameter(TypeReference left, TypeReference right, out bool result)
        {
            result = false;
            if (left is GenericParameter leftGenericParameter &&
                right is GenericParameter rightGenericParameter)
            {
                var leftOwner = (MemberReference)leftGenericParameter.Owner;
                var rightOwner = (MemberReference)rightGenericParameter.Owner;

                /*
                 * There's a really convoluted corner case that could not return false but we don't think
                 * it worth the trouble to handle it now.
                 *
                 * class Foo<T>
                 * {
                 *      class Bar
                 *      {
                 *          class Foo<T2>
                 *          {
                 *              void M(T _) {}
                 *          }
                 *      }
                 * }
                 *
                 * and in a new version dev swaps T <> T2
                 */
                result = leftOwner.Name == rightOwner.Name
                       && leftGenericParameter.Position == rightGenericParameter.Position;
                return true;
            }

            return false;
        }

        private TypeReferenceComparer() { }
    }
}
