using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace Unity.APIComparison.Framework.Collectors
{
    public sealed class MethodDefinitionEqualityComparer : IEqualityComparer<MethodDefinition>
    {
        public static readonly IEqualityComparer<MethodDefinition> Instance = new MethodDefinitionEqualityComparer();

        public bool Equals(MethodDefinition x, MethodDefinition y)
        {
            if (x.DeclaringType.IsNested != y.DeclaringType.IsNested)
                return false;

            var sameTypeName = string.Compare(x.DeclaringType.Name, y.DeclaringType.Name, StringComparison.Ordinal) == 0;
            if (!sameTypeName)
                return false;

            var sameNamespace = string.Compare(x.DeclaringType.Namespace, y.DeclaringType.Namespace, StringComparison.Ordinal) == 0;
            if (!sameNamespace)
                return false;

            return MethodDefinitionSignatureComparer.Instance.Equals(x, y);
        }

        public int GetHashCode(MethodDefinition obj)
        {
            return 0;
        }

        private MethodDefinitionEqualityComparer() { }
    }

    public sealed class MethodDefinitionSignatureComparer : IEqualityComparer<MethodDefinition>
    {
        public static IEqualityComparer<MethodDefinition> Instance = new MethodDefinitionSignatureComparer();

        public bool Equals(MethodDefinition x, MethodDefinition y)
        {
            if (x.HasGenericParameters ^ y.HasGenericParameters)
                return false;

            if (!ReturnTypesMatches(x, y))
                return false;

            var nameMatches = string.Compare(x.Name, y.Name, StringComparison.Ordinal) == 0;
            if (!nameMatches || x.Parameters.Count != y.Parameters.Count)
                return false;

            for (int i = 0; i < x.Parameters.Count; i++)
            {
                if (TypeInOverridenMethodMatches(x,y, y.Parameters[i].ParameterType, x.Parameters[i].ParameterType))
                    continue;

                if (!TypeReferenceComparer.Instance.Equals(x.Parameters[i].ParameterType, y.Parameters[i].ParameterType))
                    return false;
            }

            return true;
        }

        private bool ReturnTypesMatches(MethodDefinition x, MethodDefinition y)
        {
            return TypeReferenceComparer.Instance.Equals(x.ReturnType, y.ReturnType) 
                   || TypeInOverridenMethodMatches(x,y, x.ReturnType, y.ReturnType);
        }

        public int GetHashCode(MethodDefinition obj)
        {
            return 0;
        }
        
        
        /*
         * Checks whether a type represents a return type/parameter in an overload method.
         *
         * For instance, given the *t* parameter in Bar() method from both Foo<T> / FooBar
         * this methods returns *true*
         * 
         * class Foo<T>            { virtual void Bar(T t) {}    }  
         * class FooBar : Foo<int> { override void Bar(int t) {} }  
         */
        private bool TypeInOverridenMethodMatches(MethodDefinition leftMethod, MethodDefinition rightMethod, TypeReference leftType, TypeReference rightType)
        {
            // a type cannot declare a virtual method and override it...
            if (TypeReferenceComparer.Instance.Equals(leftMethod.DeclaringType,rightMethod.DeclaringType))
                return false;
            
            if (!leftMethod.IsVirtual && !rightMethod.IsVirtual && !leftMethod.IsAbstract && !rightMethod.IsAbstract)
                return false; // it cannot be a method override if none of the members are virtual/abstract

            if (leftType.GetElementType().IsGenericParameter || rightType.GetElementType().IsGenericParameter)
                return true;

            return false;
        }
    }
}
