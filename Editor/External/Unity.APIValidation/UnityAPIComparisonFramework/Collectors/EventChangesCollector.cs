using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Unity.APIComparison.Framework.Changes;

namespace Unity.APIComparison.Framework.Collectors
{
    public class EventChangesCollector : MemberChangesCollector<EventDefinition>
    {
        public EventChangesCollector(Dictionary<string, IEntityChange> changes) : base(changes)
        {
        }

        public override IEnumerable<EventDefinition> MembersFor(TypeDefinition type)
        {
            return type.Events;
        }

        public override bool IsPublicAPI(EventDefinition member)
        {
            return member.IsPublicAPI();
        }

        public override EventDefinition FindMember(TypeDefinition typeDefinition, EventDefinition member, out MemberKind missingMemberKind)
        {
            missingMemberKind = MemberKind.Event;
            return typeDefinition.Events.SingleOrDefault(candidate => candidate.Name == member.Name);
        }

        protected override EventDefinition FindOverridenMember(EventDefinition added, TypeDefinition originalType)
        {
            TypeDefinition overridenInType = null;
            if (added.AddMethod != null && added.AddMethod.IsVirtual())
            {
                var overriden = added.AddMethod.FindOverridenMethod();
                overridenInType = overriden?.DeclaringType;
            }

            return overridenInType == null 
                ? null 
                : overridenInType.Events.SingleOrDefault(c => c.Name == added.Name);
        }

        protected override IEqualityComparer<EventDefinition> GetComparer()
        {
            return comparer;
        }

        protected override TypeReference ElementTypeOf(EventDefinition member)
        {
            return member.EventType;
        }

        protected override bool IsStatic(EventDefinition member)
        {
            return member.AddMethod.IsStatic;
        }

        protected override MemberAccessibilityChangeKind AccessibilityChangeKind(EventDefinition current, EventDefinition original)
        {
            var m2 = current.RemoveMethod;
            var m1 = current.AddMethod;

            var originalM1 = original.AddMethod;
            var originalM2 = original.RemoveMethod;

            return AccessibilityChangeKind(m1, originalM1, m2, originalM2);
        }

        protected override string GetDataForHash(EventDefinition originalMember, EventDefinition newMember)
        {
            string data = string.Empty;
            if (originalMember != null)
                data += originalMember.FullName + "-" + (originalMember.AddMethod != null ? "add" : "") + (originalMember.RemoveMethod != null ? "remove" : "");

            if (newMember != null)
                data += newMember.FullName + "-" + (newMember.AddMethod != null ? "add" : "") + (newMember.RemoveMethod != null ? "remove" : "");

            return data;
        }

        private static readonly IEqualityComparer<EventDefinition> comparer = new MemberDefinitionEqualityComparer<EventDefinition>();
    }
}
