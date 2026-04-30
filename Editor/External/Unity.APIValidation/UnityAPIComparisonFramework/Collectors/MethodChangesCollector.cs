using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Unity.APIComparison.Framework.Changes;

namespace Unity.APIComparison.Framework.Collectors
{
    public class MethodChangesCollector : MemberChangesCollector<MethodDefinition>
    {
        public MethodChangesCollector(Dictionary<string, IEntityChange> changes) : base(changes)
        {
        }

        protected override void CheckForNewMembers(TypeDefinition originalType, TypeDefinition currentType)
        {
            CheckForNewMethods(m_changes, originalType, currentType);
            CheckForFalsePositiveCtorRemovals(m_changes, originalType, currentType);
        }

        public override IEnumerable<MethodDefinition> MembersFor(TypeDefinition type)
        {
            return type.Methods;
        }

        public override bool IsPublicAPI(MethodDefinition member)
        {
            return member.IsPublicAPI();
        }

        public override MethodDefinition FindMember(TypeDefinition typeDefinition, MethodDefinition member, out MemberKind missingMemberKind)
        {
            missingMemberKind = MemberKind.Method;

            return typeDefinition.Methods.SingleOrDefault(candidate => candidate.Name == member.Name
                && TypeReferenceComparer.Instance.Equals(candidate.ReturnType, member.ReturnType)
                && candidate.GenericParameters.Count == member.GenericParameters.Count
                && SameParameters(candidate, member));
        }

        protected override TypeReference ElementTypeOf(MethodDefinition member)
        {
            return member.ReturnType;
        }

        protected override bool IsStatic(MethodDefinition member)
        {
            return member.IsStatic;
        }

        protected override MemberAccessibilityChangeKind AccessibilityChangeKind(MethodDefinition current, MethodDefinition original)
        {
            return AccessibilityChangeKind((ushort)current.Attributes, (ushort)original.Attributes, (ushort)MethodAttributes.MemberAccessMask);
        }

        protected override IEqualityComparer<MethodDefinition> GetComparer()
        {
            return MethodDefinitionEqualityComparer.Instance;
        }

        public override bool IsPotentialBreakingChange(MethodDefinition member)
        {
            if (member.IsGetter || member.IsSetter || member.IsAddOn || member.IsRemoveOn || member.IsOther)
                return false;

            return base.IsPotentialBreakingChange(member);
        }

        protected override bool TypeMemberSpecificChecks(MethodDefinition originalMember, MethodDefinition newMember)
        {
            CheckParameterCount(originalMember, newMember);
            CheckParameterTypes(originalMember, newMember);
            CheckParameterReferenceness(originalMember, newMember);
            CheckParameterDefaultness(originalMember, newMember);
            CheckVirtualness(originalMember, newMember);
            CheckAbstractness(originalMember, newMember);

            return false;
        }

        private bool SameParameters(MethodDefinition lhs, MethodDefinition rhs)
        {
            if (lhs.Parameters.Count != rhs.Parameters.Count)
                return false;

            var lhsParameters = lhs.Parameters;
            var rhsParameters = rhs.Parameters;

            for (int i = 0; i < lhs.Parameters.Count; i++)
            {
                if (!TypeReferenceComparer.Instance.Equals(lhsParameters[i].ParameterType, rhsParameters[i].ParameterType))
                    return false;
            }

            return true;
        }

        private void CheckAbstractness(MethodDefinition originalMember, MethodDefinition currentMember)
        {
            if (originalMember.IsAbstract ^ currentMember.IsAbstract)
            {
                AddChange(originalMember.DeclaringType, new MethodAbstractnessChange(originalMember, currentMember));
            }
        }

        private void CheckParameterDefaultness(MethodDefinition originalMember, MethodDefinition currentMember)
        {
            var mismatches = new List<ParameterDefinition>();

            for (int i = 0; i < originalMember.Parameters.Count && i < currentMember.Parameters.Count; i++)
            {
                var originalParam = originalMember.Parameters[i];
                var currentParam = currentMember.Parameters[i];

                if (originalParam.HasDefault && !currentParam.HasDefault)
                    mismatches.Add(originalParam);
            }

            if (mismatches.Count == 0)
                return;

            AddChange(originalMember.DeclaringType, new ParameterDefaultnessChange(originalMember, currentMember, mismatches));
        }

