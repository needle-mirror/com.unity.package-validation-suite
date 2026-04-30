using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Unity.APIComparison.Framework.Changes;

namespace Unity.APIComparison.Framework.Collectors
{
    public enum MemberAccessibilityChangeKind
    {
        AccessorAccessibilityWidened,
        AccessorAccessibilityNarrowed,
        NoChange
    }
    
    public abstract class MemberChangesCollector<T> where T : class, IMemberDefinition
    {
        public virtual bool IsPotentialBreakingChange(T member)
        {
            return IsPublicAPI(member);
        }

        public abstract IEnumerable<T> MembersFor(TypeDefinition type);

        public abstract bool IsPublicAPI(T member);

        public abstract T FindMember(TypeDefinition typeDefinition, T member, out MemberKind missingMemberKind);
        protected virtual T FindOverridenMember(T added, TypeDefinition originalType) => null;
        
        protected abstract TypeReference ElementTypeOf(T member);

        protected abstract bool IsStatic(T member);

        protected abstract MemberAccessibilityChangeKind AccessibilityChangeKind(T current, T original);

        protected virtual CustomAttribute[] PseudoAttributesFrom(T item)
        {
            return Array.Empty<CustomAttribute>();
        }

        protected virtual bool TypeMemberSpecificChecks(T originalMember, T newMember)
        {
            return false;
        }

        protected virtual string GetDataForHash(T originalMember, T newMember)
        {
            string data = string.Empty;
            if (originalMember != null)
                data += originalMember.FullName;

            if (newMember != null)
                data += "-" + newMember.FullName;

            return data;
        }

        protected abstract IEqualityComparer<T> GetComparer();

        protected virtual void CheckForNewMembers(TypeDefinition originalType, TypeDefinition currentType)
        {
            var originalMembers = FilterCompilerGeneratedAndNonPublicMembers(MembersFor(originalType));
            var currentMembers = FilterCompilerGeneratedAndNonPublicMembers(MembersFor(currentType));

            var addedMembers = currentMembers.Except(originalMembers, GetComparer());
            foreach (var added in addedMembers)
            {
                if (!IsSideEffectOfOtherChanges(m_changes, added, originalType))
                {
                    var overriden = FindOverridenMember(added, originalType);
                    AddChange(originalType, new MemberAdded(added, overriden, GetDataForHash(default(T), added)));
                }
            }
        }

        public void ProcessMembers(TypeDefinition originalType, TypeDefinition currentType)
        {
            foreach (var member in MembersFor(originalType))
            {
                ProcessMember(member, currentType);
            }

            CheckForNewMembers(originalType, currentType);
        }

        protected void AddChange(TypeDefinition type, IAPIChange change)
        {
            if (!m_changes.TryGetValue(type.FullName, out var typeChange))
            {
                typeChange = new TypeChange(type);
                m_changes.Add(type.FullName, typeChange);
            }

            typeChange.Changes.Add(change);
        }

        protected MemberChangesCollector(Dictionary<string, IEntityChange> changes)
        {
            m_changes = changes;
        }

        protected MemberAccessibilityChangeKind AccessibilityChangeKind(ushort currentAttributes, ushort originalAttributes, ushort accessMask)
        {
            // members with higher access values are more accessible
            if ((currentAttributes & accessMask) > (originalAttributes & accessMask))
                return MemberAccessibilityChangeKind.AccessorAccessibilityWidened;
        
            if ((currentAttributes & accessMask) < (originalAttributes & accessMask))
                return MemberAccessibilityChangeKind.AccessorAccessibilityNarrowed;

            return MemberAccessibilityChangeKind.NoChange;
        }

