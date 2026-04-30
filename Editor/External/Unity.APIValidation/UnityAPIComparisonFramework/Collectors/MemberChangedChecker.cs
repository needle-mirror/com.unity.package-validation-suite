using System.Linq;
using Mono.Cecil;
using Unity.APIComparison.Framework.Changes;

namespace Unity.APIComparison.Framework.Collectors
{
    internal class MemberChangedChecker : APIChangeVisitorBase, IEntityChangeVisitor
    {
        private IMemberDefinition m_added;
        private TypeDefinition m_originalType;

        public MemberChangedChecker(IMemberDefinition added, TypeDefinition originalType)
        {
            m_added = added;
            m_originalType = originalType;
        }

        public bool IsSideEffectOfOtherChanges { get; private set; }

        public override void Visit(EntityTypeChanged change)
        {
            // do not report member changes as new members....
            if (change.Current.FullName == m_added.FullName)
            {
                IsSideEffectOfOtherChanges = true;
                return;
            }

            // do not report default ctor as new members when type changes from struct -> class
            if (IsDefaultCtorOnTypeChangedFromStructToClass(change))
            {
                IsSideEffectOfOtherChanges = true;
                return;
            }

            if (m_added.IsEnumBackingField() && m_added.DeclaringType == change.Current && !change.Original.IsEnum())
                IsSideEffectOfOtherChanges = true;
        }

        public override void Visit(TypeMoved change)
        {
            // do not report members on moved types as new members...
            if (IsDefaultCtorOnType(m_added, change.CurrentEntity))
                IsSideEffectOfOtherChanges = true;
            else if (IsEnumMemberOf(change))
                IsSideEffectOfOtherChanges = true;
            else if (IsElementTypeOrParameterTypeReferenceChange(change))
                IsSideEffectOfOtherChanges = true;
        }

        public override void Visit(InstancenessChange change)
        {
            if (change.Current != m_added.DeclaringType)
                return;

            var original = change.Original as TypeDefinition;
            if (original == null)
                return;

            var current = (TypeDefinition)change.Current;
            if (original.IsAbstract && !current.IsAbstract && IsDefaultCtorOnType(m_added, current))
                IsSideEffectOfOtherChanges = true;
        }

        public override void Visit(ElementTypeChange change)
        {
            if (change.Current.FullName == m_added.FullName)
            {
                IsSideEffectOfOtherChanges = true;
                return;
            }

            CheckAddedMemberAgainstPropertyGetterSetter(change.Current);
        }

        public override void Visit(MethodParameterCountChange change)
        {
            if (change.Current.FullName == m_added.FullName)
                IsSideEffectOfOtherChanges = true;
        }

        public override void Visit(MethodParameterTypeChange change)
        {
            if (change.Current.FullName == m_added.FullName)
                IsSideEffectOfOtherChanges = true;
        }

        public override void Visit(PropertyAbstractnessChange change)
        {
            if (change.CurrentEntity.GetMethod.FullName == m_added.FullName || change.CurrentEntity.SetMethod.FullName == m_added.FullName)
                IsSideEffectOfOtherChanges = true;
        }

        public override void Visit(ParameterReferencenessChange change)
        {
            if (change.Current.FullName == m_added.FullName)
                IsSideEffectOfOtherChanges = true;
        }

        public override void Visit(MemberRemoved change)
        {
            // property getter/setter removed
             if (change.Original.FullName == m_added.FullName && change.Original.Kind() == m_added.Kind())
                 IsSideEffectOfOtherChanges = true;
        }

        public override void Visit(MemberAdded change)
        {
            CheckAddedMemberAgainstPropertyGetterSetter(change.Current);
        }

        public override void Visit(TypeRemoved change)
        {
            IsSideEffectOfOtherChanges = false;
        }

        public void Visit(TypeChange typeChange)
        {
            foreach (var change in typeChange.Changes)
            {
                change.Accept(this);
            }
        }

        private bool IsElementTypeOrParameterTypeReferenceChange(TypeMoved change)
        {
            var addedField = m_added as FieldDefinition;
            if (addedField != null && addedField.FieldType == change.CurrentEntity)
            {
                var fieldWithMovedElementType = m_originalType.Fields.SingleOrDefault(f => f.Name == addedField.Name);
                if (fieldWithMovedElementType != null)
                    return true;
            }

            var addedProperty = m_added as PropertyDefinition;
            if (addedProperty != null && addedProperty.PropertyType == change.CurrentEntity)
            {
                var propertyWithMovedElementType = m_originalType.Properties.SingleOrDefault(p => p.Name == addedProperty.Name);
                if (propertyWithMovedElementType != null)
                    return true;
            }

            var addedMethod = m_added as MethodDefinition;
            if (addedMethod == null)
                return false;

            var candidates = m_originalType.Methods.Where(m => m.Name == addedMethod.Name && m.Parameters.Count == addedMethod.Parameters.Count);
            foreach (var method in candidates)
            {
                if (addedMethod.ReturnType == change.CurrentEntity)
                    return true;

                if (!TypeReferenceComparer.Instance.Equals(method.ReturnType, addedMethod.ReturnType))
                    return false;

                var foundMovedParameterType = false;
                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    if (TypeReferenceComparer.Instance.Equals(method.Parameters[i].ParameterType, addedMethod.Parameters[i].ParameterType))
                        continue;

                    if (foundMovedParameterType)
                        return false;

                    if (method.Parameters[i].ParameterType == change.OriginalEntity && addedMethod.Parameters[i].ParameterType == change.CurrentEntity)
                        foundMovedParameterType = true;
                }

                if (foundMovedParameterType)
                    return true;
            }
            return false;
        }

        private bool IsEnumMemberOf(TypeMoved change)
        {
            if (!change.Original.IsEnum())
                return false;

            return change.CurrentEntity == m_added.DeclaringType;
        }

        private bool IsDefaultCtorOnType(IMemberDefinition addedMember, IMetadataTokenProvider candidateDeclaringType)
        {
            if (candidateDeclaringType != addedMember.DeclaringType)
                return false;

            var method = addedMember as MethodDefinition;
            if (method == null)
                return false;

            return method.LooksLikeDefaultCtor(method.DeclaringType);
        }

        private bool IsDefaultCtorOnTypeChangedFromStructToClass(EntityTypeChanged change)
        {
            var originalTypeDef = change.Original as TypeDefinition;
            if (originalTypeDef == null)
                return false;

            return originalTypeDef.IsValueType && IsDefaultCtorOnType(m_added, change.CurrentEntity);
        }

        private void CheckAddedMemberAgainstPropertyGetterSetter(IMemberDefinition currentMember)
        {
            // property getter/setter added
            var property = currentMember as PropertyDefinition;
            if (property == null)
                return;

            if (property.GetMethod == m_added || property.SetMethod == m_added)
                IsSideEffectOfOtherChanges = true;
        }
    }
}