        private void CheckParameterReferenceness(MethodDefinition originalMember, MethodDefinition currentMember)
        {
            var mismatches = new List<ParameterMismatch>();

            for (int i = 0; i < originalMember.Parameters.Count && i < currentMember.Parameters.Count; i++)
            {
                var originalParam = originalMember.Parameters[i];
                var currentParam = currentMember.Parameters[i];

                if (!TypeReferenceComparer.Instance.Equals(originalParam.ParameterType.GetElementType(), currentParam.ParameterType.GetElementType()))
                    continue;

                if (originalParam.ParameterType.IsByReference ^ currentParam.ParameterType.IsByReference)
                    mismatches.Add(new ParameterMismatch(originalParam, currentParam));
                else if (originalParam.IsOut ^ currentParam.IsOut)
                    mismatches.Add(new ParameterMismatch(originalParam, currentParam));
            }

            if (mismatches.Count > 0)
                AddChange(originalMember.DeclaringType, new ParameterReferencenessChange(originalMember, currentMember, mismatches));
        }

        private void CheckVirtualness(MethodDefinition originalMember, MethodDefinition newMember)
        {
            var status = originalMember.VirtualnessChanged(newMember);
            if (status != ModifierChangeKind.NoChange)
            {
                AddChange(originalMember.DeclaringType, new MethodVirtualnessChange(originalMember, newMember));
            }
        }

        private void CheckParameterTypes(MethodDefinition originalMember, MethodDefinition newMember)
        {
            var mismatches = new List<ParameterDefinition>();
            for (int i = 0; i < originalMember.Parameters.Count && i < newMember.Parameters.Count; i++)
            {
                var originalParam = originalMember.Parameters[i];
                var newParam = newMember.Parameters[i];
                
                if (TypeReferenceComparer.Instance.Equals(originalParam.ParameterType, newParam.ParameterType))
                    continue;

                var hasOtherDetectableChanges =
                    (originalParam.ParameterType.IsByReference != newParam.ParameterType.IsByReference
                     || originalParam.IsOut != newParam.IsOut
                     || originalParam.HasDefault != newParam.HasDefault);

                if (hasOtherDetectableChanges && TypeReferenceComparer.Instance.Equals(originalParam.ParameterType.GetElementType(), newParam.ParameterType.GetElementType()))
                    continue;

                mismatches.Add(originalParam);
            }

            if (mismatches.Count > 0)
                AddChange(originalMember.DeclaringType, new MethodParameterTypeChange(originalMember, newMember, mismatches));
        }

        private void CheckParameterCount(MethodDefinition originalMember, MethodDefinition newMember)
        {
            if (originalMember.Parameters.Count != newMember.Parameters.Count)
                AddChange(originalMember.DeclaringType, new MethodParameterCountChange(originalMember, newMember));
        }

        private void CheckForFalsePositiveCtorRemovals(Dictionary<string, IEntityChange> changes, TypeDefinition originalType, TypeDefinition currentType)
        {
            if (!changes.TryGetValue(originalType.FullName, out var typeChanges))
                return;

            var falsePositives = new List<APIChangeBase<IMemberDefinition>>();
            typeChanges.Accept(new CtorFalsePositiveCollector(falsePositives));
            foreach (var removed in falsePositives)
            {
                typeChanges.Changes.Remove(removed);
            }
        }

        private void CheckForNewMethods(IDictionary<string, IEntityChange> changes, TypeDefinition originalType, TypeDefinition currentType)
        {
            var originalMembers = originalType.Methods.Where(m => (m.IsPublic || m.IsFamily) && !m.IsEventMethod() && !m.IsGetter && !m.IsSetter);
            var currentMembers = currentType.Methods.Where(m => (m.IsPublic || m.IsFamily) && !m.IsEventMethod() && !m.IsGetter && !m.IsSetter);

            var addedMembers = currentMembers.Except(originalMembers, MethodDefinitionEqualityComparer.Instance);
            foreach (var added in addedMembers.GroupBy(m => m.Name))
            {
                IEnumerable<MethodDefinition> actualMethodsAdded = added;
                if (changes.TryGetValue(originalType.FullName, out var entityChange))
                    actualMethodsAdded = BindAddedAndRemovedMethodsToSpecificChange(added, entityChange);

                foreach (var method in actualMethodsAdded)
                {
                    if (!IsSideEffectOfOtherChanges(changes, method, originalType))
                    {
                        var overriden= method.FindOverridenMethod();
                        AddChange(originalType, new MemberAdded(method, overriden, GetDataForHash(null, method)));
                    }
                }
            }
        }

        private IEnumerable<MemberRemoved> MemberRemovalsFor(string memberName, IEntityChange typeChanges)
        {
            var collector = new MemberRemovalCollector(memberName, MemberKind.Method);
            typeChanges.Accept(collector);

            return collector.RemovedMembers;
        }