        protected MemberAccessibilityChangeKind AccessibilityChangeKind(MethodDefinition m1, MethodDefinition originalM1, MethodDefinition m2, MethodDefinition originalM2)
        {
            var value1 = MemberAccessibilityChangeKind.NoChange;
            if (m1 != null && originalM1 != null)
                value1 = AccessibilityChangeKind((ushort) m1.Attributes, (ushort) originalM1.Attributes, (ushort) MethodAttributes.MemberAccessMask);

            var value2 = MemberAccessibilityChangeKind.NoChange;
            if (m2 != null && originalM2 != null)
                value2 = AccessibilityChangeKind((ushort) m2.Attributes, (ushort) originalM2.Attributes, (ushort) MethodAttributes.MemberAccessMask);

            if (value1 == value2)
                return value1;

            if (value1 == MemberAccessibilityChangeKind.AccessorAccessibilityNarrowed || value2 == MemberAccessibilityChangeKind.AccessorAccessibilityNarrowed)
                return MemberAccessibilityChangeKind.AccessorAccessibilityNarrowed;
        
            return MemberAccessibilityChangeKind.AccessorAccessibilityWidened;
        }

        protected IEnumerable<T> FilterCompilerGeneratedAndNonPublicMembers(IEnumerable<T> source)
        {
            return source.Where(m => IsPublicAPI(m) && !m.HasAttribute<CompilerGeneratedAttribute>());
        }

        protected bool IsSideEffectOfOtherChanges(IDictionary<string, IEntityChange> entityChanges, IMemberDefinition added, TypeDefinition originalType)
        {
            var memberChangedChecker = new MemberChangedChecker(added, originalType);
            foreach (var change in entityChanges.Values)
            {
                change.Accept(memberChangedChecker);
            }

            return memberChangedChecker.IsSideEffectOfOtherChanges;
        }

        private void ProcessMember(T member, TypeDefinition currentType)
        {
            if (!IsPotentialBreakingChange(member))
                return;

            MemberKind kind;
            var found = FindMember(currentType, member, out kind);
            if (found == null)
            {
                AddChange(member.DeclaringType, new MemberRemoved(member, kind));
                return;
            }

            if (TypeMemberSpecificChecks(member, found))
                return;

            if (CheckElementTypeChanges(member, found))
                return;

            if (CheckInstancenessChanges(member, found))
                return;

            CheckObsoleteAttribute(member, found);

            CheckAttributeChanges(member, found);

            CheckAccessibilityChanges(member, found);
        }

        private void CheckAttributeChanges(T member, T found)
        {
            var change = ValidatorMixin.CreateAttributeChangeIfApplicable(member, found, PseudoAttributesFrom(member), PseudoAttributesFrom(found));
            if (change != null)
                AddChange(member.DeclaringType, change);
        }

        private void CheckObsoleteAttribute(T member, T found)
        {
            var change = member.CreateObsoleteAttributeChangeIfApplicable(found);
            if (change != null)
                AddChange(member.DeclaringType, change);
        }

        private bool CheckInstancenessChanges(T originalMember, T newMember)
        {
            var isOriginalStatic = IsStatic(originalMember);
            var isNewStatic = IsStatic(newMember);

            if (isOriginalStatic == isNewStatic)
                return false;

            AddChange(originalMember.DeclaringType, new InstancenessChange(originalMember, newMember, isNewStatic ? InstancenessChangeKind.InstanceToStatic : InstancenessChangeKind.StaticToInstance));
            return true;
        }

        protected bool CheckElementTypeChanges(T member, T found)
        {
            var currentElementType = ElementTypeOf(found);
            var originalElementType = ElementTypeOf(member);

            if (TypeReferenceComparer.Instance.Equals(originalElementType, currentElementType))
                return false;

            IEntityChange originalTypeChanges;
            if (m_changes.TryGetValue(originalElementType.FullName, out originalTypeChanges))
            {
                var typeMovedChange = originalTypeChanges.Changes.OfType<TypeMoved>().SingleOrDefault(change => change.Current == currentElementType);
                if (typeMovedChange != null)
                    return true;
            }

            AddChange(member.DeclaringType, new ElementTypeChange(member, found));
            return true;
        }

        private void CheckAccessibilityChanges(T original, T current)
        {
            var changeKind = AccessibilityChangeKind(current, original);
            if (changeKind == MemberAccessibilityChangeKind.NoChange)
                return;

            if (changeKind == MemberAccessibilityChangeKind.AccessorAccessibilityNarrowed)
                AddChange(original.DeclaringType, new MemberAccessibilityChange(original, current));
            else
                AddChange(original.DeclaringType, new MemberAdded(current, string.Empty));
        }

        protected Dictionary<string, IEntityChange> m_changes;
    }
}
