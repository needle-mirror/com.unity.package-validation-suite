using System.Collections.Generic;
using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class MethodParameterTypeChange : APIChangeBase<MethodDefinition>
    {
        public MethodParameterTypeChange(MethodDefinition original, MethodDefinition current, IEnumerable<ParameterDefinition> mismatches) : base(original, current)
        {
            Mismatches = mismatches;
        }

        public IEnumerable<ParameterDefinition> Mismatches { get; private set; }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