        /*
        * This method tries to match added / removed methods combining them in more specific changes.
        * For instance given the original version of class Foo:
        *
        * class Foo { void M(int i) {} }
        *
        * And a new version of it:
        *
        * class Foo { void M(string s) {} }
        *
        * this method combines what would otherwise be handled as a method removal + a method being added
        *
        *  - void Foo::M(System.Int32)
        *  + void Foo::M(System.String)
        *
        * as a "parameter type change" :
        *  void Foo::M(System.Int32) -> void Foo::M(System.String)
        */
        private IEnumerable<MethodDefinition> BindAddedAndRemovedMethodsToSpecificChange(IGrouping<string, MethodDefinition> addedGroup, IEntityChange changes)
        {
            var removedMembers = MemberRemovalsFor(addedGroup.Key, changes);
            var removedStack = new Stack<MemberRemoved>(removedMembers);
            var mappedChanges = new List<MappedChange>();
            var notMapped = new List<MethodDefinition>(addedGroup);

            while (removedStack.Count > 0)
            {
                var removedMember = removedStack.Pop();

                var best = new MappedChange();
                foreach (var added in addedGroup.OrderByDescending(m => m.Parameters.Count))
                {
                    var methodChanges = new Dictionary<string, IEntityChange>();
                    var collector = new MethodChangesCollector(methodChanges);

                    collector.TypeMemberSpecificChecks((MethodDefinition)removedMember.Original, added);
                    collector.CheckElementTypeChanges((MethodDefinition)removedMember.Original, added);

                    if (methodChanges.Count == 1)
                    {
                        var similarityMapper = new MethodChangesRankVisitor();
                        methodChanges.Values.First().Accept(similarityMapper);
                        if (similarityMapper.Rank < best.similarityIndex)
                        {
                            best = new MappedChange(similarityMapper.Rank, similarityMapper.Change, added, removedMember, methodChanges.Values.First().Changes);
                        }
                    }
                }

                var alreadyUsed = mappedChanges.Find(m => m.added == best.added);
                if (alreadyUsed == null)
                {
                    mappedChanges.Add(best);
                    continue;
                }

                if (alreadyUsed.similarityIndex > best.similarityIndex)
                {
                    // in case the added member was already bound to a removal that represents a worse pick than the current one
                    // put the removed member back in the process queue and mark the current pair as a better bouding
                    removedStack.Push(alreadyUsed.removed);
                    mappedChanges.Remove(alreadyUsed);

                    mappedChanges.Add(best);
                }
            }

            foreach (var mappedChange in mappedChanges)
            {
                notMapped.Remove(mappedChange.added);

                changes.Changes.Remove(mappedChange.removed);
                changes.Changes.Add(mappedChange.mappedTo);
                foreach (var change in mappedChange.aditionalChanges)
                {
                    if (change.GetHashCode() != mappedChange.mappedTo.GetHashCode())
                        changes.Changes.Add(change);
                }
            }

            return notMapped;
        }

        private class MappedChange
        {
            public MethodDefinition added;
            public MemberRemoved removed;
            public IEnumerable<IAPIChange> aditionalChanges;
            public int similarityIndex;

            public IAPIChange mappedTo;

            public MappedChange()
            {
                similarityIndex = Int32.MaxValue;
            }

            public MappedChange(int similarity, IAPIChange mappedTo, MethodDefinition added, MemberRemoved removed, List<IAPIChange> aditionalChanges)
            {
                this.added = added;
                this.removed = removed;
                this.aditionalChanges = aditionalChanges;
                this.mappedTo = mappedTo;

                similarityIndex = similarity;
            }
        }

        /**
         * This visitor is used to rank multiple changes being applied a single method
         *
         */
        internal class MethodChangesRankVisitor : APIChangeVisitorBaseFull
        {
            private int m_Rank;

            // Calculated rank. Lower values indicates smaller (i.e, less drastic) changes
            public int Rank { get { return m_Rank; } }

            public IAPIChange Change { get; private set; }

            public override void Visit(MethodParameterCountChange change)
            {
                var originalParams = change.OriginalEntity.Parameters;
                var currentParams = change.CurrentEntity.Parameters;

                var i = 0;
                while (i < originalParams.Count && i < currentParams.Count)
                {
                    if (originalParams[i].ParameterType.FullName != currentParams[i].ParameterType.FullName)
                        break;

                    i++;
                }

                var isSubsetOfParams = Math.Abs(originalParams.Count - currentParams.Count) == i;
                if (isSubsetOfParams)
                    m_Rank += 3; // appended or removed from last params
                else
                    m_Rank += 5; // inserted or removed from middle or params

                Change = change;
            }

            public override void Visit(MethodParameterTypeChange change)
            {
                m_Rank += 4;
                Change = change;
            }

            public override void Visit(ParameterReferencenessChange change)
            {
                m_Rank += 2;
                Change = change;
            }

            public override void Visit(ElementTypeChange change)
            {
                m_Rank += 3; // same rank as adding/removing last param
                Change = change;
            }
        }
    }
}
