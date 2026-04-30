using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Unity.APIComparison.Framework.Changes;

namespace Unity.APIComparison.Framework.Collectors
{
    public class PropertyChangesCollector : MemberChangesCollector<PropertyDefinition>
    {
        public PropertyChangesCollector(Dictionary<string, IEntityChange> changes) : base(changes)
        {
        }

        protected override PropertyDefinition FindOverridenMember(PropertyDefinition added, TypeDefinition originalType)
        {
            TypeDefinition overridenInType = null;
            if (added.GetMethod != null && added.GetMethod.IsVirtual())
            {
                var overriden = added.GetMethod.FindOverridenMethod();
                overridenInType = overriden?.DeclaringType;
            }
            else if (added.SetMethod != null && added.SetMethod.IsVirtual())
            {
                var overriden = added.SetMethod.FindOverridenMethod();
                overridenInType = overriden?.DeclaringType;
            }

            return overridenInType == null 
                ? null 
                : overridenInType.Properties.SingleOrDefault(c => c.Name == added.Name);
        }

        public override bool IsPublicAPI(PropertyDefinition member)
        {
            return member.IsPublicAPI();
        }

        public override PropertyDefinition FindMember(TypeDefinition typeDefinition, PropertyDefinition original, out MemberKind missingMemberKind)
        {
            missingMemberKind = MemberKind.None;

            var found = typeDefinition.Properties.SingleOrDefault(candidate =>
                {
                    if (candidate.Name != original.Name)
                        return false;

                    // https://trello.com/c/hXuOOPxs
                    if (candidate.Parameters.Count != original.Parameters.Count)
                        return false;

                    for (int i = 0; i < candidate.Parameters.Count; i++)
                        if (candidate.Parameters[i].ParameterType.FullName != original.Parameters[i].ParameterType.FullName)
                            return false;

                    return true;
                });

            if (found == null)
            {
                missingMemberKind = MemberKind.Property;
                return null;
            }

            // A property is considered to be 'found' if the original accessor (get/setter) was implemented and now it is not anymore.
            if (original.GetMethod != null && original.GetMethod.IsPublicAPI() && found.GetMethod == null)
                missingMemberKind = MemberKind.PropertyGetter;

            if (original.SetMethod != null && original.SetMethod.IsPublicAPI() && found.SetMethod == null)
                missingMemberKind = missingMemberKind == MemberKind.PropertyGetter ? MemberKind.Property : MemberKind.PropertySetter;

            return missingMemberKind == MemberKind.None ? found : null;
        }

        public override IEnumerable<PropertyDefinition> MembersFor(TypeDefinition type)
        {
            return type.Properties;
        }

        protected override bool TypeMemberSpecificChecks(PropertyDefinition originalMember, PropertyDefinition currentMember)
        {
            var getterVirtualChange = originalMember.GetMethod.VirtualnessChanged(currentMember.GetMethod);
            var setterVirtualChange = originalMember.SetMethod.VirtualnessChanged(currentMember.SetMethod);
            if (getterVirtualChange == ModifierChangeKind.Removed || setterVirtualChange == ModifierChangeKind.Removed)
            {
                AddChange(originalMember.DeclaringType, new PropertyVirtualnessChange(originalMember, currentMember));
            }

            var getterAbstractChange = originalMember.GetMethod.AbstractnessChanged(currentMember.GetMethod);
            var setterAbstractChange = originalMember.SetMethod.AbstractnessChanged(currentMember.SetMethod);
            if (getterAbstractChange == ModifierChangeKind.Added || setterAbstractChange == ModifierChangeKind.Added)
            {
                AddChange(originalMember.DeclaringType, new PropertyAbstractnessChange(originalMember, currentMember));
            }

            return false;
        }

        protected override TypeReference ElementTypeOf(PropertyDefinition member)
        {
            return member.PropertyType;
        }

        protected override bool IsStatic(PropertyDefinition member)
        {
            return member.GetMethod != null && member.GetMethod.IsStatic || member.SetMethod != null && member.SetMethod.IsStatic;
        }

        protected override MemberAccessibilityChangeKind AccessibilityChangeKind(PropertyDefinition current, PropertyDefinition original)
        {
            return AccessibilityChangeKind(current.GetMethod, original.GetMethod, current.SetMethod, original.SetMethod);
        }

        protected override IEqualityComparer<PropertyDefinition> GetComparer()
        {
            return PropertyDefinitionEqualityComparer.Instance;
        }

        protected override string GetDataForHash(PropertyDefinition originalMember, PropertyDefinition newMember)
        {
            string data = string.Empty;
            if (originalMember != null)
                data += originalMember.FullName + "-" + (originalMember.GetMethod != null ? "get" : "") + (originalMember.SetMethod != null ? "set" : "");

            if (newMember != null)
                data += "-" + newMember.FullName + "-" + (newMember.GetMethod != null ? "get" : "") + (newMember.SetMethod != null ? "set" : "");

            return data;
        }

        private class PropertyDefinitionEqualityComparer : IEqualityComparer<PropertyDefinition>
        {
            public static PropertyDefinitionEqualityComparer Instance = new PropertyDefinitionEqualityComparer();

            public bool Equals(PropertyDefinition x, PropertyDefinition y)
            {
                var hasSameName = string.CompareOrdinal(x.Name, y.Name) == 0;
                if (!hasSameName)
                    return false;

                if (!TypeEqualityComparer.Instance.Equals(x.DeclaringType, y.DeclaringType))
                    return false;

                if (!TypeEqualityComparer.Instance.Equals(x.PropertyType, y.PropertyType))
                    return false;

                var xGetMethod = x.GetMethod;
                var yGetMethod = y.GetMethod;

                if (xGetMethod != null && yGetMethod == null && xGetMethod.IsPublicAPI())
                    return false;

                if (yGetMethod != null && xGetMethod == null && yGetMethod.IsPublicAPI())
                    return false;

                var xSetMethod = x.SetMethod;
                var ySetMethod = y.SetMethod;
                if (xSetMethod != null && ySetMethod == null && xSetMethod.IsPublicAPI())
                    return false;

                if (ySetMethod != null && xSetMethod == null && ySetMethod.IsPublicAPI())
                    return false;

                return true;
            }

            public int GetHashCode(PropertyDefinition obj)
            {
                return 0;
            }
        }
    }
}
