using System.Collections.Generic;
using Mono.Cecil;
using Unity.APIComparison.Framework.Changes;

namespace Unity.APIComparison.Framework.Collectors
{
    /*
     * This visitor checks if a type have changes to instanceness and /or entity type(like class -> interface / struct)
     * and removal or addition of default ctor.
     *
     * It is used to avoid tagging the ctor removal or addition in those cases (these changes means the new type will gain or lose its default constructor)
     */
    internal class CtorFalsePositiveCollector : APIChangeVisitorBaseFull
    {
        private readonly List<APIChangeBase<IMemberDefinition>> m_falsePositives;
        private bool m_isPotentialFalsePositive;

        public CtorFalsePositiveCollector(List<APIChangeBase<IMemberDefinition>> falsePositives)
        {
            m_falsePositives = falsePositives;
        }

        public override void Visit(EntityTypeChanged change)
        {
            if (change == null)
                return;

            m_isPotentialFalsePositive = true;
        }

        public override void Visit(TypeMoved change)
        {
            if (change == null)
                return;

            m_isPotentialFalsePositive = true;
        }

        public override void Visit(InstancenessChange change)
        {
            if (change.Original is TypeDefinition)
                m_isPotentialFalsePositive = true;
        }

        public override void Visit(MemberRemoved change)
        {
            if (!m_isPotentialFalsePositive)
                return;

            var method = change.Original as MethodDefinition;
            if (method == null)
                return;

            if (method.IsConstructor && method.Parameters.Count == 0)
                m_falsePositives.Add(change);
        }

        public override void Visit(MemberAdded change)
        {
            if (!m_isPotentialFalsePositive)
                return;

            var method = change.Current as MethodDefinition;
            if (method == null)
                return;

            if (method.IsConstructor && method.Parameters.Count == 0)
                m_falsePositives.Add(change);
        }
    }
}
