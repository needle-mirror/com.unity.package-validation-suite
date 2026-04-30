using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Unity.APIComparison.Framework.Changes;
using FieldAttributes = Mono.Cecil.FieldAttributes;

namespace Unity.APIComparison.Framework.Collectors
{
    public class FieldChangesCollector : MemberChangesCollector<FieldDefinition>
    {
        public FieldChangesCollector(Dictionary<string, IEntityChange> changes) : base(changes)
        {
        }

        protected override bool TypeMemberSpecificChecks(FieldDefinition originalMember, FieldDefinition newMember)
        {
            if (IsConstToReadonlyChange(originalMember, newMember) || IsConstToReadonlyChange(newMember, originalMember))
            {
                AddChange(originalMember.DeclaringType, new FieldConstnessChange(originalMember, newMember));
                return true;
            }

            if (originalMember.HasConstant && newMember.HasConstant && !Object.Equals(originalMember.Constant, newMember.Constant))
            {
                AddChange(
                    originalMember.DeclaringType,
                    new ConstantValueChanged(originalMember, newMember, originalMember.IsEnumMember() ? ConstantKind.EnumMember : ConstantKind.ConstField));
                return true;
            }

            return base.TypeMemberSpecificChecks(originalMember, newMember);
        }

        public override bool IsPotentialBreakingChange(FieldDefinition field)
        {
            return field.IsPublicAPI();
        }

        public override bool IsPublicAPI(FieldDefinition member)
        {
            return member.IsPublicAPI();
        }

        public override FieldDefinition FindMember(TypeDefinition typeDefinition, FieldDefinition member, out MemberKind missingMemberKind)
        {
            missingMemberKind = MemberKind.Field;
            return typeDefinition.Fields.SingleOrDefault(f => f.Name == member.Name);
        }

        protected override TypeReference ElementTypeOf(FieldDefinition member)
        {
            return member.FieldType;
        }

        protected override bool IsStatic(FieldDefinition member)
        {
            return member.IsStatic;
        }

        protected override MemberAccessibilityChangeKind AccessibilityChangeKind(FieldDefinition current, FieldDefinition original)
        {
            return AccessibilityChangeKind((ushort)current.Attributes, (ushort)original.Attributes, (ushort)FieldAttributes.FieldAccessMask);
        }

        protected override CustomAttribute[] PseudoAttributesFrom(FieldDefinition field)
        {
            if ((field.Attributes & FieldAttributes.NotSerialized) == FieldAttributes.NotSerialized)
                return new[] {new CustomAttribute(field.Module.ImportReference(typeof(NonSerializedAttribute).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new Type[0], null))) };

            return base.PseudoAttributesFrom(field);
        }

        public override IEnumerable<FieldDefinition> MembersFor(TypeDefinition type)
        {
            return type.Fields.Where(f => !f.IsEnumBackingField());
        }

        protected override IEqualityComparer<FieldDefinition> GetComparer()
        {
            return new MemberDefinitionEqualityComparer<FieldDefinition>();
        }

        private static bool IsConstToReadonlyChange(FieldDefinition lhs, FieldDefinition rhs)
        {
            return (lhs.IsLiteral && lhs.IsStatic) && rhs.IsInitOnly;
        }
    }
}
