using System.Collections.Generic;
using Mono.Cecil;
using Unity.APIComparison.Framework.Changes;

namespace Unity.APIComparison.Framework.Collectors
{
    internal class MemberRemovalCollector : APIChangeVisitorBaseFull
    {
        private readonly string m_memberName;
        private readonly MemberKind m_memberKind;
        private List<MemberRemoved> m_removedMembers;

        public MemberRemovalCollector(string memberName, MemberKind memberKind)
        {
            m_memberName = memberName;
            m_memberKind = memberKind;
            m_removedMembers = new List<MemberRemoved>();
        }

        public IEnumerable<MemberRemoved> RemovedMembers
        {
            get
            {
                m_removedMembers.Sort(AscendingNumberOfParameters);
                return m_removedMembers;
            }
        }

        private int AscendingNumberOfParameters(MemberRemoved x, MemberRemoved y)
        {
            var lhs = (MethodDefinition)x.Original;
            var rhs = (MethodDefinition)y.Original;

            return lhs.Parameters.Count - rhs.Parameters.Count;
        }

        public override void Visit(MemberRemoved change)
        {
            if (change.OriginalEntity.Name == m_memberName && change.Original.IsKind(m_memberKind))
                m_removedMembers.Add(change);
        }
    }
}
